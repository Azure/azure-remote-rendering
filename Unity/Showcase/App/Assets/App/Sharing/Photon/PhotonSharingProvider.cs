// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if PHOTON_INSTALLED
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    /// <summary>
    /// Internal class used for implementation of a sharing protocol.
    /// </summary>
    public class PhotonSharingProvider :
        ISharingProvider,
        IConnectionCallbacks,
        IMatchmakingCallbacks,
        IOnEventCallback,
        ILobbyCallbacks,
        IInRoomCallbacks,
        IDisposable
    {
        private static bool _customMessageRegistered = false;
        private bool _autoReconnect;
        private string _roomNameFormat;
        private bool _verboseLogging;
        private bool _callbacksRegisteredWithPhoton = false;
        private bool _autoJoinRoom = false;
        private bool _isConnected = false;
        private PhotonSharingRoom _currentRoom = null;
        private string _pendingRoomName = null;
        private string _appIdChat = null;
        private string _appIdRealtime = null;
        private float _timeSinceRoomUpdate;
        private SortedDictionary<string, PhotonSharingRoom> _rooms = new SortedDictionary<string, PhotonSharingRoom>();
        private Dictionary<int, Player> _players = new Dictionary<int, Player>();
        private BufferPool _bufferPool = new BufferPool();
        private Hashtable _setPropertyBuffer = new Hashtable(1);
        private Hashtable _expectedPropertyBuffer = new Hashtable(1);
        private HashSet<Type> _customTypes = new HashSet<Type>();

        private static byte _commandMessageCode = 199;
        private static byte _transformMessageCode = 198;
        private static byte _playerPoseCode = 197;

        private static byte _sharingServiceMessageType = 199;
        private static byte _transformType = 198;
        private static byte _poseType = 197;

        private const string _lastRoomKey = "Microsoft.MixedReality.Toolkit.Extensions.PhotonSharingProvider.LastRoom";

        public PhotonSharingProvider(SharingServiceProfile settingsProfile)
        {
            _autoReconnect = settingsProfile.AutoReconnect;
            _roomNameFormat = settingsProfile.RoomNameFormat;
            _verboseLogging = settingsProfile.VerboseLogging;
            _appIdRealtime = settingsProfile.PhotonRealtimeId;

            LogVerbose("PhotonSharingProvider()");

            if (string.IsNullOrEmpty(_roomNameFormat) ||
                !_roomNameFormat.Contains("{0}"))
            {
                LogVerbose("Room name format is invalid, falling back to default (format = {0})", _roomNameFormat ?? "NULL");
                _roomNameFormat = "Room {0}";
            }

            RegisterCustomMessageType<SharingServiceMessage>(
                _sharingServiceMessageType,
                SerializeCommandMessage,
                DeserializeCommandMessage);

            RegisterCustomMessageType<SharingServiceTransform>(
                _transformType,
                SerializeSharingServiceTransform,
                DeserializeSharingServiceTransform);

            RegisterCustomMessageType<Pose>(
                _poseType,
                SerializePose,
                DeserializePose);
        }

        public void Dispose()
        {
        }

        #region Private Properties
        /// <summary>
        /// The last room the user connected to.
        /// </summary>
        private string LastRoom
        {
            get
            {
                string lastRoom = null;
#if UNITY_EDITOR
                lastRoom = UnityEditor.EditorPrefs.GetString(_lastRoomKey);
#else
                lastRoom = PlayerPrefs.GetString(_lastRoomKey);
#endif
                LogVerbose("Querying LastRoom. (lastRoom = {0})", lastRoom);
                return lastRoom;
            }

            set
            {
                LogVerbose("Setting LastRoom. (lastRoom = {0})", value);
#if UNITY_EDITOR
                UnityEditor.EditorPrefs.SetString(_lastRoomKey, value);
#else
                PlayerPrefs.SetString(_lastRoomKey, value);
#endif
            }
        }
        #endregion Private Properties

        #region ISharingProvider Events
        /// <summary>
        /// Event fired when client is connected
        /// </summary>
        public event Action<ISharingProvider> Connected;

        /// <summary>
        /// Event fired when client is disconnected
        /// </summary>
        public event Action<ISharingProvider> Disconnected;

        /// <summary>
        /// Event fired when a new message is received.
        /// </summary>
        public event Action<ISharingProvider, ISharingServiceMessage> MessageReceived;

        /// <summary>
        /// A specialized message optimized for sending a transform to a target.
        /// </summary>
        public event Action<ISharingProvider, string, SharingServiceTransform> TransformMessageReceived;

        /// <summary>
        /// Event fired when a property changes.
        /// </summary>
        public event Action<ISharingProvider, string, object> PropertyChanged;

        /// <summary>
        /// Event fired when the current room has changed.
        /// </summary>
        public event Action<ISharingProvider, ISharingServiceRoom> CurrentRoomChanged;

        /// <summary>
        /// Event fired when the rooms have changed.
        /// </summary>
        public event Action<ISharingProvider, IReadOnlyCollection<ISharingServiceRoom>> RoomsChanged;

        /// <summary>
        /// Event fired when a new player has been added.
        /// </summary>
        public event Action<ISharingProvider, int> PlayerAdded;

        /// <summary>
        /// Event fired when a player has been removed.
        /// </summary>
        public event Action<ISharingProvider, int> PlayerRemoved;

        /// <summary>
        /// Event fired when a player's property changes.
        /// </summary>
        public event Action<ISharingProvider, int, string, object> PlayerPropertyChanged;

        /// <summary>
        /// Event fired when a player's position and rotation has changed.
        /// </summary>
        public event Action<ISharingProvider, int, Pose> PlayerPoseChanged;
        #endregion ISharingProvider Events

        #region ISharingProvider Properties
        /// <summary>
        /// True when client is connected
        /// </summary>
        public bool IsConnected
        {
            // There's a delay when Photon notifies us of being disconnected or out of room.
            // So check these values here, and combine with our member variable.
            get => _isConnected && PhotonNetwork.IsConnected && PhotonNetwork.InRoom;

            private set
            {
                if (_isConnected != value)
                {
                    _isConnected = value;
                    if (_isConnected)
                    {
                        Connected?.Invoke(this);
                    }
                    else
                    {
                        Disconnected?.Invoke(this);
                    }
                }
            }
        }

        /// <summary>
        /// The player id of the local player.
        /// </summary>
        public int LocalPlayerId { get; private set; } = -1;

        /// <summary>
        /// The list of current room.
        /// </summary>
        public IReadOnlyCollection<ISharingServiceRoom> Rooms => _rooms.Values;

        /// <summary>
        /// Get the current room.
        /// </summary>
        public ISharingServiceRoom CurrentRoom
        {
            get => _currentRoom;
        }
        #endregion ISharingProvider Properties

        #region ISharingProvider Functions
        /// <summary>
        /// Connect and join the sharing service's lobby. This allows the client to see the available sharing rooms.
        /// </summary>
        public void Connect()
        {
            ConnectToPhoton();
        }

        /// <summary>
        /// Disconnected from sharing service. This leave the lobby and the client can no longer see the available sharing rooms.
        /// </summary>
        public void Disconnect()
        {
            DisconnectFromPhoton();
        }

        /// <summary>
        /// Update provider every frame.
        /// </summary>
        public void Update()
        {
            // Attempt to reconnect
            if (_callbacksRegisteredWithPhoton && !PhotonNetwork.IsConnected)
            {
                ConnectToPhoton();
            }

            if(PhotonNetwork.IsConnected && CurrentRoom == null)
            {
                _timeSinceRoomUpdate += Time.deltaTime;
                if(_timeSinceRoomUpdate > 10f)
                {
                    _timeSinceRoomUpdate = 0f;
                    UpdateRooms();
                }
            }
        }

        /// <summary>
        /// Create and join a new sharing room.
        /// </summary>
        public void CreateAndJoinRoom()
        {
            CreatePhotonRoom();
        }

        /// <summary>
        /// Join the given room.
        /// </summary>
        public void JoinRoom(ISharingServiceRoom room)
        {
            JoinOrCreatePhotonRoom(room);
        }

        /// <summary>
        /// Leave the currently joined sharing room, and join the default lobby.
        /// </summary>
        public void LeaveRoom()
        {
            LogVerbose("LeaveRoom()");
            LeavePhotonRoom();
        }

        /// <summary>
        /// Force the list of rooms to update now.
        /// </summary>
        public void UpdateRooms()
        {
            UpdatePhotonRooms();
        }

        /// <summary>
        /// Send the local player's transform.
        /// </summary>
        public void SendLocalPlayerPose(Pose pose)
        {
            if (IsConnected)
            {
                PhotonNetwork.RaiseEvent(_playerPoseCode, pose, RaiseEventOptions.Default, SendOptions.SendUnreliable);
            }
        }

        /// <summary>
        /// Send a specialized message that contains only a transform. 
        /// </summary>
        public void SendTransformMessage(string target, SharingServiceTransform transform)
        {
            if (IsConnected)
            {
                transform.Target = target;
                PhotonNetwork.RaiseEvent(_transformMessageCode, transform, RaiseEventOptions.Default, SendOptions.SendUnreliable);
            }
        }

        /// <summary>
        /// Sends a message to all other clients
        /// </summary>
        /// <param name="message">Message to send</param>
        public void SendMessage(ISharingServiceMessage message)
        {
            if (IsConnected)
            {
                PhotonNetwork.RaiseEvent(_commandMessageCode, message, RaiseEventOptions.Default, SendOptions.SendReliable);
            }
        }

        /// <summary>
        /// Sends a private reply to a message another client sent
        /// </summary>xx
        /// <param name="original">Original message to reply to</param>
        /// <param name="reply">Reply to send</param>
        public void SendReply(ISharingServiceMessage original, ISharingServiceMessage message)
        {
            var options = new RaiseEventOptions()
            {
                TargetActors = new int[] { ((PhotonMessage)original).Sender }
            };
            PhotonNetwork.RaiseEvent(_commandMessageCode, message, options, SendOptions.SendReliable);
        }

        /// <summary>
        /// Set a shared property on the server. Setting to null will clear the property from the server.
        /// </summary>
        public void SetProperty(string property, object value)
        {
            if (PhotonNetwork.CurrentRoom != null)
            {
                SetPhotonCustomProperty(PhotonNetwork.CurrentRoom.CustomProperties, PhotonNetwork.CurrentRoom.SetCustomProperties, property, value);
            }
        }

        /// <summary>
        /// Set a shared properties on the server. Setting to a value to null will clear the property from the server.
        /// </summary>
        public void SetProperties(params object[] propertyNamesAndValues)
        {
            if (PhotonNetwork.CurrentRoom != null)
            {
                SetPhotonCustomProperty(PhotonNetwork.CurrentRoom.CustomProperties, PhotonNetwork.CurrentRoom.SetCustomProperties, propertyNamesAndValues);
            }
        }

        /// <summary>
        /// Try to get a sharing service's property value.
        /// </summary>
        /// <returns>True if a non-null property value was found.</returns>
        public bool TryGetProperty(string property, out object value)
        {
            Hashtable properties = PhotonNetwork.CurrentRoom?.CustomProperties;
            if (string.IsNullOrEmpty(property) || properties == null)
            {
                value = default;
                return false;
            }

            return properties.TryGetValue(property, out value);
        }

        /// <summary>
        /// Does the sharing service have the current property name.
        /// </summary>
        public bool HasProperty(string property)
        {
            Hashtable properties = PhotonNetwork.CurrentRoom?.CustomProperties;
            if (string.IsNullOrEmpty(property) || properties == null)
            {
                return false;
            }

            return properties.ContainsKey(property) && properties[property] != null;
        }

        /// <summary>
        /// Clear all properties with the given prefix.
        /// </summary>
        public void ClearPropertiesStartingWith(string prefix)
        {
            Hashtable properties = PhotonNetwork.CurrentRoom?.CustomProperties;
            if (string.IsNullOrEmpty(prefix) || properties == null)
            {
                return;
            }

            Hashtable toRemove = new Hashtable();
            foreach (var entry in properties)
            {
                if (((string)entry.Key).StartsWith(prefix))
                {
                    toRemove[entry.Key] = null;
                }
            }

            PhotonNetwork.CurrentRoom.SetCustomProperties(toRemove);
        }

        /// <summary>
        /// Try to set a player's property value.
        /// </summary>
        public void SetPlayerProperty(int playerId, string property, object value)
        {
            Player player;
            lock (_players)
            {
                _players.TryGetValue(playerId, out player);
            }

            if (player != null)
            {
                LogVerbose("SetPlayerProperty() (player id = {0}) (name = {1}) (value = {2})", playerId, property, value);
                SetPhotonCustomProperty(player.CustomProperties, player.SetCustomProperties, property, value);
            }
            else
            {
                LogVerbose("SetPlayerProperty() error, unable to find player id (player id = {0}) (name = {1}) (value = {2})", playerId, property, value);
            }
        }

        /// <summary>
        /// Try to get a player's property value.
        /// </summary>
        /// <returns>True if a non-null property value was found.</returns>
        public bool TryGetPlayerProperty(int playerId, string key, out object value)
        {
            Player player;
            lock (_players)
            {
                _players.TryGetValue(playerId, out player);
            }

            bool found = false;
            if (player == null)
            {
                value = null;
            }
            else
            {
                found = player.CustomProperties.TryGetValue(key, out value) && value != null;
            }
            return found;
        }

        // <summary>
        /// Does this provider have the current property for the player.
        /// </summary>
        public bool HasPlayerProperty(int playerId, string property)
        {
            object value;
            return TryGetPlayerProperty(playerId, property, out value);
        }
        #endregion ISharingProvider Functions

        #region IConnectionCallbacks Functions        
        /// <summary>
        /// Called to signal that the "low level connection" got established but before the client can call operation on the server.
        /// </summary>
        public void OnConnected()
        {
            UpdateConnectedState();
        }

        /// <summary>
        /// Called when the client is connected to the Master Server and ready for matchmaking and other tasks.
        /// </summary>
        public void OnConnectedToMaster()
        {
            LogVerbose("OnConnectedToMaster() (server = {0}) (region = {1})", PhotonNetwork.ServerAddress, PhotonNetwork.CloudRegion);            
            JoinPhotonLobby();
            UpdateConnectedState();
        }

        /// <summary>
        /// Called when the custom authentication failed. Followed by disconnect!
        /// </summary>
        public void OnCustomAuthenticationFailed(string debugMessage)
        {
            LogVerbose("OnCustomAuthenticationFailed() (error = {0})", debugMessage);
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
            ReceivedRooms(null);
            UpdateConnectedState();

            if (_autoReconnect && Application.isPlaying)
            {
                LogVerbose("Attempting to reconnect to room...");
                if (!PhotonNetwork.ReconnectAndRejoin())
                {
                    ConnectToPhoton();
                }
            }
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
            if (!string.IsNullOrEmpty(_pendingRoomName))
            {
                JoinOrCreatePhotonRoom(_pendingRoomName);
            }
        }

        /// <summary>
        /// Called after leaving a lobby.
        /// </summary>
        public void OnLeftLobby()
        {
            LogVerbose($"OnLeftLobby()");
        }

        /// <summary>
        /// Called for any update of the room-listing while in a lobby (InLobby) on the Master Server.
        /// </summary>
        public void OnRoomListUpdate(List<RoomInfo> updates)
        {
            LogVerbose("OnRoomListUpdate() (room count = {0})", PhotonNetwork.CountOfRooms);
            int count = updates?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                RoomInfo info = updates[i];
                if (info.RemovedFromList)
                {
                    _rooms.Remove(info.Name);
                }
                else if (_rooms.ContainsKey(info.Name))
                {
                    _rooms[info.Name].RoomInfo = info;
                }
                else
                {
                    _rooms[info.Name] = new PhotonSharingRoom(info.Name, info);
                }
            }

            ReceivedRooms(_rooms);
            TryAutoJoinRoom();
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
            LogVerbose("OnCreateRoomFailed() (message = {0}) (code = {1})", message, returnCode);
        }

        /// <summary>
        /// Called when the LoadBalancingClient entered a room, no matter if this client created it or simply joined.
        /// </summary>
        public void OnJoinedRoom()
        {
            LogVerbose("OnJoinedRoom() (room name = {0}) (player id = {1})", PhotonNetwork.CurrentRoom.Name, PhotonNetwork.LocalPlayer.ActorNumber);
            UpdateLocalPlayer();
            UpdateOtherPlayers();
            UpdateCurrentRoom();
            UpdateConnectedState();
            ReceivedRoomPropertiesChanged(PhotonNetwork.CurrentRoom.CustomProperties);
        }

        /// <summary>
        /// Called when a previous OpJoinRoom call failed on the server.
        /// </summary>
        public void OnJoinRoomFailed(short returnCode, string message)
        {
            LogVerbose("OnJoinRoomFailed() (message = {0}) (code = {1})", message, returnCode);
            UpdateConnectedState();
        }

        /// <summary>
        /// Called when a previous OpJoinRandom call failed on the server.
        /// </summary>
        public void OnJoinRandomFailed(short returnCode, string message)
        {
            LogVerbose("OnJoinRandomFailed() (message = {0}) (code = {1})", message, returnCode);
            UpdateConnectedState();
        }

        /// <summary>
        /// Called when the local user/client left a room, so the game's logic can clean up it's internal state.
        /// </summary>
        public void OnLeftRoom()
        {
            LogVerbose("OnLeftRoom()");
            UpdateOtherPlayers();
            UpdateLocalPlayer();
            UpdateCurrentRoom();
            UpdateConnectedState();
            JoinPhotonLobby();
        }
        #endregion IMatchMakingCallbacks Functions

        #region IInRoomCallbacks
        /// <summary>
        /// Called after switching to a new MasterClient when the current one leaves.
        /// </summary>
        public void OnMasterClientSwitched(Player newMasterClient)
        {
            LogVerbose("OnMasterClientSwitched() (master player id = {0})", newMasterClient.ActorNumber);
        }

        /// <summary>
        /// Called when a remote player entered the room. This Player is already added to the playerlist.
        /// </summary>
        public void OnPlayerEnteredRoom(Player newPlayer)
        {
            LogVerbose("OnPlayerEnteredRoom() (player id = {0})", newPlayer.ActorNumber);
            AddPlayer(newPlayer);
        }

        /// <summary>
        /// Called when a remote player left the room or became inactive. Check otherPlayer.IsInactive.
        /// </summary>
        public void OnPlayerLeftRoom(Player otherPlayer)
        {
            LogVerbose("OnPlayerLeftRoom() (player id = {0})", otherPlayer.ActorNumber);
            RemovePlayer(otherPlayer.ActorNumber);
        }

        /// <summary>
        /// Called when custom player-properties are changed. Player and the changed properties are passed as object[].
        /// </summary>
        public void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable propertiesThatChanged)
        {
            LogVerbose("OnPlayerPropertiesUpdate() (player id = {0})", targetPlayer.ActorNumber);
            ReceivedPlayerPropertiesChanged(targetPlayer.ActorNumber, propertiesThatChanged);
        }

        /// <summary>
        /// Called when a room's custom properties changed. The propertiesThatChanged contains all that was set via Room.SetCustomProperties.
        /// </summary>
        public void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
            LogVerbose("OnRoomPropertiesUpdate() (room = {0})", PhotonNetwork.CurrentRoom.Name);
            ReceivedRoomPropertiesChanged(propertiesThatChanged);
        }
        #endregion IInRoomCallbacks

        #region IOnEventCallback
        /// <summary>
        /// Called for any incoming events.
        /// </summary>
        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code == _commandMessageCode)
            {
                ReceivedCommandMessage(photonEvent.Sender, (PhotonMessage)photonEvent.CustomData);
            }
            else if (photonEvent.Code == _transformMessageCode)
            {
                ReceivedTransformMessage((SharingServiceTransform)photonEvent.CustomData);
            }
            else if (photonEvent.Code == _playerPoseCode)
            {
                ReceivedPlayerPose(photonEvent.Sender, (Pose)photonEvent.CustomData);
            }
        }
        #endregion

        #region Private Functions
        /// <summary>
        /// Connect and join a Photon.  not connected, connect to Photon. Once connected, try to join a Photon room.
        /// </summary>
        private void ConnectToPhoton()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            LogVerbose("ConnectToPhoton()");
            ValidatePhotonConfiguration();

            if (!_callbacksRegisteredWithPhoton)
            {
                LogVerbose("AddCallbackTarget()");
                PhotonNetwork.AddCallbackTarget(this);
                _callbacksRegisteredWithPhoton = true;
            }

            if (!PhotonNetwork.IsConnected)
            {
                PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime = _appIdRealtime;
                PhotonNetwork.ConnectUsingSettings();
            }
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

            if (error != null)
            {
                Debug.LogError(error);
                AppServices.AppNotificationService.RaiseNotification(error, AppNotificationType.Error);
                throw new ApplicationException(error);
            }
        }

        /// <summary>
        /// Disconnect from the Photon network.
        /// </summary>
        private void DisconnectFromPhoton()
        {
            LogVerbose("DisconnectFromPhoton()");

            PhotonNetwork.Disconnect();
            if (_callbacksRegisteredWithPhoton)
            {
                LogVerbose("RemoveCallbackTarget()");
                PhotonNetwork.RemoveCallbackTarget(this);
                _callbacksRegisteredWithPhoton = false;
            };
        }

        /// <summary>
        /// Join the default Photon lobby. This will exit the current Photon room.
        /// </summary>
        private void JoinPhotonLobby()
        {
            LogVerbose("JoinPhotonLobby()");
            PhotonNetwork.JoinLobby(TypedLobby.Default);
        }

        /// <summary>
        /// Called after connected to Photon. This will attempt to connect to the last joined Photon, or the room will
        /// the most players.
        /// </summary>
        private void TryAutoJoinRoom()
        {
            LogVerbose("TryAutoJoinRoom()");
            
            if (!_autoJoinRoom)
            {
                return;
            }

            if (PhotonNetwork.InRoom)
            {
                return;
            }

            string bestRoom = LastRoom;
            if (string.IsNullOrEmpty(bestRoom))
            {
                int mostUsers = -1;
                foreach (var room in _rooms.Values)
                {
                    RoomInfo info = ((PhotonSharingRoom)room).RoomInfo;
                    if (info.PlayerCount > mostUsers)
                    {
                        bestRoom = info.Name;
                        mostUsers = info.PlayerCount;
                    }
                }
            }

            if (!string.IsNullOrEmpty(bestRoom))
            {
                _autoJoinRoom = false;
                JoinOrCreatePhotonRoom(bestRoom);
            }
        }

        /// <summary>
        /// Create a new Photon room with a unique name.
        /// </summary>
        private void CreatePhotonRoom()
        {
            LogVerbose("CreatePhotonRoom()");

            int roomIndex = 1;
            string roomName = _roomNameFormat;
            bool foundUniqueRoomName = false;

            while (!foundUniqueRoomName)
            {
                roomName = string.Format(_roomNameFormat, roomIndex++);
                foundUniqueRoomName = !_rooms.ContainsKey(roomName);
            }

            JoinOrCreatePhotonRoom(roomName);
        }

        /// <summary>
        /// Attempt to join the given room. If the room doesn't exist it'll be created, and joined.
        /// </summary>
        private void JoinOrCreatePhotonRoom(ISharingServiceRoom room)
        {
            LogVerbose("JoinOrCreatePhotonRoom()");

            PhotonSharingRoom photonSharingRoom = room as PhotonSharingRoom;
            if (photonSharingRoom == null)
            {
                return;
            }

            JoinOrCreatePhotonRoom(photonSharingRoom.Name);
        }

        /// <summary>
        /// Attempt to join the given room. If the room doesn't exist it'll be created, and joined.
        /// </summary>
        private void JoinOrCreatePhotonRoom(string roomName)
        {
            LogVerbose("JoinOrCreatePhotonRoom() (room name = {0})", roomName);

            if (string.IsNullOrEmpty(roomName))
            {
                return;
            }

            if (PhotonNetwork.CurrentRoom != null &&
                PhotonNetwork.CurrentRoom.Name == roomName)
            {
                return;
            }

            LeavePhotonRoom();

            var options = new RoomOptions()
            {
                IsVisible = true,
                IsOpen = true,
                EmptyRoomTtl = 5000,
                PlayerTtl = 5000,
            };

            if (!PhotonNetwork.JoinOrCreateRoom(roomName, options, TypedLobby.Default))
            {
                _pendingRoomName = roomName;
            }
            else
            {
                _pendingRoomName = null;
            }
        }

        /// <summary>
        /// Leave the current Photono room, and join the default lobby after disconnected from the room.
        /// </summary>
        private void LeavePhotonRoom()
        {
            LogVerbose("LeavePhotonRoom()");

            if (PhotonNetwork.CurrentRoom != null)
            {
                PhotonNetwork.LeaveRoom(false);
            }
        }

        /// <summary>
        /// Force the list of rooms to update.
        /// </summary>
        public void UpdatePhotonRooms()
        {
            LogVerbose("UpdatePhotonRooms()");

            PhotonNetwork.GetCustomRoomList(new TypedLobby(null, LobbyType.SqlLobby), "*");
        }

        /// <summary>
        /// Set a Photon custom properties using the given 'setter' action.
        /// </summary>
        /// <param name="propertyNamesAndValues">
        /// A collection of property names and values, alterating between name and value. The first value is a property
        /// name, the next is its value, and soon on.
        /// </param>
        private void SetPhotonCustomProperty(Hashtable properties, Func<Hashtable, Hashtable, WebFlags, bool> setter, params object[] propertyNamesAndValues)
        {
            if (properties == null)
            {
                return;
            }

            lock (_setPropertyBuffer)
            {
                _setPropertyBuffer.Clear();
                _expectedPropertyBuffer.Clear();
                int count = propertyNamesAndValues.Length - 1;
                for (int i = 0; i < count; i += 2)
                {
                    object property = propertyNamesAndValues[i];
                    if (property == null)
                    {
                        continue;
                    }

                    object value = propertyNamesAndValues[i + 1];
                    object oldValue;

                    // expected values don't function correctly for custom types.
                    if (properties.TryGetValue(property, out oldValue) && !_customTypes.Contains(oldValue.GetType()))
                    {
                        LogVerbose("SetPhotonCustomProperty() (name = {0}) (new value = {1}) (old value = {2})", property, value, oldValue);
                        _expectedPropertyBuffer[property] = oldValue;
                    }
                    else
                    {
                        LogVerbose("SetPhotonCustomProperty() (name = {0}) (new value = {1})", property, value);
                    }
                    properties[property] = value;
                    _setPropertyBuffer[property] = value;
                }

                setter(_setPropertyBuffer, _expectedPropertyBuffer, null);
            }
        }

        /// <summary>
        /// Update the _currentRoom field to match whatever the current Photon room is.
        /// </summary>
        private void UpdateCurrentRoom()
        {
            string currentRoomName = PhotonNetwork.CurrentRoom?.Name;
            if (_currentRoom?.Name != currentRoomName)
            {
                if (PhotonNetwork.CurrentRoom == null)
                {
                    _currentRoom = null;
                }
                else
                {
                    _currentRoom = new PhotonSharingRoom(PhotonNetwork.CurrentRoom.Name, PhotonNetwork.CurrentRoom);
                }

                if (_currentRoom != null)
                {
                    LastRoom = _currentRoom.Name;
                }

                CurrentRoomChanged?.Invoke(this, _currentRoom);
            }

            if (_currentRoom != null &&
                !_rooms.ContainsKey(_currentRoom.Name))
            {
                _rooms[CurrentRoom.Name] = _currentRoom;
                RoomsChanged?.Invoke(this, _rooms.Values);
            }
        }

        /// <summary>
        /// Called when a new list of rooms was received from Photon.
        /// </summary>
        private void ReceivedRooms(SortedDictionary<string, PhotonSharingRoom> rooms)
        {
            _rooms = rooms ?? new SortedDictionary<string, PhotonSharingRoom>();
            RoomsChanged?.Invoke(this, _rooms.Values);
        }

        /// <summary>
        /// Calculate the current connection state based on if connected to a Photon room.
        /// </summary>
        private void UpdateConnectedState()
        {
            IsConnected = PhotonNetwork.IsConnected && PhotonNetwork.InRoom;
        }

        /// <summary>
        /// Update the LocalPlayerId property to match Photon's local player anchor number.
        /// </summary>
        private void UpdateLocalPlayer()
        {
            int newLocalPlayerId = PhotonNetwork.LocalPlayer?.ActorNumber ?? -1;
            if (LocalPlayerId != newLocalPlayerId)
            {
                RemovePlayer(LocalPlayerId);
                LocalPlayerId = newLocalPlayerId;
                if (PhotonNetwork.LocalPlayer != null)
                {
                    AddPlayer(PhotonNetwork.LocalPlayer);
                }
            }
        }

        /// <summary>
        /// Update the list of non-local players.
        /// </summary>
        private void UpdateOtherPlayers()
        {
            ClearOtherPlayers();

            if (PhotonNetwork.PlayerListOthers != null)
            {
                foreach (var player in PhotonNetwork.PlayerListOthers)
                {
                    AddPlayer(player);
                }
            }
        }
        
        /// <summary>
        /// Add a player object to the local cache, and raise a 'player added' event.
        /// </summary>
        public void AddPlayer(Player player)
        {
            if (player == null || player.ActorNumber < 0)
            {
                return;
            }

            lock (_players)
            {
                _players[player.ActorNumber] = player;
            }

            PlayerAdded?.Invoke(this, player.ActorNumber);
            ReceivedPlayerPropertiesChanged(player.ActorNumber, player.CustomProperties);
        }

        /// <summary> 
        /// Remove a player object to the local cache, and raise a 'player removed' event.
        /// </summary>
        public void RemovePlayer(int playerId)
        {
            lock (_players)
            {
                _players.Remove(playerId);
            }

            PlayerRemoved?.Invoke(this, playerId);
        }

        /// <summary>
        /// Clear all non-local player objects from the local cache.
        /// </summary>
        public void ClearOtherPlayers()
        {
            List<int> removed = new List<int>(_players.Count);
            lock (_players)
            {
                foreach (var entry in _players)
                {
                    if (!entry.Value.IsLocal)
                    {
                        removed.Add(entry.Key);
                    }
                }

                foreach (var other in removed)
                {
                    _players.Remove(other);
                }
            }

            if (PlayerRemoved != null)
            { 
                foreach (int playerId in removed)
                {
                    PlayerRemoved.Invoke(this, playerId);
                }
            }
        }

        /// <summary>
        /// Handle new player position, and raise a player pose changed event.
        /// </summary>
        private void ReceivedPlayerPose(int playerId, Pose pose)
        {
            PlayerPoseChanged?.Invoke(this, playerId, pose);
        }

        /// <summary>
        /// Handle a set of room property changes, and raise 'property changed' events.
        /// </summary>
        private void ReceivedRoomPropertiesChanged(Hashtable propertiesThatChanged)
        {
            if (PropertyChanged == null)
            {
                return;
            }

            // copy keys as the dictionary can change during callbacks
            var keys = propertiesThatChanged.Keys.ToArray();
            foreach (var key in keys)
            {
                object value = propertiesThatChanged[key];
                LogVerbose("ReceivedRoomPropertiesChanged() (room = {0}) (name = {1}) (new value = {2})", PhotonNetwork.CurrentRoom.Name, key, value);
                PropertyChanged.Invoke(this, (string)key, value);
            }
        }

        /// <summary>
        /// Handle a set of room property changes, and raise 'player property changed' events. 
        /// </summary>
        private void ReceivedPlayerPropertiesChanged(int playerId, Hashtable propertiesThatChanged)
        {
            if (PlayerPropertyChanged == null)
            {
                return;
            }

            // copy keys as the dictionary can change during callbacks
            var keys = propertiesThatChanged.Keys.ToArray();
            foreach (var key in keys)
            {
                object value = propertiesThatChanged[key];
                LogVerbose("ReceivedPlayerPropertiesChanged() (player id = {0}) (name = {1}) (new value = {2})", playerId, key, value);
                PlayerPropertyChanged.Invoke(this, playerId, (string)key, value);
            }
        }

        /// <summary>
        /// Handle receiving a command message, and invoked a "message received" event.
        /// </summary>
        private void ReceivedCommandMessage(int sender, PhotonMessage message)
        {
            message.InitializeSender(sender);
            MessageReceived?.Invoke(this, message);
        }

        /// <summary>
        /// Handle receiving a command message, and invoked a "numeric message received" event.
        /// </summary>
        private void ReceivedTransformMessage(SharingServiceTransform message)
        {
            TransformMessageReceived?.Invoke(
                this,
                message.Target,
                message);
        }

        /// <summary>
        /// Register a new custom event message.
        /// </summary>
        private void RegisterCustomMessageType<T>(
            byte typeCode,
            SerializeMethod serializeMethod,
            DeserializeMethod deserializeMethod)
        {
            Type type = typeof(T);
            var success = PhotonPeer.RegisterType(
                type,
                typeCode,
                serializeMethod,
                deserializeMethod);

            Debug.Assert(success, $"Unable to register type code '{typeCode}' for type '{type}'");
        }

        /// <summary>
        /// Register a new custom event message.
        /// </summary>
        private void RegisterCustomMessageType<T>(
            byte typeCode,
            SerializeStreamMethod serializeMethod,
            DeserializeStreamMethod deserializeMethod)
        {
            Type type = typeof(T);
            _customTypes.Add(type);
            var success = PhotonPeer.RegisterType(
                type,
                typeCode,
                serializeMethod,
                deserializeMethod);

            Debug.Assert(success, $"Unable to register type code '{typeCode}' for type '{type}'");
        }

        /// <summary>
        /// Serialize a pose object
        /// </summary>
        /// <param name="outStream"></param>
        /// <param name="customobject"></param>
        /// <returns></returns>
        private short SerializePose(StreamBuffer outStream, object customobject)
        {
            return _bufferPool.CheckOut((byte[] buffer) =>
            {
                int index = 0;
                Pose pose = (Pose)customobject;
                Protocol.Serialize(pose.position.x, buffer, ref index);
                Protocol.Serialize(pose.position.y, buffer, ref index);
                Protocol.Serialize(pose.position.z, buffer, ref index);
                Protocol.Serialize(pose.rotation.x, buffer, ref index);
                Protocol.Serialize(pose.rotation.y, buffer, ref index);
                Protocol.Serialize(pose.rotation.z, buffer, ref index);
                Protocol.Serialize(pose.rotation.w, buffer, ref index);
                outStream.Write(buffer, 0, index);
                return (short)index;
            }, (3 + 4) * sizeof(float));
        }

        /// <summary>
        /// Deserialize a pose object
        /// </summary>
        private object DeserializePose(StreamBuffer inStream, short length)
        {
            return _bufferPool.CheckOut((byte[] buffer) =>
            {
                inStream.Read(buffer, 0, length);
                Pose pose = new Pose();
                int index = 0;
                Protocol.Deserialize(out pose.position.x, buffer, ref index);
                Protocol.Deserialize(out pose.position.y, buffer, ref index);
                Protocol.Deserialize(out pose.position.z, buffer, ref index);
                Protocol.Deserialize(out pose.rotation.x, buffer, ref index);
                Protocol.Deserialize(out pose.rotation.y, buffer, ref index);
                Protocol.Deserialize(out pose.rotation.z, buffer, ref index);
                Protocol.Deserialize(out pose.rotation.w, buffer, ref index);
                return (object)pose;
            }, length);
        }

        /// <summary>
        /// Serialize a command message event data.
        /// </summary>
        private short SerializeCommandMessage(StreamBuffer outStream, object customType)
        {
            ISharingServiceMessage message = (ISharingServiceMessage)customType;
            short written = 0;
            written += SerializeString(outStream, message.Target);
            written += SerializeString(outStream, message.Command);
            return written;
        }

        /// <summary>
        /// Deserialize a command message event data.
        /// </summary>
        private object DeserializeCommandMessage(StreamBuffer inStream, short length)
        {
            return new PhotonMessage(
                target: DeserializeString(inStream, length),
                command: DeserializeString(inStream, length)
            );
        }

        /// <summary>
        /// Serialize SharingServiceTransform event data.
        /// </summary>
        private short SerializeSharingServiceTransform(StreamBuffer outStream, object customType)
        {
            SharingServiceTransform transform = (SharingServiceTransform)customType;
            short written = 0;
            short transformSize = (3 + 4 + 3) * sizeof(float);

            written += SerializeString(outStream, transform.Target);
            written += _bufferPool.CheckOut((byte[] buffer) =>
            {
                int index = 0;
                Protocol.Serialize(transform.Position.x, buffer, ref index);
                Protocol.Serialize(transform.Position.y, buffer, ref index);
                Protocol.Serialize(transform.Position.z, buffer, ref index);
                Protocol.Serialize(transform.Rotation.x, buffer, ref index);
                Protocol.Serialize(transform.Rotation.y, buffer, ref index);
                Protocol.Serialize(transform.Rotation.z, buffer, ref index);
                Protocol.Serialize(transform.Rotation.w, buffer, ref index);
                Protocol.Serialize(transform.Scale.x, buffer, ref index);
                Protocol.Serialize(transform.Scale.y, buffer, ref index);
                Protocol.Serialize(transform.Scale.z, buffer, ref index);
                outStream.Write(buffer, 0, index);
                return (short)index;
            }, transformSize);
            return written;
        }

        /// <summary>
        /// Deserialize SharingServiceTransform data.
        /// </summary>
        private object DeserializeSharingServiceTransform(StreamBuffer inStream, short length)
        {
            int index = 0;
            SharingServiceTransform transform = new SharingServiceTransform()
            {
                Target = DeserializeString(inStream, length)
            };

            short transformSize = (3 + 4 + 3) * sizeof(float);
            _bufferPool.CheckOut((byte[] buffer) =>
            {
                inStream.Read(buffer, 0, transformSize);
                Protocol.Deserialize(out transform.Position.x, buffer, ref index);
                Protocol.Deserialize(out transform.Position.y, buffer, ref index);
                Protocol.Deserialize(out transform.Position.z, buffer, ref index);
                Protocol.Deserialize(out transform.Rotation.x, buffer, ref index);
                Protocol.Deserialize(out transform.Rotation.y, buffer, ref index);
                Protocol.Deserialize(out transform.Rotation.z, buffer, ref index);
                Protocol.Deserialize(out transform.Rotation.w, buffer, ref index);
                Protocol.Deserialize(out transform.Scale.x, buffer, ref index);
                Protocol.Deserialize(out transform.Scale.y, buffer, ref index);
                Protocol.Deserialize(out transform.Scale.z, buffer, ref index);
            }, transformSize);

            return transform;
        }

        /// <summary>
        /// Serialize a string to Photon's StreamBuffer
        /// </summary>
        private short SerializeString(StreamBuffer outStream, string value)
        {
            short stringByteCount = 0;
            if (!string.IsNullOrEmpty(value))
            {
                stringByteCount = (short)Encoding.UTF8.GetByteCount(value);
            }

            return _bufferPool.CheckOut((byte[] buffer) =>
            {
                int index = 0;
                Protocol.Serialize(stringByteCount, buffer, ref index);

                if (stringByteCount > 0)
                {
                    Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, index);
                    index += stringByteCount;
                }

                outStream.Write(buffer, 0, index);
                return (short)(stringByteCount + sizeof(short));
            }, stringByteCount + sizeof(short));
        }

        /// <summary>
        /// Deserialize a string from Photon's StreamBuffer
        /// </summary>
        private string DeserializeString(StreamBuffer inStream, short maxLength)
        {
            string result = null;
            _bufferPool.CheckOut((byte[] buffer) =>
            {
                short stringByteCount = 0;

                // Read the byte size of the string
                int offset = 0;
                int read = inStream.Read(buffer, offset, sizeof(short));
                Protocol.Deserialize(out stringByteCount, buffer, ref offset);

                // Read the string
                if (stringByteCount > 0)
                {
                    read += inStream.Read(buffer, offset, stringByteCount);
                    result = Encoding.UTF8.GetString(buffer, offset, stringByteCount);
                }

                return read;
            }, maxLength);

            return result;
        }

        /// <summary>
        /// Log a message if verbose logging is enabled.
        /// </summary>
        private void LogVerbose(string message)
        {
            if (_verboseLogging)
            {
                Debug.LogFormat(LogType.Log, LogOption.None, null, $"[{nameof(PhotonSharingProvider)}] {message}");
            }
        }

        /// <summary>
        /// Log a message if verbose logging is enabled. 
        /// </summary>
        private void LogVerbose(string messageFormat, params object[] args)
        {
            if (_verboseLogging)
            {
                // expand arrays and hashtables to strings
                for (int i = 0; i < args.Length; i++)
                {
                    args[i] = ToString(args[i]);
                }
                Debug.LogFormat(LogType.Log, LogOption.None, null, $"[{nameof(PhotonSharingProvider)}] {messageFormat}", args);
            }
        }

        /// <summary>
        /// Convert a value to string. If the value is a hashtable or an array, each entry will be converted to a string.
        /// </summary>
        private static string ToString(object value)
        {
            if (value is Hashtable)
            {
                return ToString((Hashtable)value, maxEntries: 10);
            }
            else if (value is Array)
            {
                return ToString((Array)value, maxEntries: 10);
            }
            else
            {
                return value?.ToString() ?? "NULL";
            }
        }

        /// <summary>
        /// Expand a hashtable to single string. Adding only a max number of entries the string.
        /// </summary>
        private static string ToString(Hashtable table, int maxEntries)
        {
            int count = Math.Min(maxEntries, table.Count);
            int currnet = 0;
            StringBuilder sb = new StringBuilder();

            sb.Append("[");
            foreach (var entry in table)
            {
                if (currnet >= count)
                {
                    break;
                }

                if (currnet > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(entry.Key.ToString());
                sb.Append(" = ");
                sb.Append(ToString(entry.Value));

                currnet++;
            }

            if (count < table.Count)
            {
                sb.Append("...");
            }
            sb.Append("]");

            return sb.ToString();
        }

        /// <summary>
        /// Expand an array to single string. Adding only a max number of entries the string. 
        /// </summary>
        private static string ToString(Array array, int maxEntries)
        {
            int count = Math.Min(maxEntries, array.Length);
            StringBuilder sb = new StringBuilder();

            sb.Append("[");
            for (int j = 0; j < count; j++)
            {
                object value = array.GetValue(j);
                if (j > 0)
                {
                    sb.Append(", ");
                }
                sb.Append(value.ToString());
            }

            if (count < array.Length)
            {
                sb.Append("...");
            }
            sb.Append("]");

            return sb.ToString();
        }
        #endregion Private Methods

        #region Private Class
        /// <summary>
        /// A pool of byte arrays that can be used to deserialize Photon event data.
        /// </summary>
        private class BufferPool
        {
            private int _maxPoolSize = 10;
            private AutoResetEvent _waitForBuffer = new AutoResetEvent(false);
            private byte[][] _pool;

            public BufferPool()
            {
                _pool = new byte[_maxPoolSize][];
                for (int i = 0; i < _maxPoolSize; i++)
                {
                    _pool[i] = new byte[BufferSize];
                }
            }

            /// <summary>
            /// Get the max buffer size.
            /// </summary>
            public short BufferSize { get; } = sizeof(int) * 256;

            /// <summary>
            /// Check out a byte array. This will block until a buffer becomes available.
            /// Once checked out, action will be invoked. After action completed, buffer
            /// is automatically checked in.
            /// </summary>
            public void CheckOut(Action<byte[]> action, short bufferLength = 0)
            {
                if (bufferLength < 0)
                {
                    Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "Requested a negative buffer size. A possible numeric overflow.");
                    return;
                }

                if (bufferLength > BufferSize)
                {
                    Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "Buffer request is too large to use the pool.");
                    action(new byte[bufferLength]);
                    return;
                }

                byte[] buffer = CheckOut(bufferLength);
                try
                {
                    action(buffer);
                }
                finally
                {
                    CheckIn(buffer);
                }
            }

            /// <summary>
            /// Check out a byte array. This will block until a buffer becomes available.
            /// Once checked out, action will be invoked. After action completed, buffer
            /// is automatically checked in.
            /// </summary>
            public T CheckOut<T>(Func<byte[], T> action, int bufferLength = -1)
            {
                if (bufferLength > BufferSize)
                {
                    Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "Buffer request is too large to use the pool.");
                    return action(new byte[bufferLength]);
                }

                byte[] buffer = CheckOut(bufferLength);
                try
                {
                    return action(buffer);
                }
                finally
                {
                    CheckIn(buffer);
                }
            }

            /// <summary>
            /// Check out a byte array. This will block until a buffer becomes available.
            /// </summary>
            private byte[] CheckOut(int bufferLength = -1)
            {
                if (bufferLength > BufferSize)
                {
                    throw new IndexOutOfRangeException("The given buffer size is too big.");
                }

                byte[] result = null;
                while (true)
                {
                    lock (_pool)
                    {
                        for (int i = 0; i < _maxPoolSize; i++)
                        {
                            if (_pool[i] != null)
                            {
                                result = _pool[i];
                                _pool[i] = null;
                                break;
                            }
                        }
                    }

                    if (result != null)
                    {
                        break;
                    }

                    _waitForBuffer.WaitOne();
                }

                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = 0;
                }

                return result;
            }

            /// <summary>
            /// Check in a byte array.
            /// </summary>
            private void CheckIn(byte[] buffer)
            {
                if (buffer == null || buffer.Length != BufferSize)
                {
                    Debug.LogWarning("Trying to check-in a buffer that is too small.");
                    buffer = new byte[BufferSize];
                }

                bool added = false;
                lock (_pool)
                {
                    for (int i = 0; i < _maxPoolSize; i++)
                    {
                        if (_pool[i] == null)
                        {
                            _pool[i] = buffer;
                            added = true;
                            break;
                        }
                    }
                }

                if (added)
                {
                    _waitForBuffer.Set();
                }
            }
        }
        #endregion Private Class
    }
}
#endif // PHOTON_INSTALLED
