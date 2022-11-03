// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if PHOTON_INSTALLED
using Photon.Pun;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    /// <summary>
    /// Internal class used for implementation of a sharing protocol.
    /// </summary>
    public class PhotonProvider : ISharingProvider, IDisposable
    {
        private LogHelper<PhotonProvider> _logger = new LogHelper<PhotonProvider>();
        private bool _isDisposed = false;
        private CancellationTokenSource _providerCancellation = new CancellationTokenSource();
        private ConcurrentQueue<Action<CancellationToken>> _queuedWork = new ConcurrentQueue<Action<CancellationToken>>();
        private string _statusMessage = null;
        private OfflineLocation _offlineLocation = null;
        private PhotonRoomState _activeState = PhotonRoomState.Disconnected;
        private PhotonMessageTypeRegistrar _activeTypeRegistrar = null;
        private PhotonConnectionManager _activeConnection = null;
        private PhotonComponents _activeComponents = null;
        private PhotonSharingRoom _activeRoom = null;
        private PhotonLocation _activeLocation = null;
        private PhotonProperties _activeProperties = null;
        private PhotonParticipants _activeParticipants = null;
        private PhotonMessages _activeMessages = null;
        private PhotonAvatars _activeAvatars = null;
        private PhotonOwnership _activeOwnership = null;
        private SharingServiceProfile _settings = null;
        private SharingServiceAvatarSettings _avatarSettings = SharingServiceAvatarSettings.Default;
        private SharingServiceAudioSettings _audioSettings = new SharingServiceAudioSettings()
        {
            MuteMicrophone = false,
            MicrophoneAdjustment = Application.isEditor ? 100.0f : 90.0f,
            PlaybackFalloffDistance = 1.0f
        };

        #region Constructors
        public PhotonProvider(SharingServiceProfile settings, ISharingServiceProtocol protocol, GameObject root)
        {
            _settings = settings ?? throw new ArgumentNullException("Settings can't be null");
            _logger.Verbose = settings.VerboseLogging ? LogHelperState.Always : LogHelperState.Default;
            _activeTypeRegistrar = new PhotonMessageTypeRegistrar(settings, protocol);
            _activeConnection = new PhotonConnectionManager(settings);
            _activeConnection.CurrentRoomChanged += OnConnectionRoomChanged;
            _activeConnection.RoomsChanged += OnConnectionRoomsChanged;
            _activeComponents = PhotonComponents.Create(settings, _audioSettings, root);
            _offlineLocation = OfflineLocation.Create();
            _offlineLocation.Initialize();

            AddLifetimeEventHandlers();

            // Register static scene objects with Photon 
            UnityEngine.Object.FindObjectsOfType<SharingObject>().RegisterWithPhoton();
        }
        #endregion Constructors

        #region IDispose
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            if (_activeConnection != null)
            {
                _activeConnection.Dispose();
                _activeConnection.CurrentRoomChanged -= OnConnectionRoomChanged;
                _activeConnection.RoomsChanged -= OnConnectionRoomsChanged;
                _activeConnection = null;
            }

            if (_providerCancellation != null &&
                !_providerCancellation.IsCancellationRequested)
            {
                _providerCancellation.Cancel();
            }

            while (!_queuedWork.IsEmpty)
            {
                _queuedWork.TryDequeue(out var work);
            }

            DisposeSessionComponents();

            if (_providerCancellation != null)
            {
                _providerCancellation.Dispose();
                _providerCancellation = null;
            }

            RemoveLifetimeEventHandlers();
        }
        #endregion IDispose

        #region Public Properties
        /// <summary>
        /// Get the latest address to use when not connected to a Sharing Session
        /// </summary>
        public SharingServiceAddress OfflineAddress => _offlineLocation.PrimaryAddress;

        /// <summary>
        /// Get if the offline address has been located.
        /// </summary>
        public bool IsOfflineAddressLocated => OfflineAddress != null && OfflineAddress.IsLocated;
        #endregion Public Properties

        #region ISharingProvider Events
        /// <summary>
        /// Event fired when client is connected to a session/room
        /// </summary>
        public event Action<ISharingProvider> Connected;

        /// <summary>
        /// Event raised when a new client is connecting to a session/room
        /// </summary>
        public event Action<ISharingProvider> Connecting;

        /// <summary>
        /// Event fired when client is disconnected from a session/room
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
        /// Event fired when an invitation is received.
        /// </summary>
        public event Action<ISharingProvider, ISharingServiceRoom> RoomInviteReceived;

        /// <summary>
        /// Event fired when a new player has been added.
        /// </summary>
        public event Action<ISharingProvider, SharingServicePlayerData> PlayerAdded;

        /// <summary>
        /// Event fired when a player has been removed.
        /// </summary>
        public event Action<ISharingProvider, SharingServicePlayerData> PlayerRemoved;

        /// <summary>
        /// Event fired when a player's property changes.
        /// </summary>
        public event Action<ISharingProvider, string, string, object> PlayerPropertyChanged;

        /// <summary>
        /// Event fired when a player's name changes.
        /// </summary>
        public event Action<ISharingProvider, string, string> PlayerDisplayNameChanged;

        /// <summary>
        /// Event raised when the provider's status message changes.
        /// </summary>
        public event Action<ISharingProvider, string> StatusMessageChanged;

        /// <summary>
        /// A specialized message when response from a ping request
        /// </summary>
        public event Action<ISharingProvider, string, SharingServicePingResponse> PingReturned;

        /// <summary>
        /// Event fired when a user's address has changed.
        /// </summary>
        public event Action<ISharingProvider, SharingServiceAddress> PrimaryAddressChanged;

        /// <summary>
        /// Event fired when the users are the current primary address have changed.
        /// </summary>
        public event Action<ISharingProvider> PrimaryAddressUsersChanged;

        /// <summary>
        /// Event fired when the known local addresses have changed.
        /// </summary>
        public event Action<ISharingProvider, IReadOnlyList<SharingServiceAddress>> LocalAddressesChanged;

        /// <summary>
        /// Event fired when audio settings changed.
        /// </summary>
        public event Action<ISharingProvider, SharingServiceAudioSettings> AudioSettingsChanged;

        /// <summary>
        /// Event fired when avatar settings changed.
        /// </summary>
        public event Action<ISharingProvider, SharingServiceAvatarSettings> AvatarSettingsChanged;
        #endregion ISharingProvider Events

        #region ISharingProvider Properties
        /// <summary>
        /// Get the container for all sharing related game objects, such as new avatars. Avatar positioning will be relative to this container.
        /// </summary>
        public GameObject SharingRoot => _activeComponents.Root;

        /// <summary>
        /// True if connected to sharing service and logged in. But not necessarily in a session/room
        /// </summary>
        public bool IsLoggedIn => _activeConnection.IsLoggedIn;

        /// <summary>
        /// True when client is connected to a session/room
        /// </summary>
        public bool IsConnected => _activeState == PhotonRoomState.Connected;

        /// <summary>
        /// Get if the provider is connecting to a session/room
        /// </summary>
        public bool IsConnecting => _activeState == PhotonRoomState.Connecting;

        /// <summary>
        /// The player id of the local player.
        /// </summary>
        public string LocalPlayerId => _activeParticipants?.LocalParticipant.Identifier ?? InvalidPlayerId;

        /// <summary>
        /// Get an invalid player id
        /// </summary>
        public string InvalidPlayerId => null;

        /// <summary>
        /// The list of current room.
        /// </summary>
        public IReadOnlyCollection<ISharingServiceRoom> Rooms => _activeConnection.Rooms;

        /// <summary>
        /// Get the current room.
        /// </summary>
        public ISharingServiceRoom CurrentRoom => _activeRoom;

        /// <summary>
        /// Get if this provider supports private sharing sessions/rooms.
        /// </summary>
        public bool HasPrivateRooms => false;

        /// <summary>
        /// The user's current address.
        /// </summary>
        public SharingServiceAddress PrimaryAddress =>
            _activeLocation == null ? _offlineLocation.PrimaryAddress : _activeLocation.PrimaryAddress;

        /// <summary>
        /// All the known local addresses that are available to make a primary.
        /// </summary>
        public IReadOnlyList<SharingServiceAddress> LocalAddresses =>
            _activeLocation == null ? _offlineLocation.LocalAddresses : _activeLocation.LocalAddresses;

        /// <summary>
        /// Get the provider's status message
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;

            private set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    StatusMessageChanged?.Invoke(this, value);
                }
            }
        }

        /// <summary>
        /// Get or set the providers audio settings.
        /// </summary>
        public SharingServiceAudioSettings AudioSettings
        {
            get
            {
                if (_activeComponents.VoiceRecorder != null)
                {
                    _audioSettings.MuteMicrophone = !_activeComponents.VoiceRecorder.TransmitEnabled;
                }

                if (_activeComponents.VolumeControl != null)
                {
                    _audioSettings.MicrophoneAdjustment = _activeComponents.VolumeControl.Volume;
                }

                return _audioSettings;
            }
            set
            {
                _audioSettings = value;
                if (_activeComponents.VoiceRecorder != null)
                {
                    _activeComponents.VoiceRecorder.TransmitEnabled = !_audioSettings.MuteMicrophone;
                }

                if (_activeComponents.VolumeControl != null)
                {
                    _activeComponents.VolumeControl.Volume = _audioSettings.MicrophoneAdjustment;
                }

                AudioSettingsChanged?.Invoke(this, _audioSettings);
            }
        }

        /// <summary>
        /// Get the provider's audio capabilities.
        /// </summary>
        public SharingServiceAudioCapabilities AudioCapabilities { get; } = new SharingServiceAudioCapabilities()
        {
            MicrophoneAdjustment = MicrophoneAdjustmentType.ScaleFactor,
            MinMicrophoneAdjustment = 0f,
            MaxMicrophoneAdjustment = 100f,
            MicrophoneAdjustmentNumberFormat = "{0:0.00}%",
            SupportsVoiceCalibration = true
        };

        /// <summary>
        /// Get or set the provider's avatar settings
        /// </summary>
        public SharingServiceAvatarSettings AvatarSettings
        {
            get => _avatarSettings;

            set
            {
                _avatarSettings = value;
                AvatarSettingsChanged?.Invoke(this, _avatarSettings);
            }
        }

        /// <summary>
        /// Get if offline spawning of objects is supported by this provider.
        /// </summary>
        public bool OfflineSpawningSupported => false;

        /// <summary>
        /// Get if the data parameters used doing spawning should be wrapped into ProtocolMessages.
        /// </summary>
        public bool WrapSpawningData => true;
        #endregion ISharingProvider Properties

        #region ISharingProvider Functions
        /// <summary>
        /// Connect and join the sharing service's lobby. This allows the client to see the available sharing rooms.
        /// </summary>
        public Task Login()
        {
            return EnqueueActionWithTask((ct) => _activeConnection.Login());
        }

        /// <summary>
        /// Disconnected from sharing service. This leave the lobby and the client can no longer see the available sharing rooms.
        /// </summary>
        public void Logout()
        {
            EnqueueActionWithTask((ct) => _activeConnection.Logout());
        }

        /// <summary>
        /// Update provider every frame.
        /// </summary>
        public void Update()
        {
            while (!_queuedWork.IsEmpty && !_isDisposed)
            {
                if (_queuedWork.TryDequeue(out var work))
                {
                    if (!_providerCancellation.IsCancellationRequested)
                    {
                        work.Invoke(_providerCancellation.Token);
                    }
                }
            }

            _activeAvatars?.Update();
        }

        /// <summary>
        /// LateUpdate provider every frame.
        /// </summary>
        public void LateUpdate()
        {
            _activeAvatars?.LateUpdate();
        }

        /// <summary>
        /// Create and join a new sharing room.
        /// </summary>
        public Task CreateAndJoinRoom()
        {
            return EnqueueActionWithTask((ct) =>
            {
                return ConnectToSessionAsync(() => _activeConnection.CreateRoom(), ct);
            });
        }

        /// <summary>
        /// Join the given room.
        /// </summary>
        public Task JoinRoom(ISharingServiceRoom room)
        {
            return EnqueueActionWithTask((ct) =>
            {
                return ConnectToSessionAsync(() => _activeConnection.JoinOrCreateRoom(room), ct);
            });
        }

        /// <summary>
        /// Join the given room by string id
        /// </summary>
        public Task JoinRoom(string id)
        {
            return EnqueueActionWithTask((ct) =>
            {
                return ConnectToSessionAsync(() => _activeConnection.JoinOrCreateRoom(id), ct);
            });
        }

        /// <summary>
        /// Create and join a new private sharing room. Only the given list of players can join the room.
        /// </summary>
        public Task CreateAndJoinRoom(IEnumerable<SharingServicePlayerData> inviteList)
        {
            _ = AppServices.AppNotificationService.ShowDialog(new DialogOptions()
            {
                Title = "Not Supported",
                Message = "Private rooms are currently not supported.",
                Buttons = AppDialog.AppDialogButtons.Ok,
                Location = AppDialog.AppDialogLocation.Menu
            });

            return Task.CompletedTask;
        }

        /// <summary>
        /// Decline a room/session invitation.
        /// </summary>
        public void DeclineRoom(ISharingServiceRoom room)
        {
            _ = AppServices.AppNotificationService.ShowDialog(new DialogOptions()
            {
                Title = "Not Supported",
                Message = "Unable to decline invite. Private room invites are currently not supported.",
                Buttons = AppDialog.AppDialogButtons.Ok,
                Location = AppDialog.AppDialogLocation.Menu
            });
        }

        /// <summary>
        /// If the current user is part of a private room, invite the given player to this room.
        /// </summary>
        public Task<bool> InviteToRoom(SharingServicePlayerData player)
        {
            _ = AppServices.AppNotificationService.ShowDialog(new DialogOptions()
            {
                Title = "Not Supported",
                Message = "Unable to invite player. Private rooms are currently not supported.",
                Buttons = AppDialog.AppDialogButtons.Ok,
                Location = AppDialog.AppDialogLocation.Menu
            });

            return Task.FromResult(false);
        }

        /// <summary>
        /// Leave the currently joined sharing room, and join the default lobby.
        /// </summary>
        public Task LeaveRoom()
        {
            return EnqueueActionWithTask(async (ct) =>
            {
                if (IsConnected)
                {
                    DisposeSessionComponents();
                    await _activeConnection.LeaveRoom();
                    SetActiveState(PhotonRoomState.Disconnected);
                }
            });
        }

        /// <summary>
        /// Force the list of rooms to update now.
        /// </summary>
        public Task<IReadOnlyCollection<ISharingServiceRoom>> UpdateRooms()
        {
            return Task.FromResult(Rooms);
        }

        /// <summary>
        /// Send a specialized message that contains only a transform. 
        /// </summary>
        public void SendTransformMessage(string gameObject, SharingServiceTransform transform)
        {
            transform.Target = gameObject;
            _activeMessages?.SendMessage(new ProtocolMessage()
            {
               type = ProtocolMessageType.SharingServiceTransform,
               data = new ProtocolMessageData()
               {
                   type = ProtocolMessageDataType.SharingServiceTransform,
                   value = transform
               }
            });
        }

        /// <summary>
        /// Sends a message to all other clients
        /// </summary>
        /// <param name="message">Message to send</param>
        public void SendMessage(ISharingServiceMessage message)
        {
            _activeMessages?.SendMessage(new ProtocolMessage()
            {
                type = ProtocolMessageType.SharingServiceMessage,
                data = new ProtocolMessageData()
                {
                    type = ProtocolMessageDataType.SharingServiceMessage,
                    value = message
                }
            });
        }

        /// <summary>
        /// Set a shared property on the server. Setting to null will clear the property from the server.
        /// </summary>
        public void SetProperty(string name, object value)
        {
            _activeProperties?.SetSessionProperty(name, value);
        }

        /// <summary>
        /// Set a shared properties on the server. Setting to a value to null will clear the property from the server.
        /// </summary>
        public void SetProperties(params object[] propertyNamesAndValues) 
        {
            _activeProperties?.SetSessionProperties(propertyNamesAndValues);
        }

        /// <summary>
        /// Try to get a sharing service's property value.
        /// </summary>
        /// <returns>True if a non-null property value was found.</returns>
        public bool TryGetProperty(string name, out object value)
        {
            value = null;
            return _activeProperties?.TryGetSessionProperty(name, out value) ?? false;
        }

        /// <summary>
        /// Does the sharing service have the current property name.
        /// </summary>
        public bool HasProperty(string name)
        {
            return _activeProperties?.HasSessionProperty(name) ?? false; 
        }

        /// <summary>
        /// Clear all properties with the given prefix.
        /// </summary>
        public void ClearPropertiesStartingWith(string prefix)
        {
            _activeProperties?.ClearSessionPropertiesStartingWith(prefix);
        }

        /// <summary>
        /// Try to set a player's property value.
        /// </summary>
        public void SetPlayerProperty(string playerId, string name, object value)
        {
            _activeProperties?.SetSessionParticipantProperty(playerId, name, value);
        }

        /// <summary>
        /// Try to get a player's property value.
        /// </summary>
        /// <returns>True if a non-null property value was found.</returns>
        public bool TryGetPlayerProperty(string playerId, string name, out object value)
        {
            value = null;
            return _activeProperties?.TryGetSessionParticipantProperty(playerId, name, out value) ?? false;
        }

        // <summary>
        /// Does this provider have the current property for the player.
        /// </summary>
        public bool HasPlayerProperty(string playerId, string name)
        {
            return _activeProperties?.HasSessionParticipantProperty(playerId, name) ?? false;
        }

        /// <summary>
        /// Send a ping request.
        /// </summary>
        /// <remarks>
        /// If targetRecipientId is not null, will send it to that person only
        /// </remarks>
        public SharingServicePingRequest? SendPing(byte id, string targetRecipientId = null)
        {
            SharingServicePingRequest? pingRequest = null;

            var messages = _activeMessages;
            if (messages != null)
            {
                pingRequest = SharingServicePingRequest.Create(id);
                var result = messages.SendMessage(targetRecipientId, new ProtocolMessage()
                {
                    type = ProtocolMessageType.SharingServicePingRequest,
                    data = new ProtocolMessageData()
                    {
                        type = ProtocolMessageDataType.SharingServicePingRequest,
                        value = pingRequest
                    }
                });

                if (!result)
                {
                    pingRequest = null;
                }
            }

            return pingRequest;
        }

        /// <summary>
        /// Return a ping request.
        /// </summary>
        public void ReturnPing(SharingServicePingRequest pingRequest, string targetPlayerId)
        {
            _activeMessages?.SendMessage(targetPlayerId, new ProtocolMessage()
            {
                type = ProtocolMessageType.SharingServicePingResponse,
                data = new ProtocolMessageData()
                {
                    type = ProtocolMessageDataType.SharingServicePingResponse,
                    value = SharingServicePingResponse.Create(pingRequest.Id)
                }
            });
        }

        /// <summary>
        /// Set primary address to the current sharing root position
        /// </summary>
        public async Task CreateAddress(CancellationToken ct = default)
        {
            if (SharingRoot == null)
            {
                _logger.LogWarning("Ignoring create address request, there is no sharing root.");
                return;
            }

            // Always update the offline address, to use once disconnected from a session
            _ = _offlineLocation.CreateAddress(SharingRoot.transform, ct);

            if (_activeLocation != null)
            {
                _logger.LogVerbose("Creating new address at '{0}'", SharingRoot.name);
                var clearMessage = SetStatusMessage("Creating new sharing address");
                try
                {
                    await _activeLocation.CreateAddress(SharingRoot.transform, ct);
                    _logger.LogVerbose("Created new address at '{0}'", SharingRoot.name);
                }
                finally
                {
                    clearMessage();
                }
            }
        }

        /// <summary>
        /// Set primary address to the given address.
        /// </summary>
        public Task SetAddress(SharingServiceAddress address, CancellationToken ct = default)
        {
            if (_activeLocation == null)
            {
                return Task.CompletedTask;
            }
            else
            {
                return _activeLocation.SetAddress(address, ct);
            }
        }

        /// <summary>
        /// Get if the participant is co-located with the local user.
        /// </summary>
        public bool Colocated(string participantId)
        {
            return _activeLocation != null && _activeLocation.Colocated(participantId);
        }

        /// <summary>
        /// Start finding all local known addresses. Also if primary address is null, this will default primary to first known address.
        /// </summary>
        public Task FindAddresses(CancellationToken ct = default)
        {
            if (_activeLocation == null)
            {
                return Task.CompletedTask;
            }
            else
            {
                return _activeLocation.FindAddresses(ct);
            }
        }

        /// <summary>
        /// Find sharing service players by a name. These player might not be in the current session.
        /// </summary>
        public Task<IList<SharingServicePlayerData>> FindPlayers(string prefix, CancellationToken ct = default)
        {
            _ = AppServices.AppNotificationService.ShowDialog(new DialogOptions()
            {
                Title = "Not Supported",
                Message = "Unable to search for players. Private rooms are currently not supported.",
                Buttons = AppDialog.AppDialogButtons.Ok,
                Location = AppDialog.AppDialogLocation.Menu
            });

            return Task.FromResult<IList<SharingServicePlayerData>>(new List<SharingServicePlayerData>());
        }

        /// <summary>
        /// Spawn a network object that is shared across all clients
        /// </summary>
        public Task<GameObject> SpawnTarget(GameObject original, object[] data)
        {
            return EnqueueActionWithTask((ct) =>
            {
                if (IsConnected)
                {
                    return SpawnOnlineTarget(original, data);
                }
                else
                {
                    return Task.FromException<GameObject>(new Exception(
                        "Can't spawn game object while not connected to service"));
                }
            });
        }

        /// <summary>
        /// Despawn a network object that is shared across all clients
        /// </summary>
        public Task DespawnTarget(GameObject gameObject)
        {
            return EnqueueActionWithTask((ct) =>
            {
                if (IsConnected)
                {
                    return DespawnOnlineTarget(gameObject);
                }
                else
                {
                    return Task.FromException(new Exception(
                        "Can't despawn game object while not connected to service"));
                }
            });
        }

        /// <summary>
        /// Initialize a network object with sharing components needed for the selected provider
        /// </summary>
        /// <param name="gameObject"></param>
        public void EnsureNetworkObjectComponents(GameObject gameObject)
        {
            if (gameObject != null && Application.isPlaying)
            {
                gameObject.EnsureComponent<PhotonViewToSharingObject>();
            }
        }

        /// <summary>
        /// Calibrate the sharing provider's microphone so to better detect voices, and eliminate background noise.
        /// </summary>
        public async Task<bool> CalibrateVoiceDetection()
        {
            _logger.LogVerbose("Calibrating voice dector...");

            var success = false;
            var volumeControl = _activeComponents?.VolumeControl;

            if (volumeControl != null)
            {
                success = await volumeControl.Calibrate();
            }

            return success;
        }
        #endregion ISharingProvider Functions

        #region Private Functions
        private void EnqueueAction(Action action)
        {
            if (action != null)
            {
                EnqueueAction((CancellationToken token) => action());
            }
        }

        private void EnqueueAction(Action<CancellationToken> action)
        {
            if (action != null && !_isDisposed)
            {
                _queuedWork.Enqueue(action);
            }
        }

        private Task EnqueueActionWithTask(Func<CancellationToken, Task> action)
        {
            if (action != null && !_isDisposed)
            {
                var taskSource = new TaskCompletionSource<object>();
                _queuedWork.Enqueue(async (CancellationToken ct) =>
                {
                    try
                    {
                        await action(ct);
                        taskSource.SetResult(null);
                    }
                    catch (Exception ex)
                    {
                        taskSource.TrySetException(ex);
                    }
                });
                return taskSource.Task;
            }
            else
            {
                return Task.CompletedTask;
            }
        }

        private Task<T> EnqueueActionWithTask<T>(Func<CancellationToken, Task<T>> action)
        {
            if (action != null && !_isDisposed)
            {
                var taskSource = new TaskCompletionSource<T>();
                _queuedWork.Enqueue(async (CancellationToken ct) =>
                {
                    try
                    {
                        taskSource.TrySetResult(await action(ct));
                    }
                    catch (Exception ex)
                    {
                        taskSource.TrySetException(ex);
                    }
                });
                return taskSource.Task;
            }
            else
            {
                return Task.FromResult<T>(default);
            }
        }

        private async Task ConnectToSessionAsync(CreateRoomFactory factory, CancellationToken ct)
        {
            if (_activeState != PhotonRoomState.Disconnected)
            {
                LogError("Unable to join a new session. The application is still connected or connecting to another session.");
                return;
            }

            SetActiveState(PhotonRoomState.Connecting);
            PhotonSharingRoom room = null;
            PhotonLocation location = null;
            PhotonProperties properties = null;
            PhotonParticipants participants = null;
            PhotonMessages messages = null;
            PhotonAvatars avatars = null;
            PhotonOwnership ownership = null;
            bool error = false;

            try
            {
                LogVerbose("Ensure logged in before connecting to session");
                StatusMessage = "Logging into sharing services";
                await _activeConnection.Login();
                ct.ThrowIfCancellationRequested();

                LogVerbose("Ensuring a session can be created...");
                EnsureCanCreateSession();

                //LogVerbose("Creating session avatar manager...");

                LogVerbose("Creating CollaborationSession from factory...");
                StatusMessage = "Joining sharing session";
                room = await factory.Invoke();

                LogVerbose("Creating session participants...");
                participants = PhotonParticipants.CreateFromRoom(_settings, room);

                LogVerbose("Creating session properties cache...");
                StatusMessage = "Loading room state";
                properties = PhotonProperties.CreateFromRoom(_settings, room, participants, _activeTypeRegistrar.Protocol);

                LogVerbose("Waitting for offline anchor to load...");
                StatusMessage = "Creating sharing stage"; 
                await _offlineLocation.Initialize();
                LogVerbose("Creating searching for a nearby users or creating a new sharing address...");
                location = await PhotonLocation.CreateFromRoom(_settings, room, properties);
                await location.TrySetDefaultAddress(allowPrompt: true, IsOfflineAddressLocated ? OfflineAddress.Transform : null, ct);
                ct.ThrowIfCancellationRequested();

                LogVerbose("Creating message transport for...");
                StatusMessage = "Creating message transport";
                messages = PhotonMessages.CreateFromParticipants(_settings, participants);

                LogVerbose("Creating ownership helper...");
                ownership = PhotonOwnership.Create(_settings);

                LogVerbose("Initialize session avatar manager...");
                avatars = PhotonAvatars.CreateFromParticipants(_settings, _activeComponents, participants, properties, SharingRoot?.transform);

                //LogVerbose("Creating call media transport...");

                EnsureConnectedRoom(room);
                LogInformation("Active session is {0} ({1})", room.Inner.Name, room.Inner.MasterClientId);

                _activeLocation = location;
                _activeProperties = properties;
                _activeParticipants = participants;
                _activeMessages = messages;
                _activeAvatars = avatars;
                _activeOwnership = ownership;
                _activeRoom = room;
                _activeTypeRegistrar.InRoom = true;
                AddSessionEventHandlers();
            }
            catch (Exception ex)
            {
                LogError("Failed to create session. Exception: {0}", ex);
                error = true;
            }

            // clear status message
            StatusMessage = null;

            if (error)
            {
                AppServices.AppNotificationService.RaiseNotification(
                    "Failed to join Photon session.",
                    AppNotificationType.Error);

                if (location != null)
                {
                    location.Dispose();
                    location = null;
                }

                if (properties != null)
                {
                    properties.Dispose();
                    properties = null;
                }

                if (participants != null)
                {
                    participants.Dispose();
                    participants = null;
                }

                if (messages != null)
                {
                    messages.Dispose();
                    messages = null;
                }

                if (avatars != null)
                {
                    avatars.Dispose();
                    avatars = null;
                }

                if (ownership != null)
                {
                    ownership.Dispose();
                    ownership = null;
                }

                if (_activeConnection.CurrentRoom == room)
                {
                    await _activeConnection.LeaveRoom();
                }

                room = null;
                SetActiveState(PhotonRoomState.Disconnected);
            }

            if (_activeState == PhotonRoomState.Connecting)
            {
                SetActiveState(PhotonRoomState.Connected);
                ReplayImportantEvents();
            }
        }

        private void DisposeSessionComponents()
        {
            _activeTypeRegistrar.InRoom = false;

            if (_activeLocation != null)
            {
                _activeLocation.Dispose();
            }

            if (_activeProperties != null)
            {
                _activeProperties.Dispose();
            }

            if (_activeParticipants != null)
            {
                _activeParticipants.Dispose();
            }

            if (_activeMessages != null)
            {
                _activeMessages.Dispose();
            }

            if (_activeAvatars != null)
            {
                _activeAvatars.Dispose();
            }

            if (_activeOwnership != null)
            {
                _activeOwnership.Dispose();
            }

            RemoveSessionEventHandlers();

            _activeLocation = null;
            _activeProperties = null;
            _activeRoom = null;
            _activeParticipants = null;
            _activeMessages = null;
            _activeAvatars = null;
            _activeOwnership = null;
        }

        private void SetActiveState(PhotonRoomState newState)
        {
            if (_activeState != newState)
            {
                _activeState = newState;

                switch (newState)
                {
                    case PhotonRoomState.Disconnected:
                        CurrentRoomChanged?.Invoke(this, _activeRoom);
                        Disconnected?.Invoke(this);
                        break;

                    case PhotonRoomState.Connected:
                        CurrentRoomChanged?.Invoke(this, _activeRoom);
                        Connected?.Invoke(this);
                        break;

                    case PhotonRoomState.Connecting:
                        Connecting?.Invoke(this);
                        break;
                }
            }
        }

        /// <summary>
        /// Double check a new sharing session/room can be created.
        /// </summary>
        private void EnsureCanCreateSession()
        {
            if (!IsLoggedIn)
            {
                throw new Exception("Unable to create session, not logged into the sharing service");
            }

            if (IsConnected)
            {
                throw new Exception("Unable to create, not disconnected from existing session");
            }
        }

        /// <summary>
        /// Double check a new room is still connected to.
        /// </summary>
        private void EnsureConnectedRoom(PhotonSharingRoom room)
        {
            if (_activeConnection.CurrentRoom == null ||
                _activeConnection.CurrentRoom.Inner == null ||
                _activeConnection.CurrentRoom.Inner != room.Inner)
            {
                throw new Exception($"The app is no longer connected to room '{room.Name}'");
            }
        }

        /// <summary>
        /// Add events for current active session objects. 
        /// </summary>
        private void AddSessionEventHandlers()
        {
            if (_activeLocation != null)
            {
                _activeLocation.PrimaryAddressChanged += OnPrimaryAddressChanged;
                _activeLocation.LocalAddressesChanged += OnLocalAddressesChanged;
                _activeLocation.AddressParticipantsChanged += OnPrimaryAddressUsersChanged;
            }

            if (_activeProperties != null)
            {
                _activeProperties.PropertyChanged += OnPropertyChanged;
                _activeProperties.PlayerPropertyChanged += OnPlayerPropertyChanged;
                _activeProperties.PlayerDisplayNameChanged += OnPlayerDisplayNameChanged;
            }

            if (_activeParticipants != null)
            {
                _activeParticipants.PlayerAdded += OnPlayerAdded;
                _activeParticipants.PlayerRemoved += OnPlayerRemoved;
            }

            if (_activeMessages != null)
            {
                _activeMessages.MessageReceived += OnMessageReceived;
            }
        }

        /// <summary>
        /// Remove events for current active session objects. 
        /// </summary>
        private void RemoveSessionEventHandlers()
        {
            if (_activeLocation != null)
            {
                _activeLocation.PrimaryAddressChanged -= OnPrimaryAddressChanged;
                _activeLocation.LocalAddressesChanged -= OnLocalAddressesChanged;
                _activeLocation.AddressParticipantsChanged -= OnPrimaryAddressUsersChanged;
            }

            if (_activeProperties != null)
            {
                _activeProperties.PropertyChanged -= OnPropertyChanged;
                _activeProperties.PlayerPropertyChanged -= OnPlayerPropertyChanged;
                _activeProperties.PlayerDisplayNameChanged -= OnPlayerDisplayNameChanged;
            }

            if (_activeParticipants != null)
            {
                _activeParticipants.PlayerAdded -= OnPlayerAdded;
                _activeParticipants.PlayerRemoved -= OnPlayerRemoved;
            }

            if (_activeMessages != null)
            {
                _activeMessages.MessageReceived -= OnMessageReceived;
            }
        }

        /// <summary>
        /// Add event handlers that last the lifetime of this component.
        /// </summary>
        private void AddLifetimeEventHandlers()
        {
            if (_activeComponents != null && _activeComponents.VolumeControl != null)
            {
                _activeComponents.VolumeControl.ExternalVolumeChange += OnExternalVolumeChange;
            }
        }

        /// <summary>
        /// Remove event handlers that last the lifetime of this component.
        /// </summary>
        private void RemoveLifetimeEventHandlers()
        {
            if (_activeComponents != null && _activeComponents.VolumeControl != null)
            {
                _activeComponents.VolumeControl.ExternalVolumeChange -= OnExternalVolumeChange;
            }
        }

        /// <summary>
        /// Replay events consumers may have missed, and that are important
        /// </summary>
        private void ReplayImportantEvents()
        {
            _activeLocation?.ReplayPropertyChangeEvents();
            _activeParticipants?.ReplayPlayerAddedEvents();
            _activeProperties?.ReplayPropertyChangeEvents();
        }

        /// <summary>
        /// Spawn game objects when connected to Photon
        /// </summary>
        private Task<GameObject> SpawnOnlineTarget(GameObject original, object[] data)
        {
            return Task.FromResult(PhotonNetwork.Instantiate(
                original.name,
                Vector3.zero,
                Quaternion.identity,
                group: 0,
                data));
        }

        /// <summary>
        /// Despawn game objects when connected to Photon
        /// </summary>
        private Task DespawnOnlineTarget(GameObject original)
        {
            TaskCompletionSource<object> taskSource = new TaskCompletionSource<object>();
            if (_activeParticipants != null)
            {
                PhotonViewAction.Create(original, _activeParticipants.LocalParticipant, (view) =>
                {
                    PhotonNetwork.Destroy(view);
                    taskSource.TrySetResult(null);
                });
            }
            return taskSource.Task;
        }

        /// <summary>
        /// Handle volume changing by an external source.
        /// </summary>
        private void OnExternalVolumeChange(PhotonMicrophoneVolumeControl sender, float volume)
        {
            AudioSettingsChanged?.Invoke(this, AudioSettings);
        }

        /// <summary>
        /// Handle the current room changing on the connection manager.
        /// </summary>
        private void OnConnectionRoomChanged(PhotonConnectionManager sender, ISharingServiceRoom room)
        {
            if (_activeRoom != null)
            {
                if (room == null || room.Name == _activeRoom.Name)
                {
                    LeaveRoom();
                }
            }
        }

        /// <summary>
        /// Handle rooms changing.
        /// </summary>
        private void OnConnectionRoomsChanged(PhotonConnectionManager sender, IReadOnlyCollection<ISharingServiceRoom> rooms)
        {
            RoomsChanged?.Invoke(this, rooms);
        }

        /// <summary>
        /// Handle new location id changed
        /// </summary>
        private void OnPrimaryAddressChanged(SharingServiceLocation sender, SharingServiceAddress address)
        {
            if (_activeLocation == sender)
            {
                try
                {
                    PrimaryAddressChanged?.Invoke(this, address);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Event handler encountered an exception during address change. Exception = {0}", ex);
                }
            }
        }

        /// <summary>
        /// Handle users at current address changing
        /// </summary>
        private void OnPrimaryAddressUsersChanged(SharingServiceLocation sender, SharingServiceAddress address)
        {
            if (_activeLocation == sender)
            {
                try
                {
                    PrimaryAddressUsersChanged?.Invoke(this);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Event handler encountered an exception during address users changing. Exception = {0}", ex);
                }
            }
        }

        /// <summary>
        /// Handle new known local addresses changing
        /// </summary>
        private void OnLocalAddressesChanged(SharingServiceLocation sender, IReadOnlyList<SharingServiceAddress> addressess)
        {
            if (sender == _activeLocation)
            {
                try
                {
                    LocalAddressesChanged?.Invoke(this, addressess);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Event handler encountered an exception during local addresses change. Exception = {0}", ex);
                }
            }
        }

        /// <summary>
        /// Handle property change.
        /// </summary>
        private void OnPropertyChanged(PhotonProperties sender, string propertyName, object propertyValue)
        {
            if (sender == _activeProperties)
            {
                try
                {
                    PropertyChanged?.Invoke(this, propertyName, propertyValue);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Event handler encountered an exception during property change. Exception = {0}", ex);
                }
            }
        }

        /// <summary>
        /// Handle player property change.
        /// </summary>
        private void OnPlayerPropertyChanged(PhotonProperties sender, string participantId, string propertyName, object propertyValue)
        {
            if (sender == _activeProperties)
            {
                try
                {
                    PlayerPropertyChanged?.Invoke(this, participantId, propertyName, propertyValue);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Event handler encountered an exception during player property change. Exception = {0}", ex);
                }
            }
        }

        /// <summary>
        /// Handle player properties name change.
        /// </summary>
        private void OnPlayerDisplayNameChanged(PhotonProperties sender, string player, string name)
        {
            if (sender == _activeProperties)
            {
                try
                {
                    PlayerDisplayNameChanged?.Invoke(this, player, name);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Event handler encountered an exception during player name changed event. Exception = {0}", ex);
                }
            }
        }

        /// <summary>
        /// Handle player being removed from room.
        /// </summary>
        private void OnPlayerRemoved(PhotonParticipants sender, PhotonParticipant participant)
        {
            if (sender == _activeParticipants)
            {
                try
                {
                    PlayerRemoved?.Invoke(this, participant.ToPlayerData());
                }
                catch (Exception ex)
                {
                    _logger.LogError("Event handler encountered an exception during player removed. Exception = {0}", ex);
                }
            }
        }

        /// <summary>
        /// Handle player being added to room.
        /// </summary>
        private void OnPlayerAdded(PhotonParticipants sender, PhotonParticipant participant)
        {
            if (sender == _activeParticipants)
            {
                try
                {
                    PlayerAdded?.Invoke(this, participant.ToPlayerData());
                }
                catch (Exception ex)
                {
                    _logger.LogError("Event handler encountered an exception during player added. Exception = {0}", ex);
                }
            }
        }

        /// <summary>
        /// Handle a new incoming photon message.
        /// </summary>
        private void OnMessageReceived(PhotonMessages sender, PhotonMessage message)
        {
            if (_activeMessages == sender)
            {
                switch (message.inner.type)
                {
                    case ProtocolMessageType.SharingServiceTransform when message.inner.data.value is SharingServiceTransform:
                        SharingServiceTransform transform = (SharingServiceTransform)message.inner.data.value;
                        TransformMessageReceived?.Invoke(this, transform.Target, transform);
                        break;

                    case ProtocolMessageType.SharingServiceMessage when message.inner.data.value is SharingServiceMessage:
                        MessageReceived?.Invoke(this, (SharingServiceMessage)message.inner.data.value);
                        break;

                    case ProtocolMessageType.SharingServicePingRequest when message.inner.data.value is SharingServicePingRequest:
                        ReturnPing((SharingServicePingRequest)message.inner.data.value, message.sender.Identifier);
                        break;

                    case ProtocolMessageType.SharingServicePingResponse when message.inner.data.value is SharingServicePingResponse:
                        SharingServicePingResponse response = (SharingServicePingResponse)message.inner.data.value;
                        PingReturned?.Invoke(this, message.sender.Identifier, response);
                        break;
                }
            }
        }

        /// <summary>
        /// Set StatusMessage and return action that can clear this particular StatusMessage
        /// </summary>
        private Action SetStatusMessage(string message)
        {
            StatusMessage = message;
            return () =>
            {
                if (StatusMessage == message)
                {
                    StatusMessage = null;
                }
            };
        }
        #endregion Private Methods
        
        #region Logging Methods
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
        /// Log a message if information logging is enabled.
        /// </summary>
        private void LogInformation(string message)
        {
            _logger.LogInformation(message);
        }

        /// <summary>
        /// Log a message if information logging is enabled. 
        /// </summary>
        private void LogInformation(string messageFormat, params object[] args)
        {
            _logger.LogInformation(messageFormat, args);
        }

        /// <summary>
        /// Log a message if warning logging is enabled.
        /// </summary>
        private void LogWarning(string message)
        {
            _logger.LogWarning(message);
        }

        /// <summary>
        /// Log a message if warning logging is enabled. 
        /// </summary>
        private void LogWarning(string messageFormat, params object[] args)
        {
            _logger.LogWarning(messageFormat, args);
        }


        /// <summary>
        /// Log a message if error logging is enabled.
        /// </summary>
        private void LogError(string message)
        {
            _logger.LogError(message);
        }

        /// <summary>
        /// Log a message if error logging is enabled. 
        /// </summary>
        private void LogError(string messageFormat, params object[] args)
        {
            _logger.LogError(messageFormat, args);
        }
        #endregion Logging Methods

        #region Private Enum
        private enum PhotonRoomState
        {
            Connecting,
            Connected,
            Disconnected,
        }
        #endregion Private Enum

        #region Private Delegates
        /// <summary>
        /// A factory function for creating a new sharing room.
        /// </summary>
        private delegate Task<PhotonSharingRoom> CreateRoomFactory();
        #endregion Private Delegates
    }
}
#endif // PHOTON_INSTALLED
