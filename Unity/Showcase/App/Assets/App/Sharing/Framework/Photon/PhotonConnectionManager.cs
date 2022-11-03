// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if PHOTON_INSTALLED
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    public class PhotonConnectionManager :
        IConnectionCallbacks,
        IMatchmakingCallbacks,
        ILobbyCallbacks,
        IDisposable
    {
        private string _roomNameFormat;
        private string _appIdVoice = null;
        private string _appIdRealtime = null;
        private bool _isDisposed = false;
        private LogHelper<PhotonConnectionManager> _logger = new LogHelper<PhotonConnectionManager>();
        private TypedLobby _lobby = new TypedLobby("main", LobbyType.Default);
        private SortedDictionary<string, PhotonSharingRoom> _rooms = new SortedDictionary<string, PhotonSharingRoom>();
        private object _taskLock = new object();
        private TaskCompletionSource<PhotonSharingRoom> _taskJoinRoom = null;
        private TaskCompletionSource<object> _taskLogin = null;
        private TaskCompletionSource<object> _taskLogout = null;
        private PhotonSharingRoom _lastRoom = null;

        public PhotonConnectionManager(SharingServiceProfile settings)
        {
            _logger.Verbose = settings.VerboseLogging ? LogHelperState.Always : LogHelperState.Default;
            _roomNameFormat = settings.RoomNameFormat;
            _appIdVoice = settings.PhotonVoiceId;
            _appIdRealtime = settings.PhotonRealtimeId;

            PhotonNetwork.AddCallbackTarget(this);

            if (string.IsNullOrEmpty(_roomNameFormat) ||
                !_roomNameFormat.Contains("{0}"))
            {
                LogVerbose("Room name format is invalid, falling back to default (format = {0})", _roomNameFormat ?? "NULL");
                _roomNameFormat = "Room {0}";
            }
        }

        #region Public Properties
        /// <summary>
        /// True if connected to sharing service and logged in. But not necessarily in a session/room
        /// </summary>
        public bool IsLoggedIn =>
            State == ManagerState.ConnectingToLobby ||
            State == ManagerState.ConnectedToLobby ||
            State == ManagerState.ConnectedToRoom ||
            State == ManagerState.ConnectingToRoom;

        /// <summary>
        /// True when client is connected to a session/room
        /// </summary>
        public bool IsConnected => State == ManagerState.ConnectedToRoom;

        /// <summary>
        /// Get if the provider is connecting to a session/room
        /// </summary>
        public bool IsConnecting => State == ManagerState.ConnectingToRoom;

        /// <summary>
        /// Get the currently known room
        /// </summary>
        public IReadOnlyCollection<PhotonSharingRoom> Rooms => _rooms.Values;

        /// <summary>
        /// Get the currently joined room
        /// </summary>
        public PhotonSharingRoom CurrentRoom { get; private set; }
        #endregion Public Properties

        #region Private Properties
        /// <summary>
        /// Get the current state of the Photon connection
        /// </summary>
        private ManagerState State { get; set; } = ManagerState.DisconnectedFromService;
        #endregion Private Properties

        #region Public Events
        /// <summary>
        /// Event fired when client is connected to a network, but not necessarily to a session/room
        /// </summary>
        public event Action<PhotonConnectionManager> LoggedIn;

        /// <summary>
        /// Event fired when client is connected to a session/room
        /// </summary>
        public event Action<PhotonConnectionManager> Connected;

        /// <summary>
        /// Event raised when a new client is connecting to a session/room
        /// </summary>
        public event Action<PhotonConnectionManager> Connecting;

        /// <summary>
        /// Event fired when client is disconnected from a session/room
        /// </summary>
        public event Action<PhotonConnectionManager> Disconnected;

        /// <summary>
        /// Event fired when the current room has changed.
        /// </summary>
        public event Action<PhotonConnectionManager, ISharingServiceRoom> CurrentRoomChanged;

        /// <summary>
        /// Event fired when the rooms have changed.
        /// </summary>
        public event Action<PhotonConnectionManager, IReadOnlyCollection<ISharingServiceRoom>> RoomsChanged;
        #endregion Public Events

        #region Public Functions
        /// <summary>
        /// Log into the Photon network.
        /// </summary>
        public async Task Login()
        {
            if (PhotonNetwork.InLobby)
            {
                return;
            }

            try
            {
                await TaskWait(ref _taskLogout);
            }
            catch { }

            // Load user name for later
            var userNameTask = PhotonLocalUser.GetUserName();

            // Start logging in
            bool newlyCreated;
            var taskSource = TaskGetOrCreate(ref _taskLogin, out newlyCreated);
            if (newlyCreated && !JoinNetwork() && !JoinLobby())
            {
                TaskSetException(ref taskSource, new Exception("Failed to log into the Photon network."));
            }

            await taskSource.Task;

            // Commit user name
            if (PhotonNetwork.LocalPlayer != null)
            {
                PhotonNetwork.LocalPlayer.NickName = await userNameTask;
            }
        }

        /// <summary>
        /// Log out of the Photon network.
        /// </summary>
        public async Task Logout()
        {
            if (!PhotonNetwork.IsConnected)
            {
                return;
            }

            try
            {
                await TaskWait(ref _taskLogin);
            }
            catch { }

            bool newlyCreated;
            var taskSource = TaskGetOrCreate(ref _taskLogout, out newlyCreated);
            if (newlyCreated && !LeaveNetwork())
            {
                TaskSetResult(ref taskSource);
            }

            await taskSource.Task;
        }

        /// <summary>
        /// Leave the current room
        /// </summary>
        /// <returns></returns>
        public async Task LeaveRoom()
        {
            LogVerbose("LeaveRoom()");

            try
            {
                await TaskWait(ref _taskJoinRoom);
            }
            catch { }

            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LeaveRoom(becomeInactive: true);
            }
        }

        /// <summary>
        /// Create a new Photon room with a unique name.
        /// </summary>
        public Task<PhotonSharingRoom> CreateRoom()
        {
            LogVerbose("CreateRoom()");

            int roomIndex = 1;
            string roomName = _roomNameFormat;
            bool foundUniqueRoomName = false;

            while (!foundUniqueRoomName)
            {
                roomName = string.Format(_roomNameFormat, roomIndex++);
                foundUniqueRoomName = !_rooms.ContainsKey(roomName);
            }

            return JoinOrCreateRoom(roomName);
        }

        /// <summary>
        /// Attempt to join the given room. If the room doesn't exist it'll be created, and joined.
        /// </summary>
        public Task<PhotonSharingRoom> JoinOrCreateRoom(ISharingServiceRoom room)
        {
            LogVerbose("JoinOrCreateRoom()");

            PhotonSharingRoom photonSharingRoom = room as PhotonSharingRoom;
            if (photonSharingRoom == null)
            {
                return Task.FromException<PhotonSharingRoom>(new ArgumentException("Room was not a PhotonSharingRoom"));
            }
            else
            {
                return JoinOrCreateRoom(photonSharingRoom.Name);
            }
        }

        /// <summary>
        /// Attempt to join the given room. If the room doesn't exist it'll be created, and joined.
        /// </summary>
        public async Task<PhotonSharingRoom> JoinOrCreateRoom(string roomName)
        {
            LogVerbose("JoinOrCreateRoom() (room name = {0})", roomName);

            if (string.IsNullOrEmpty(roomName))
            {
                throw new ArgumentException("Room name was null or empty");
            }

            if (CurrentRoom != null &&
                CurrentRoom.Name == roomName)
            {
                return CurrentRoom;
            }

            await Login();            

            bool newlyCreated;
            var taskSource = TaskGetOrCreate(ref _taskJoinRoom, out newlyCreated);
            if (!newlyCreated)
            {
                throw new ArgumentException("Currently joining a room. Please try again.");
            }

            if (PhotonNetwork.InRoom)
            {
                throw new ArgumentException("Can't join a room, already in a room.");
            }

            var options = new RoomOptions()
            {
                IsVisible = true,
                IsOpen = true,
                CleanupCacheOnLeave = false,
                DeleteNullProperties = true,
                EmptyRoomTtl = 10000,
                PlayerTtl = 5000,
            };

            // Join might fail if this is a valid "rejoin". However 
            // tracking rejoins is not trivial. So join may fail, but
            // the user can easily retry, and eventually Photon will
            // timeout the old user and joining will succeed.
            if (PhotonNetwork.JoinOrCreateRoom(roomName, options, _lobby))
            {
                LogVerbose("JoinOrCreateRoom() Joining or Creating");
                UpdateCurrentState(ManagerState.ConnectingToRoom);
            }
            else
            {
                LogVerbose("JoinOrCreateRoom() Can't join or create");
                TaskSetExceptionIfMatch(ref _taskJoinRoom, taskSource, new Exception("Failed to create Photon room"));
            }

            return await taskSource.Task;
        }
        #endregion Public Functions

        #region IDispose Functions
        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                PhotonNetwork.RemoveCallbackTarget(this);
            }
        }
        #endregion IDispose Functions

        #region IConnectionCallbacks Functions        
        /// <summary>
        /// Called to signal that the "low level connection" got established but before the client can call operation on the server.
        /// </summary>
        public void OnConnected()
        {
            LogVerbose("OnConnected()");
        }

        /// <summary>
        /// Called when the client is connected to the Master Server and ready for matchmaking and other tasks.
        /// </summary>
        public void OnConnectedToMaster()
        {
            LogVerbose("OnConnectedToMaster() (server = {0}) (region = {1})", PhotonNetwork.ServerAddress, PhotonNetwork.CloudRegion);
            JoinLobby();
        }

        /// <summary>
        /// Called when the custom authentication failed. Followed by disconnect!
        /// </summary>
        public void OnCustomAuthenticationFailed(string debugMessage)
        {
            LogError("OnCustomAuthenticationFailed() (error = {0})", debugMessage);
        }

        /// <summary>
        /// Called when your Custom Authentication service responds with additional data.
        /// </summary>
        public void OnCustomAuthenticationResponse(Dictionary<string, object> data)
        {
            LogVerbose("OnCustomAuthenticationResponse() (data = {0})", data);
        }

        /// <summary>
        /// Called after disconnecting from the Photon server. It could be a failure or an explicit disconnect call
        /// </summary>
        public void OnDisconnected(DisconnectCause cause)
        {
            LogVerbose("OnDisconnected() (cause = {0})", cause);
            UpdateCurrentState();
            TaskSetException(ref _taskJoinRoom, new Exception($"Failed to join room. Disconnected."));
            TaskSetException(ref _taskLogin, new Exception($"Failed to login. Disconnected."));
            TaskSetResult(ref _taskLogout);
        }

        /// <summary>
        /// Called when the Name Server provided a list of regions for your title.
        /// </summary>
        public void OnRegionListReceived(RegionHandler regionHandler)
        {
            LogVerbose("OnRegionListReceived()");
        }
        #endregion IConnectionCallbacks Functions

        #region ILobbyCallbacks Functions        
        /// <summary>
        /// Called on entering a lobby on the Master Server. The actual room-list updates will call OnRoomListUpdate.
        /// </summary>
        public void OnJoinedLobby()
        {
            LogVerbose($"OnJoinedLobby()");
            UpdateCurrentState();
            TaskSetException(ref _taskJoinRoom, new Exception($"Failed to join room. In lobby."));
            TaskSetResult(ref _taskLogin);
            //PhotonNetwork.GetCustomRoomList(_lobby, "*");
        }

        /// <summary>
        /// Called after leaving a lobby.
        /// </summary>
        public void OnLeftLobby()
        {
            LogVerbose($"OnLeftLobby()");
            UpdateCurrentState();
        }

        /// <summary>
        /// Called for any update of the room-listing while in a lobby (InLobby) on the Master Server.
        /// </summary>
        public void OnRoomListUpdate(List<RoomInfo> updates)
        {
            LogVerbose("OnRoomListUpdate() (room count = {0})", PhotonNetwork.CountOfRooms);
            UpdateSharingRooms(updates);
        }

        /// <summary>
        /// Called when the Master Server sent an update for the Lobby Statistics.
        /// </summary>
        public void OnLobbyStatisticsUpdate(List<TypedLobbyInfo> lobbyStatistics)
        {
            LogVerbose($"OnLobbyStatisticsUpdate()");
        }
        #endregion ILobbyCallbacks Functions

        #region IMatchMakingCallbacks Functions
        /// <summary>
        /// Called when the server sent the response to a FindFriends request.
        /// </summary>
        public void OnFriendListUpdate(List<FriendInfo> friendList)
        {
            LogVerbose($"OnFriendListUpdate()");
        }

        /// <summary>
        /// Called when this client created a room and entered it. OnJoinedRoom() will be called as well.
        /// </summary>
        public void OnCreatedRoom()
        {
            LogVerbose("Created room");
        }

        /// <summary>
        /// Called when the server couldn't create a room (OpCreateRoom failed).
        /// </summary>
        public void OnCreateRoomFailed(short returnCode, string message)
        {
            LogError("OnCreateRoomFailed() (message = {0}) (code = {1})", message, returnCode);
            UpdateCurrentState();
            TaskSetException(ref _taskJoinRoom, new Exception($"Failed to create room ({returnCode}). {message}"));
        }

        /// <summary>
        /// Called when the LoadBalancingClient entered a room, no matter if this client created it or simply joined.
        /// </summary>
        public void OnJoinedRoom()
        {
            LogVerbose("OnJoinedRoom() (room name = {0}) (player id = {1})", PhotonNetwork.CurrentRoom?.Name, PhotonNetwork.LocalPlayer?.ActorNumber);
            UpdateCurrentState();
            TaskSetResult(ref _taskJoinRoom, CurrentRoom);
        }

        /// <summary>
        /// Called when a previous OpJoinRoom call failed on the server.
        /// </summary>
        public void OnJoinRoomFailed(short returnCode, string message)
        {
            LogError("OnJoinRoomFailed() (message = {0}) (code = {1})", message, returnCode);
            UpdateCurrentRoom();
            UpdateCurrentState();
            TaskSetException(ref _taskJoinRoom, new Exception($"Failed to join room ({returnCode}). {message}"));
        }

        /// <summary>
        /// Called when a previous OpJoinRandom call failed on the server.
        /// </summary>
        public void OnJoinRandomFailed(short returnCode, string message)
        {
            LogError("OnJoinRandomFailed() (message = {0}) (code = {1})", message, returnCode);
            UpdateCurrentRoom();
            UpdateCurrentState();
            TaskSetException(ref _taskJoinRoom, new Exception($"Failed to join room ({returnCode}). {message}"));
        }

        /// <summary>
        /// Called when the local user/client left a room, so the game's logic can clean up it's internal state.
        /// </summary>
        public void OnLeftRoom()
        {
            LogVerbose("OnLeftRoom()");
            UpdateCurrentRoom();
            UpdateCurrentState();
            TaskSetException(ref _taskJoinRoom, new Exception($"Failed to join room. User left room."));
        }
        #endregion IMatchMakingCallbacks Functions

        #region Private Functions
        private bool JoinNetwork()
        {
            LogVerbose("JoinNetwork()");
            ValidatePhotonConfiguration();

            if (Application.isEditor)
            {
                // Keep alive for 30 minutes to help with debugging
                PhotonNetwork.NetworkingClient.LoadBalancingPeer.DisconnectTimeout = 1000 * 60 * 30;
            }

            bool joining = false;
            if (!PhotonNetwork.IsConnected)
            {
                PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime = _appIdRealtime;
                PhotonNetwork.PhotonServerSettings.AppSettings.AppIdVoice = _appIdVoice;
                PhotonNetwork.ConnectUsingSettings();
                joining = true;
            }

            return joining;
        }

        /// <summary>
        /// Starting leaving the photon network
        /// </summary>
        private bool LeaveNetwork()
        {
            bool leaving = false;
            if (PhotonNetwork.IsConnected)
            {
                PhotonNetwork.Disconnect();
                leaving = true;
            }
            return leaving;
        }

        /// <summary>
        /// Validate that Photon is configured correctly.
        /// </summary>
        private void ValidatePhotonConfiguration()
        {
            string error = null;

            if (string.IsNullOrEmpty(_appIdRealtime))
            {
                _appIdRealtime = PhotonNetwork.PhotonServerSettings?.AppSettings?.AppIdRealtime;
            }

            if (string.IsNullOrEmpty(_appIdRealtime))
            {
                error = "The Photon real-time app id hasn't been specified. Please check the configuration of 'SharingService' MRTK extension.";
            }

            if (string.IsNullOrEmpty(_appIdVoice))
            {
                _appIdVoice = PhotonNetwork.PhotonServerSettings?.AppSettings?.AppIdVoice;
            }

            if (string.IsNullOrEmpty(_appIdVoice))
            {
                error = "The Photon voice app id hasn't been specified. Please check the configuration of 'SharingService' MRTK extension.";
            }

            if (error != null)
            {
                Debug.LogError(error);
                AppServices.AppNotificationService.RaiseNotification(error, AppNotificationType.Error);
                throw new ApplicationException(error);
            }
        }

        /// <summary>
        /// Join the lobby
        /// </summary>
        private bool JoinLobby()
        {
            if (!PhotonNetwork.InLobby &&
                PhotonNetwork.JoinLobby(_lobby))
            {
                UpdateCurrentState(ManagerState.ConnectingToLobby);
                return true;
            }
            else
            {
                UpdateCurrentState();
                return false;
            }
        }

        /// <summary>
        /// Update the _currentRoom field to match whatever the current Photon room is.
        /// </summary>
        private void UpdateCurrentRoom()
        {
            if (PhotonNetwork.CurrentRoom == null)
            {
                CurrentRoom = null;
            }
            else
            {
                _lastRoom = CurrentRoom = CreateSharingRoom(PhotonNetwork.CurrentRoom);
            }
        }

        /// <summary>
        /// Update the current state based on photon information
        /// </summary>
        /// <param name="force">
        /// Force the state to this given value.
        /// </param>
        private void UpdateCurrentState(ManagerState? force = null)
        {
            ManagerState oldState = State;
            ManagerState newState;

            if (force != null)
            {
                newState = force.Value;
            }
            else if (PhotonNetwork.InRoom)
            {
                newState = ManagerState.ConnectedToRoom;
            }
            else if (PhotonNetwork.InLobby)
            {
                newState = ManagerState.ConnectedToLobby;
            }
            else if (PhotonNetwork.IsConnected)
            {
                newState = ManagerState.ConnectingToLobby;
            }
            else
            {
                newState = ManagerState.DisconnectedFromService;
            }

            if (newState != oldState)
            {
                State = newState;

                //
                // Handle leaving a state 
                //

                switch (oldState)
                {
                    case ManagerState.ConnectedToRoom:
                        UpdateCurrentRoom();
                        Disconnected?.Invoke(this);
                        CurrentRoomChanged?.Invoke(this, CurrentRoom);
                        break;
                }

                //
                // Handle entering a state
                //

                switch (newState)
                {
                    case ManagerState.DisconnectedFromService:
                    case ManagerState.ConnectingToService:
                    case ManagerState.ConnectingToLobby:
                        break;
                    case ManagerState.ConnectedToLobby:
                        LoggedIn?.Invoke(this);
                        break;
                    case ManagerState.ConnectingToRoom:
                        Connecting?.Invoke(this);
                        break;
                    case ManagerState.ConnectedToRoom:
                        UpdateCurrentRoom();
                        Connected?.Invoke(this);
                        CurrentRoomChanged?.Invoke(this, CurrentRoom);
                        break;
                }
            }
        }

        /// <summary>
        /// Update the rooms in the lobby, based on Photon updates.
        /// </summary>
        private PhotonSharingRoom CreateSharingRoom(Room room)
        {
            return new PhotonSharingRoom(room.Name, room);
        }

        /// <summary>
        /// Update the rooms in the lobby, based on Photon updates.
        /// </summary>
        private void UpdateSharingRooms(List<RoomInfo> updates)
        {
            int length = updates?.Count ?? 0;
            LogVerbose("UpdateSharingRooms() (updates = {0})", length);
            for (int i = 0; i < length; i++)
            {
                RoomInfo info = updates[i];

                if (info.RemovedFromList)
                {
                    _rooms.Remove(info.Name);
                }
                else
                {
                    if (CurrentRoom != null && info.Name == CurrentRoom.Name)
                    {
                        _rooms[info.Name] = CurrentRoom;
                    }
                    else
                    {
                        _rooms[info.Name] = new PhotonSharingRoom(info.Name, info);
                    }
                }
            }

            RoomsChanged?.Invoke(this, _rooms.Values);
        }

        /// <summary>
        /// Try get a task completion source. If task complete source is completed, a new one is created
        /// </summary>
        /// <param name="created">If a new task completion source was created.</param>
        /// <returns>The task completion source to use.</returns>
        private TaskCompletionSource<T> TaskGetOrCreate<T>(ref TaskCompletionSource<T> taskSource, out bool created)
        {
            TaskCompletionSource<T> result;
            lock (_taskLock)
            {
                result = taskSource;
                if (result == null || result.Task.IsCompleted || result.Task.IsFaulted || result.Task.IsCanceled)
                {
                    result = taskSource = new TaskCompletionSource<T>();
                    created = true;
                }
                else
                {
                    created = false;
                }
            }
            return result;
        }

        /// <summary>
        /// A helper that clears a the references value, and sets the result of the task source.
        /// </summary>
        private void TaskSetResult<T>(ref TaskCompletionSource<T> taskSource, T result = default)
        {
            TaskCompletionSource<T> oldSource = null;
            lock (_taskLock)
            {
                oldSource = taskSource;
            }
            oldSource?.TrySetResult(result);
        }

        /// <summary>
        /// A helper that clears a the references value, and sets the result of the task source.
        /// </summary>
        private void TaskSetResultIfMatch<T>(ref TaskCompletionSource<T> taskSource, TaskCompletionSource<T> match, T result = default)
        {
            TaskCompletionSource<T> oldSource = null;
            lock (_taskLock)
            {
                if (match == taskSource)
                {
                    oldSource = taskSource;
                }
            }
            oldSource?.TrySetResult(result);
        }

        /// <summary>
        /// A helper that clears a the references value, and sets the exception of the task source.
        /// </summary>
        private void TaskSetException<T>(ref TaskCompletionSource<T> taskSource, Exception exception)
        {
            TaskCompletionSource<T> oldSource = null;
            lock (_taskLock)
            {
                oldSource = taskSource;
            }
            oldSource?.TrySetException(exception);
        }

        /// <summary>
        /// A helper that clears a the references value, and sets the exception of the task source.
        /// </summary>
        private void TaskSetExceptionIfMatch<T>(ref TaskCompletionSource<T> taskSource, TaskCompletionSource<T> match, Exception exception)
        {
            TaskCompletionSource<T> oldSource = null;
            lock (_taskLock)
            {
                if (match == taskSource)
                {
                    oldSource = taskSource;
                }
            }
            oldSource?.TrySetException(exception);
        }

        /// <summary>
        /// A helper that clears a the references value, and cancels the task source.
        /// </summary>
        private void TaskSetCancel<T>(ref TaskCompletionSource<T> taskSource)
        {
            TaskCompletionSource<T> oldSource = null;
            lock (_taskLock)
            {
                oldSource = taskSource;
            }
            oldSource?.TrySetCanceled();
        }

        /// <summary>
        /// A helper that safely waits on task
        /// </summary>
        private Task<T> TaskWait<T>(ref TaskCompletionSource<T> taskSource)
        {
            TaskCompletionSource<T> source = null;
            lock (_taskLock)
            {
                source = taskSource;
            }

            if (source == null)
            {
                return Task.FromResult<T>(default);
            }
            else
            {
                return source.Task;
            }
        }

        /// <summary>
        /// Log a message if verbose logging is enabled.
        /// </summary>
        private void LogVerbose(string message)
        {
            _logger.LogVerbose(message);
        }

        /// <summary>
        /// Log a message if verbose logging is enabled. 
        /// </summary>
        private void LogVerbose(string messageFormat, params object[] args)
        {
            _logger.LogVerbose(messageFormat, args);
        }

        /// <summary>
        /// Log a message if error logging is enabled.
        /// </summary>
        private void LogError(string message)
        {
            _logger.LogError(message);
        }

        /// <summary>
        /// Log a message if verbose logging is enabled. 
        /// </summary>
        private void LogError(string messageFormat, params object[] args)
        {
            _logger.LogError(messageFormat, args);
        }
        #endregion Private Functions

        private enum ManagerState
        {
            DisconnectedFromService,
            ConnectingToService,
            ConnectingToLobby,
            ConnectedToLobby,
            ConnectingToRoom,
            ConnectedToRoom,
        }
    }
}
#endif
