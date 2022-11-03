// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication;
using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// A service for sharing application state across other clients.
    /// </summary>
    [MixedRealityExtensionService(SupportedPlatforms.WindowsStandalone | SupportedPlatforms.WindowsUniversal | SupportedPlatforms.WindowsEditor)]
    public class SharingService : BaseExtensionService, ISharingService, ISharingServiceObjectInitializer, IMixedRealityExtensionService
    {
        private LogHelper<SharingService> _logger = new LogHelper<SharingService>();
        private SharingServiceProfile _defaultProfile;
        private SharingServiceProfile _loadedProfile;
        private SharingServiceProtocol _protocol;
        private HashSet<ISharingServiceObject> _targets = new HashSet<ISharingServiceObject>();
        private Dictionary<string, SharingServicePlayer> _players = new Dictionary<string, SharingServicePlayer>();
        private ISharingServicePlayer _localPlayer;
        private SynchronizationContext _appContext;
        private bool _handlingQuits;
        private GameObject _root;
        private OfflineSpawner _offlineSpawner = new OfflineSpawner();
        private Dictionary<string, SharingServicePingRequest> _pingRequests = new Dictionary<string, SharingServicePingRequest>();

        private const byte BLAST_ID = 0xfb;

        public SharingService(string name, uint priority, BaseMixedRealityProfile profile)
            : base(name, priority, profile)
        {
            _protocol = new SharingServiceProtocol();
            _defaultProfile = profile as SharingServiceProfile;
            if (_defaultProfile == null)
            {
                _defaultProfile = ScriptableObject.CreateInstance<SharingServiceProfile>();
            }
        }

        #region ISharingService Properties
        /// <summary>
        /// True if service is ready for use.
        /// </summary>
        public bool IsReady => Provider != null;

        /// <summary>
        /// True if connected to a session and able to communicate with other clients
        /// </summary>
        public bool IsConnected => Provider?.IsConnected ?? false;

        /// <summary>
        /// Get if the provider is connecting
        /// </summary>
        public bool IsConnecting => Provider?.IsConnecting ?? false;

        /// <summary>
        /// True if connected to sharing service and logged in. But not necessarily in a session
        /// </summary>
        public bool IsLoggedIn => Provider?.IsLoggedIn ?? false;

        /// <summary>
        /// Get the service's status message
        /// </summary>
        public string StatusMessage => Provider?.StatusMessage;

        /// <summary>
        /// Get all known sharing targets.
        /// </summary>
        public IReadOnlyCollection<ISharingServiceObject> Targets => _targets;

        /// <summary>
        /// The list of current room.
        /// </summary>
        public IReadOnlyCollection<ISharingServiceRoom> Rooms => Provider?.Rooms;

        /// <summary>
        /// The list of current players
        /// </summary>
        public IReadOnlyCollection<ISharingServicePlayer> Players => _players.Values;

        /// <summary>
        /// The local player.
        /// </summary>
        public ISharingServicePlayer LocalPlayer
        {
            get => _localPlayer;

            set
            {
                if (_localPlayer != value)
                {
                    _localPlayer = value;
                    LocalPlayerChanged?.Invoke(this, value);
                }
            }
        }

        /// <summary>
        /// Get the invalid player id
        /// </summary>
        public string InvalidPlayerId => Provider?.InvalidPlayerId ?? string.Empty;

        /// <summary>
        /// Get the current room.
        /// </summary>
        public ISharingServiceRoom CurrentRoom => Provider?.CurrentRoom;

        /// <summary>
        /// Get if the sharing service's configuration supports private sharing session/rooms.
        /// </summary>
        public bool HasPrivateRooms => Provider?.HasPrivateRooms ?? false;

        /// <summary>
        /// The user's current address.
        /// </summary>
        public SharingServiceAddress PrimaryAddress => Provider?.PrimaryAddress;

        /// <summary>
        /// Get the known addresses in the user's local space.
        /// </summary>
        public IReadOnlyList<SharingServiceAddress> LocalAddresses => Provider?.LocalAddresses;

        /// <summary>
        /// Get the container for all sharing related game objects, such as new avatars. Avatar positioning will be relative to this container.
        /// This must be setting before joining a sharing room. If not set, avatars will not appear.
        /// </summary>
        public GameObject Root => Provider?.SharingRoot;

        /// <summary>
        /// Get or set the providers audio settings.
        /// </summary>
        public SharingServiceAudioSettings AudioSettings
        {
            get => Provider?.AudioSettings ?? SharingServiceAudioSettings.Default;

            set
            {
                if (Provider != null)
                {
                    Provider.AudioSettings = value;
                }
            }
        }

        /// <summary>
        /// Get the provider audio capabilities.
        /// </summary>
        public SharingServiceAudioCapabilities AudioCapabilities
        {
            get => Provider != null ? Provider.AudioCapabilities : SharingServiceAudioCapabilities.Default;
        }

        /// <summary>
        /// Get or set the provider's avatar settings
        /// </summary>
        public SharingServiceAvatarSettings AvatarSettings
        {
            get => Provider != null ? Provider.AvatarSettings : SharingServiceAvatarSettings.Default;

            set
            {
                if (Provider != null)
                {
                    Provider.AvatarSettings = value;
                }
            }
        }
        #endregion ISharingService Properties

        #region Private Properties
        /// <summary>
        /// Get the sharing provider.
        /// </summary>
        private ISharingProvider Provider { get; set; }

        /// <summary>
        /// Is there a valid provider that supports offline spawning
        /// </summary>
        private bool ProviderSupportsOfflineSpawning
        {
            get => Provider != null && Provider.OfflineSpawningSupported;
        }
        #endregion Private Properties

        #region ISharingService Events
        /// <summary>
        /// Event fired when the service is connected
        /// </summary>
        public event Action<ISharingService> Connected;

        /// <summary>
        /// Event fired when the service is connecting
        /// </summary>
        public event Action<ISharingService> Connecting;

        /// <summary>
        /// Event fired when the service disconnects
        /// </summary>
        public event Action<ISharingService> Disconnected;

        /// <summary>
        /// Event fired when the service's status message has changed
        /// </summary>
        public event Action<ISharingService, string> StatusMessageChanged;

        /// <summary>
        /// Event fired when the current room has changed.
        /// </summary>
        public event Action<ISharingService, ISharingServiceRoom> CurrentRoomChanged;

        /// <summary>
        /// Event fired when an invitation is received.
        /// </summary>
        public event Action<ISharingService, ISharingServiceRoom> RoomInviteReceived;

        /// <summary>
        /// Event fired when the rooms have changed.
        /// </summary>
        public event Action<ISharingService, IReadOnlyCollection<ISharingServiceRoom>> RoomsChanged;

        /// <summary>
        /// Event fired when a message is received from a remote client
        /// </summary>
        public event Action<ISharingService, ISharingServiceMessage> MessageReceived;

        /// <summary>
        /// Event fired when a new sharing object has been added 
        /// </summary>
        public event Action<ISharingService, ISharingServiceObject> ObjectAdded;

        /// <summary>
        /// Event fired when the local player object has changed
        /// </summary>
        public event Action<ISharingService, ISharingServicePlayer> LocalPlayerChanged;

        /// <summary>
        /// Event fired when a new player has been added.
        /// </summary>
        public event Action<ISharingService, ISharingServicePlayer> PlayerAdded;

        /// <summary>
        /// Event fired when a new player has been removed.
        /// </summary>
        public event Action<ISharingService, ISharingServicePlayer> PlayerRemoved;

        /// <summary>
        /// Event fired when a player property has changed
        /// </summary>
        public event Action<ISharingServicePlayer, string, object> PlayerPropertyChanged;

        /// <summary>
        /// Event fired when a player display name has changed
        /// </summary>
        public event Action<ISharingServicePlayer, string> PlayerDisplayNameChanged;

        /// <summary>
        /// Event fired when a user's address has changed.
        /// </summary>
        public event Action<ISharingService, SharingServiceAddress> AddressChanged;

        /// <summary>
        /// Event fired when the users at the user's address have changed.
        /// </summary>
        public event Action<ISharingService> AddressUsersChanged;

        /// <summary>
        /// Event fired when a user's local addresses have changed.
        /// </summary>
        public event Action<ISharingService, IReadOnlyList<SharingServiceAddress>> LocalAddressesChanged;

        /// <summary>
        /// Event fired when a ping response has been received
        /// </summary>
        public event Action<ISharingService, string, TimeSpan> PingReturned;

        /// <summary>
        /// Event fired when audio settings changed.
        /// </summary>
        public event Action<ISharingService, SharingServiceAudioSettings> AudioSettingsChanged;

        /// <summary>
        /// Event fired when avatar settings changed.
        /// </summary>
        public event Action<ISharingService, SharingServiceAvatarSettings> AvatarSettingsChanged;
        #endregion ISharingService Events

        #region ISharingService Methods
        /// <summary>
        /// Connects to the session
        /// </summary>
        public void Login()
        {
            if (ValidateProviderReady())
            {
                Provider.Login();
            }
        }

        /// <summary>
        /// Create and join a new public sharing room.
        /// </summary>
        public Task CreateAndJoinRoom()
        {
            return ConfirmJoinRoomWithAction(creatingRoom: true, () => Provider.CreateAndJoinRoom());
        }

        /// <summary>
        /// Create and join a new private sharing room. Only the given list of players can join the room.
        /// </summary>
        public Task CreateAndJoinRoom(IEnumerable<SharingServicePlayerData> inviteList)
        {
            return ConfirmJoinRoomWithAction(creatingRoom: true, () => Provider.CreateAndJoinRoom(inviteList));
        }

        /// <summary>
        /// Join the given room.
        /// </summary>
        public void JoinRoom(ISharingServiceRoom room)
        {
            _ = ConfirmJoinRoomWithAction(creatingRoom: false, () => Provider.JoinRoom(room));
        }

        /// <summary>
        /// Join the given room by room id
        /// </summary>
        public void JoinRoom(string roomId)
        {
            _ = ConfirmJoinRoomWithAction(creatingRoom: false, () => Provider.JoinRoom(roomId));
        }

        /// <summary>
        /// Decline a room/session invitation.
        /// </summary>
        public void DeclineRoom(ISharingServiceRoom room)
        {
            if (ValidateProviderReady())
            {
                Provider.DeclineRoom(room);
            }
        }

        /// <summary>
        /// If the current user is part of a private room, invite the given player to this room.
        /// </summary>
        public Task<bool> InviteToRoom(SharingServicePlayerData player)
        {
            if (ValidateProviderReady())
            {
                return Provider.InviteToRoom(player);
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Leave the currently joined sharing room, and join the default lobby.
        /// </summary>
        public Task LeaveRoom()
        {
            if (ValidateProviderReady())
            {
                return Provider.LeaveRoom();
            }
            else
            {
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Send a specialized message that contains only a transform. 
        /// </summary>
        public void SendTransformMessage(string target, SharingServiceTransform transform)
        {
            if (ValidateProviderReady())
            {
                Provider.SendTransformMessage(target, transform);
            }
        }

        /// <summary>
        /// Sends a message to all other clients
        /// </summary>
        /// <param name="message">Message to send</param>
        public void SendMessage(ISharingServiceMessage message)
        {
            if (ValidateProviderReady())
            {
                Provider.SendMessage(message);
            }
        }

        /// <summary>
        /// Try to set a sharing service's property value.
        /// </summary>
        public void SetProperty(string key, object value)
        {
            if (ValidateProviderReady())
            {
                Provider.SetProperty(key, value);
            }
        }

        /// <summary>
        /// Set a shared properties on the server. Setting to a value to null will clear the property from the server.
        /// </summary>
        public void SetProperties(params object[] propertyNamesAndValues)
        {
            if (ValidateProviderReady())
            {
                Provider.SetProperties(propertyNamesAndValues);
            }
        }

        /// <summary>
        /// Try to get a sharing service's property value.
        /// </summary>
        /// <returns>True if a non-null property value was found.</returns>
        public bool TryGetProperty(string key, out object value)
        {
            if (ValidateProviderReady())
            {
                return Provider.TryGetProperty(key, out value);
            }
            else
            {
                value = null;
                return false;
            }
        }

        // <summary>
        /// Does this provider have the current property
        /// </summary>
        public bool HasProperty(string property)
        {
            if (ValidateProviderReady())
            {
                return Provider.HasProperty(property);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Clear all properties with the given prefix.
        /// </summary>
        public void ClearPropertiesStartingWith(string prefix)
        {
            if (ValidateProviderReady())
            {
                Provider.ClearPropertiesStartingWith(prefix);
            }
        }

        /// <summary>
        /// Try to set a player's property value.
        /// </summary>
        public void SetPlayerProperty(string playerId, string key, object value)
        {
            if (ValidateProviderReady())
            {
                Provider.SetPlayerProperty(playerId, key, value);
            }
        }

        /// <summary>
        /// Try to get a player property's value.
        /// </summary>
        /// <returns>True if a non-null property value was found.</returns>
        public bool TryGetPlayerProperty(string playerId, string key, out object value)
        {
            if (ValidateProviderReady())
            {
                return Provider.TryGetPlayerProperty(playerId, key, out value);
            }
            else
            {
                value = default;
                return false;
            }
        }

        /// <summary>
        /// Does this provider have the current property for the player.
        /// </summary>
        public bool HasPlayerProperty(string playerId, string property)
        {
            if (ValidateProviderReady())
            {
                return Provider.HasPlayerProperty(playerId, property);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Update list of rooms
        /// </summary>
        public Task<IReadOnlyCollection<ISharingServiceRoom>> UpdateRooms()
        {
            if (ValidateProviderReady())
            {
                return Provider.UpdateRooms();
            }
            else
            {
                return Task.FromResult<IReadOnlyCollection<ISharingServiceRoom>>(new List<ISharingServiceRoom>());
            }
        }

        /// <summary>
        /// Send a ping
        /// if player is not null, will send it to that person only
        /// </summary>
        public void SendPing(string targetRecipientId = null)
        {
            if (!IsConnected || !ValidateProviderReady())
            {
                return;
            }

            if (Players == null || Players.Count < 2)
            {
                _logger.LogWarning($"Must have at least 2 people in the room.");
                return;
            }

            _pingRequests.Clear();
            if (string.IsNullOrEmpty(targetRecipientId))
            {
                // broadcast ping
                SharingServicePingRequest? pingRequest = Provider.SendPing(BLAST_ID, targetRecipientId);
                if (pingRequest.HasValue)
                {
                    foreach (var player in Players)
                    {
                        _pingRequests.Add(player.Data.PlayerId, pingRequest.Value);
                    }
                }
            }
            else
            {
                // Target ping to known players
                var player = Players.FirstOrDefault(p => p.Data.PlayerId == targetRecipientId);
                if (player != null)
                {
                    System.Random rnd = new System.Random();
                    SharingServicePingRequest? pingRequest = Provider.SendPing((byte)rnd.Next(0x00, 0xff), targetRecipientId);
                    if (pingRequest.HasValue)
                    {
                        _pingRequests.Add(player.Data.PlayerId, pingRequest.Value);
                    }
                }
                else
                {
                    _logger.LogWarning("Attempted to send ping to unknown player '{0}'.", targetRecipientId);
                }
            }
        }

        /// <summary>
        /// Create a unique target object that will be synced across clients. 
        /// A unique identification will be automatically created once client
        /// connects to the sharing service.
        /// </summary>
        public ISharingServiceObject CreateTarget(SharingServiceObjectType type)
        {
            return SharingServiceObjectModel.Create(
                this, CurrentRoom, new SharingServiceObjectModel.Identification()
                {
                    Type = type
                });
        }

        /// <summary>
        /// Create a target object, with a given label, that will be synced across clients.
        /// </summary>
        public ISharingServiceObject CreateTarget(SharingServiceObjectType type, string label)
        {
            return SharingServiceObjectModel.Create(
                this, CurrentRoom, new SharingServiceObjectModel.Identification()
                {
                    Type = type,
                    Label = label
                });
        }

        /// <summary>
        /// Create a target object, from a sharing id.
        /// </summary>
        public ISharingServiceObject CreateTargetFromSharingId(string sharingId)
        {
            return SharingServiceObjectModel.Create(this, CurrentRoom, sharingId);
        }

        /// <summary>
        /// Spawn a network object that is shared across all clients
        /// </summary>
        public async Task<GameObject> SpawnTarget(GameObject original, object[] data = null)
        {
            Task<GameObject> spawning;
            if (IsConnecting)
            {
                spawning = Task.FromException<GameObject>(new Exception(
                    "Can't spawn game objects while connecting to service"));
            }
            else if (IsConnected || ProviderSupportsOfflineSpawning)
            {
                original = await SharingServiceResources.Convert(_loadedProfile.Provider, original);
                spawning = Provider.SpawnTarget(original, WrapSpawnData(data));
            }
            else
            {
                spawning = _offlineSpawner.SpawnTarget(original, WrapSpawnData(data));
            }

            GameObject result = await spawning;
            SharingServiceDynamicSpawn.Instance(result, original, data);

            return result;
        }

        /// <summary>
        /// Despawn a network object that is shared across all clients
        /// </summary>
        public Task DespawnTarget(GameObject gameObject)
        {
            if (IsConnecting)
            {
                return Task.FromException(new Exception(
                    "Can't despawn game objects while connecting to service"));
            }
            else if (IsConnected || ProviderSupportsOfflineSpawning)
            {
                return Provider.DespawnTarget(gameObject);
            }
            else
            {
                return _offlineSpawner.DespawnTarget(gameObject);
            }
        }
        
        /// <summary>
        /// Start finding the nearest address.
        /// </summary>
        public async void FindAddresses()
        {
            if (ValidateProviderReady())
            {
                try
                {
                    await Provider.FindAddresses();
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to find sharing addresses. Exception: {0}", ex);
                }
            }
        }

        /// <summary>
        /// Start updating the address, using the current position of the sharing root.
        /// </summary>
        public async void CreateAddress()
        {
            if (ValidateProviderReady())
            {
                try
                {
                    await Provider.CreateAddress();
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to create sharing address at sharing root. Exception: {0}", ex);
                }
            }
        }

        /// <summary>
        /// Set the user's primary address
        /// </summary>
        public async void SetAddress(SharingServiceAddress address)
        {
            if (ValidateProviderReady())
            {
                try
                {
                    await Provider.SetAddress(address);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to set sharing address to {0}. Exception: {1}", address, ex);
                }
            }
        }

        /// <summary>
        /// Get if the user is co-located with the current device.
        /// </summary>
        public bool Colocated(string participantId)
        {
            if (ValidateProviderReady())
            {
                try
                {
                    return Provider.Colocated(participantId);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Failed to get if co-located. Exception: {0}", ex);
                }
            }
            return false;
        }

        /// <summary>
        /// Find sharing service players by a name. These player might not be in the current session.
        /// </summary>
        public Task<IList<SharingServicePlayerData>> FindPlayers(string prefix, CancellationToken ct = default(CancellationToken))
        {
            if (ValidateProviderReady())
            {
                return Provider.FindPlayers(prefix, ct);
            }
            else
            {
                return Task.FromResult<IList<SharingServicePlayerData>>(new List<SharingServicePlayerData>());
            }
        }

        /// <summary>
        /// Initialize a network object with sharing components needed for the selected provider
        /// </summary>
        public void EnsureNetworkObjectComponents(GameObject gameObject)
        {
            if (Application.isPlaying && !gameObject.IsPrefab())
            {
                if (ValidateProviderReady())
                {
                    Provider.EnsureNetworkObjectComponents(gameObject);
                }
            }
        }

        /// <summary>
        /// Calibrate the sharing service's microphone so to better detect voices, and eliminate background noise.
        /// </summary>
        public Task<bool> CalibrateVoiceDetection()
        {
            if (!ValidateProviderReady())
            {
                return Task.FromResult(false);
            }
            else if (!AudioCapabilities.SupportsVoiceCalibration)
            {
                return Task.FromException<bool>(new NotImplementedException("Voice calibration is not supported by sharing provider"));
            }
            else
            {
                return Provider.CalibrateVoiceDetection();
            }
        }
        #endregion ISharingService Methods

        #region ISharingServiceObjectInitializer Methods
        /// <summary>
        /// Initialize the sharing service object.
        /// </summary>
        public void InitializeSharingObject(ISharingServiceObject sharingObject, object[] data)
        {
            // Unwrap protocol messages. Some providers require wrapping data
            data = UnwrapSpawnData(data);

            if (sharingObject != null && sharingObject.IsRoot && Application.isPlaying)
            {
                if (sharingObject is SharingObject)
                {
                    EnsureNetworkObjectComponents(((SharingObject)sharingObject).gameObject);
                }

                if (sharingObject is SharingObjectBase)
                {
                    ((SharingObjectBase)sharingObject).Initialize(SharingServiceObjectModel.Create(
                        this, CurrentRoom, new SharingServiceObjectModel.Identification()
                        {
                            Type = sharingObject.Type,
                            Label = sharingObject.Label,
                        }));
                }

                if (sharingObject is MonoBehaviour)
                {
                    var sharingObjectHandlers = ((MonoBehaviour)sharingObject).GetComponents<ISharingServiceObjectInitialized>();
                    foreach (var entry in sharingObjectHandlers)
                    {
                        entry.Initialized(sharingObject, data);
                    }
                }
            }
        }
        #endregion ISharingServiceObjectInitializer Methods

        #region BaseExtensionService Methods
        /// <summary>
        /// Initialize the sharing service, and initialize the internal sharing provider. A provider is a wrapper around a 
        /// praticular networking service. Currently Photon is the only supported provider.
        /// </summary>
        public override async void Initialize() 
        {
            _appContext = SynchronizationContext.Current;
            _loadedProfile = await SharingServiceProfileLoader.Load(_defaultProfile);
            _root = Object.FindObjectOfType<SharingServiceRoot>()?.gameObject;

            // Configure the hand serialization for avatars
            if (!_handlingQuits)
            {
                Application.quitting += ApplicationQuitting;
                _handlingQuits = true;
            }

            if (Application.isPlaying)
            {
                switch (_loadedProfile.Provider)
                {
                    case SharingServiceProfile.ProviderService.Photon:
                        Provider = CreatePhotonProvider();
                        break;

                    case SharingServiceProfile.ProviderService.Offline:
                        Provider = CreateOfflineProvider();
                        break;

                    case SharingServiceProfile.ProviderService.None:
                        _logger.LogWarning("Sharing service is disabled. No sharing provider set.");
                        Provider = CreateOfflineProvider();
                        break;

                    default:
                        _logger.LogError("Unknown provider type '{0}'.", _loadedProfile.Provider);
                        Provider = CreateOfflineProvider();
                        break;
                }
            }

            if (Provider != null)
            {
                Provider.Connected += OnConnected;
                Provider.Connecting += OnConnecting;
                Provider.Disconnected += OnDisconnected;
                Provider.StatusMessageChanged += OnStatusMessageChanged;
                Provider.RoomsChanged += OnRoomsChanged;
                Provider.CurrentRoomChanged += OnCurrentRoomChanged;
                Provider.RoomInviteReceived += OnRoomInviteReceived;
                Provider.MessageReceived += OnMessageReceived;
                Provider.TransformMessageReceived += OnTransformMessageReceived;
                Provider.PingReturned += OnPingReturned;
                Provider.PropertyChanged += OnPropertyChanged;
                Provider.PlayerAdded += OnPlayerAdded;
                Provider.PlayerRemoved += OnPlayerRemoved;
                Provider.PlayerPropertyChanged += OnPlayerPropertyChanged;
                Provider.PlayerDisplayNameChanged += OnPlayerDisplayNameChanged;
                Provider.PrimaryAddressChanged += OnAddressChanged;
                Provider.PrimaryAddressUsersChanged += OnAddressUsersChanged;
                Provider.LocalAddressesChanged += OnLocalAddressesChanged;
                Provider.AudioSettingsChanged += OnAudioSettingsChanged;
                Provider.AvatarSettingsChanged += OnAvatarSettingsChanged;

                if (_loadedProfile.AutoStart)
                {
                    Login();
                }

                // Initialize static sharing targets
                InitializeAllSharingObjects();
            }
        }

        /// <summary>
        /// While the application is playing, update the internal provider class.
        /// </summary>
        public override void Update()
        {
            if (Application.isPlaying)
            {
                Provider?.Update();
            }
        }

        /// <summary>
        /// While the application is playing, update the internal provider class.
        /// </summary>
        public override void LateUpdate()
        {
            if (Application.isPlaying)
            {
                Provider?.LateUpdate();
            }
        }

        /// <summary>
        /// Destroy the internal provider class.
        /// </summary>
        public override void Destroy()
        {
            if (_handlingQuits)
            {
                Application.quitting -= ApplicationQuitting;
                _handlingQuits = false;
            }

            if (Provider != null)
            {
                Provider.Connected -= OnConnected;
                Provider.Connecting -= OnConnecting;
                Provider.Disconnected -= OnDisconnected;
                Provider.StatusMessageChanged -= OnStatusMessageChanged;
                Provider.RoomsChanged -= OnRoomsChanged;
                Provider.CurrentRoomChanged -= OnCurrentRoomChanged;
                Provider.RoomInviteReceived -= OnRoomInviteReceived;
                Provider.MessageReceived -= OnMessageReceived;
                Provider.TransformMessageReceived -= OnTransformMessageReceived;
                Provider.PropertyChanged -= OnPropertyChanged;
                Provider.PlayerAdded -= OnPlayerAdded;
                Provider.PlayerRemoved -= OnPlayerRemoved;
                Provider.PlayerPropertyChanged -= OnPlayerPropertyChanged;
                Provider.PlayerDisplayNameChanged -= OnPlayerDisplayNameChanged;
                Provider.PrimaryAddressChanged -= OnAddressChanged;
                Provider.PrimaryAddressUsersChanged -= OnAddressUsersChanged;
                Provider.LocalAddressesChanged -= OnLocalAddressesChanged;
                Provider.AudioSettingsChanged -= OnAudioSettingsChanged;
                Provider.AvatarSettingsChanged -= OnAvatarSettingsChanged;
                Provider.Dispose();
                Provider = null;
            }
        }
        #endregion BaseExtensionService Methods

        #region Private Methods
        /// <summary>
        /// Destory provider if in editor.
        /// </summary>
        private void ApplicationQuitting()
        {
            if (Application.isEditor)
            {
                Destroy();
            }
        }

        /// <summary>
        /// Create a offline sharing provider if supported.
        /// </summary>
        private ISharingProvider CreateOfflineProvider()
        {
            return new Sharing.Communication.Offline.OfflineProvider(_loadedProfile, _protocol, _root);
        }

        /// <summary>
        /// Create a Photon sharing provider if supported.
        /// </summary>
        private ISharingProvider CreatePhotonProvider()
        {
#if PHOTON_INSTALLED
            return new Sharing.Communication.Photon.PhotonProvider(_loadedProfile, _protocol, _root);
#else
            _logger.LogError("Photon for Unity is not installed. Install Photon Voice 2 from the Unity Asset Store.");
            return CreateOfflineProvider();
#endif
        }

        /// <summary>
        /// If the target is not known by this class, add it to the class's cache and raise an added event.
        /// </summary>
        private ISharingServiceObject TryRaiseTargetAdded(ISharingServiceObject target)
        {
            if (target != null && !_targets.Contains(target))
            {
                _targets.Add(target); 
                ObjectAdded?.Invoke(this, target);
            }
            return target;
        }

        /// <summary>
        /// Clear all known targets so that TryRaiseTargetAdded will see all targets as "new".
        /// </summary>
        private void ClearKnownTargets()
        {
            _targets.Clear();
        }
        
        /// <summary>
        /// Clear all sharing targets
        /// </summary>
        private void DestroyDynamicSharingObjects()
        {
            foreach(var target in Object.FindObjectsOfType<SharingObjectBase>())
            {
                if (target.Type == SharingServiceObjectType.Dynamic)
                {
                    Object.Destroy(target.gameObject);
                }
            }
        }

        /// <summary>
        /// Initialize all sharing targets in the scene, if not initialized yet
        /// </summary>
        private void InitializeAllSharingObjects()
        {
            foreach (var target in Object.FindObjectsOfType<SharingObjectBase>())
            {
                if (target.Inner == null)
                {
                    InitializeSharingObject(target, data: null);
                }
            }
        }

        /// <summary>
        /// Capture all sharing objects in the scene.
        /// </summary>
        private IReadOnlyList<SpawnInformation> CaptureRootSharingObjects(bool includeDynamic)
        {
            var allSharingObjects = Object.FindObjectsOfType<SharingObjectBase>();
            List<SpawnInformation> filtered = new List<SpawnInformation>(allSharingObjects.Length);
            foreach (var sharingObjects in allSharingObjects)
            {
                if (sharingObjects.IsRoot)
                {
                    SharingServiceDynamicSpawn spawn = sharingObjects.GetComponent<SharingServiceDynamicSpawn>();
                    bool isDynamic = spawn != null && spawn.OriginalPrefab != null;

                    if (includeDynamic || !isDynamic)
                    {
                        filtered.Add(new SpawnInformation()
                        {
                            Original = isDynamic ? spawn.OriginalPrefab : null,
                            CreationData = isDynamic ? spawn.CreationData : null,
                            SharingId = isDynamic ? null : sharingObjects.SharingId,
                            Properties = CaptureSharingObjectProperties(sharingObjects),
                        });
                    }
                }
            }
            return filtered;
        }

        /// <summary>
        /// Create a shallow copy of the sharing properties.
        /// </summary>
        private IReadOnlyDictionary<string, object> CaptureSharingObjectProperties(ISharingServiceObject sharingObject)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            if (sharingObject.Properties != null)
            {
                foreach (var entry in sharingObject.Properties)
                {
                    result[entry.Key] = entry.Value;
                }
            }
            return result;
        }

        /// <summary>
        /// Respawn all the given sharing object, and synchronize their states with the cloud service.
        /// </summary>
        /// <param name="sharingObjects"></param>
        private void ReplaySharingObjects(IEnumerable<SpawnInformation> sharingObjects)
        {
            foreach (var entry in sharingObjects)
            {
                ReplaySharingObject(entry);
            }
        }

        /// <summary>
        /// Respawn and replay sharing object property changes
        /// </summary>
        private async void ReplaySharingObject(SpawnInformation information)
        {
            string replayTo = information.SharingId;
            if (information.Original != null)
            {
                replayTo = (await SpawnTargetWithSharingComponents(information.Original, information.CreationData)).SharingId;
            }

            // Find properties from the server that differ from local values.
            // Server values always take precedence over local values, when connecting
            // to a new server.
            List<object> receivePropertyNamesAndValues = new List<object>(information.Properties.Count * 2);
            foreach (var entry in information.Properties)
            {
                string providerPropertyName = SharingServicePropertyHelper.Encode(replayTo, entry.Key);
                if (!string.IsNullOrEmpty(providerPropertyName))
                {
                    object serverValue;
                    if (TryGetProperty(providerPropertyName, out serverValue) && serverValue != null)
                    {
                        if (serverValue != entry.Value)
                        {
                            receivePropertyNamesAndValues.Add(entry.Key);
                            receivePropertyNamesAndValues.Add(serverValue);
                        }
                    }
                }
            }

            // Notify local components of new property values received from the server.
            int lastKeyIndex = receivePropertyNamesAndValues.Count - 2;
            for (int i = 0; i <= lastKeyIndex; i += 2)
            {
                OnPropertyChanged(
                    SharingServicePropertyHelper.Encode(replayTo, (string)receivePropertyNamesAndValues[i]),
                    receivePropertyNamesAndValues[i + 1]);
            }

            // Find the properties to send to remote server. These are properties not known by the remote server.
            List<object> sendPropertyNamesAndValues = new List<object>(information.Properties.Count * 2);
            foreach (var entry in information.Properties)
            {
                string providerPropertyName = SharingServicePropertyHelper.Encode(replayTo, entry.Key);
                if (!string.IsNullOrEmpty(providerPropertyName))
                {
                    object serverValue;
                    if (!TryGetProperty(providerPropertyName, out serverValue) || serverValue == null)
                    {
                        sendPropertyNamesAndValues.Add(providerPropertyName);
                        sendPropertyNamesAndValues.Add(entry.Value);
                    }
                }
            }

            // Notify other players of new property values, after local client has handled server values.
            if (sendPropertyNamesAndValues.Count > 0)
            {
                SetProperties(sendPropertyNamesAndValues.ToArray());
            }
        }

        /// <summary>
        /// Spawn a network object that is shared across all clients, and return its sharing component
        /// </summary>
        private async Task<ISharingServiceObject> SpawnTargetWithSharingComponents(GameObject original, object[] data = null)
        {
            GameObject spawned = await SpawnTarget(original, data);
            ISharingServiceObject result = null;
            if (spawned != null)
            {
                result = spawned.GetComponentInChildren<ISharingServiceObject>(includeInactive: true);
            }
            return result;
        }

        /// <summary>
        /// Execute ConfirmJoinRoom() and the given action.
        /// </summary>
        private async Task ConfirmJoinRoomWithAction(bool creatingRoom, Func<Task> action)
        {
            JoinRoomOptions option = await ConfirmJoinRoom(creatingRoom);
            if (option != JoinRoomOptions.Cancel)
            {
                bool savingDynamicObjects = option == JoinRoomOptions.JoinAndBringObjects;
                var sharingObjects = CaptureRootSharingObjects(savingDynamicObjects);

                // The original dynamic objects always need to be destroyed. If savingDynamicObjects is true,
                // the dynamic objects will be recreated once connected to a new sharing room/session.
                // This is done because the sharing service needs to create a new ID for the dynamic objects
                DestroyDynamicSharingObjects();

                await action();

                // Replay events on the sharing objects as needed
                ReplaySharingObjects(sharingObjects);
            }
        }

        /// <summary>
        /// Confirm the room should be joined, and whether or not to bring models with you.
        /// </summary>
        private async Task<JoinRoomOptions> ConfirmJoinRoom(bool creatingRoom)
        {
            if (!ValidateProviderReady())
            {
                return JoinRoomOptions.Cancel;
            }

            AppDialog.AppDialogResult dialogResult = AppDialog.AppDialogResult.No;
            int remoteObjects = Object.FindObjectsOfType<RemoteObject>().Length;
            if (remoteObjects > 0)
            {
                dialogResult = await ClearObjectsDialogController.ClearObjectsNeedsConfirmation(creatingRoom);
            }

            JoinRoomOptions option = JoinRoomOptions.Cancel;
            if (dialogResult != AppDialog.AppDialogResult.Cancel)
            {
                // Ok maps to bring objects, no maps to don't bring
                if (dialogResult == AppDialog.AppDialogResult.Ok)
                {
                    option = JoinRoomOptions.JoinAndBringObjects;
                }
                else
                {
                    option = JoinRoomOptions.JoinAndClearObjects;
                }
            }

            return option;
        }

        /// <summary>
        /// Wrap spawning data if the provider requires it.
        /// </summary>
        private object[] WrapSpawnData(object[] data)
        {
            object[] result = data;

            if (Provider != null && Provider.WrapSpawningData)
            {
                int length = data?.Length ?? 0;
                result = new object[length];
                for (int i = 0; i < length; i++)
                {
                    result[i] = _protocol.Wrap(ProtocolMessageType.SharingServiceSpawnParameter, data[i]);
                }
            }

            return result;
        }

        /// <summary>
        /// Unwrap spawning data if the provider requires it.
        /// </summary>
        private object[] UnwrapSpawnData(object[] data)
        {
            object[] result = data; 
            int length = data?.Length ?? 0;

            bool hasWrappedData = false;
            for (int i = 0; i < length; i++)
            {
                if (data[i] is ProtocolMessage)
                {
                    hasWrappedData = true;
                    break;
                }
            }

            if (hasWrappedData)
            {
                result = new object[length];
                for (int i = 0; i < length; i++)
                {
                    result[i] = _protocol.Unwrap(data[i]);
                }
            }

            return result;
        }

        /// <summary>
        /// Handle the provider sending a "Connected" event, and resend the event to this object's listeners.
        /// </summary>
        private void OnConnected(ISharingProvider provider)
        {
            _appContext.Send(contextState =>
            {
                Connected?.Invoke(this);
            }, null);
        }

        /// <summary>
        /// Handle the provider sending a "Connecting" event, and resend the event to this object's listeners.
        /// </summary>
        private void OnConnecting(ISharingProvider provider)
        {
            _appContext.Send(contextState =>
            {
                Connecting?.Invoke(this);
            }, null);
        }

        /// <summary>
        /// Handle the provider sending a "Disconnected" event, and resend the event to this object's listeners.
        /// </summary>
        private void OnDisconnected(ISharingProvider provider)
        {
            _appContext.Send(contextState =>
            {
                ClearKnownTargets();
                ForceClearPlayers();
                Disconnected?.Invoke(this); 
            }, null);
        }

        /// <summary>
        /// Handle provider status changes.
        /// </summary>
        private void OnStatusMessageChanged(ISharingProvider sender, string status)
        {
            _appContext.Send(contextState =>
            {
                StatusMessageChanged?.Invoke(this, status);
            }, null);
        }

        /// <summary>
        /// Handle the provider sending a "CurrentRoomChanged" event, and resend the event to this object's listeners.
        /// </summary>
        private void OnCurrentRoomChanged(ISharingProvider sender, ISharingServiceRoom currentRoom)
        {
            _appContext.Send(contextState =>
            {
                CurrentRoomChanged?.Invoke(this, currentRoom);
            }, null);
        }

        /// <summary>
        /// Handle a room invite
        /// </summary>
        private void OnRoomInviteReceived(ISharingProvider sender, ISharingServiceRoom room)
        {
            _appContext.Send(contextState =>
            {
                RoomInviteReceived?.Invoke(this, room);
            }, null);
        }

        /// <summary>
        /// Handle the provider sending a "RooomsChanged" event, and resend the event to this object's listeners.
        /// </summary>
        private void OnRoomsChanged(ISharingProvider sender, IReadOnlyCollection<ISharingServiceRoom> rooms)
        {
            _appContext.Send(contextState =>
            {
                RoomsChanged?.Invoke(this, rooms);
            }, null);
        }

        /// <summary>
        /// Handle the provider sending a "Messsage" event. If there is a target id, resend the event to the 
        /// corresponding share target. If there is no target id (aka global event), resend the event to this object's
        /// listeners.
        /// </summary>
        private void OnMessageReceived(ISharingProvider provider, ISharingServiceMessage message)
        {
            _appContext.Send(contextState =>
            {
                if (string.IsNullOrEmpty(message.Target))
                {
                    MessageReceived?.Invoke(this, message);
                }
                else
                {
                    var target = SharingServiceObjectModel.Create(this, CurrentRoom, message.Target);
                    target?.NotifyMessageReceived(message);
                }
            }, null);
        }

        /// <summary>
        /// Handle the provider sending a "transform" event. If there is a target id, resend the event to the 
        /// corresponding share target. If there is no target id (aka global event), the event is ignored.
        /// </summary>
        private void OnTransformMessageReceived(ISharingProvider sender, string targetId, SharingServiceTransform transform)
        {
            _appContext.Send(contextState =>
            {
                if (!string.IsNullOrEmpty(targetId))
                {
                    var target = SharingServiceObjectModel.Create(this, CurrentRoom, targetId);
                    target?.NotifyTransformMessageReceived(transform);
                }
            }, null);
        }

        /// <summary>
        /// Handle the provider sending a "PropertyChanged" event. The event will be redirected to the corresponding share target.
        /// The share target id is encoded into the property string (e.g. property == targetId.propertyName).
        /// </summary>
        private void OnPropertyChanged(ISharingProvider provider, string property, object value) 
        {
            OnPropertyChanged(property, value);
        }

        /// <summary>
        /// Handle property changes. The property change will be redirected to the corresponding share target.
        /// The share target id is encoded into the property string (e.g. property == targetId.propertyName).
        /// </summary>
        private void OnPropertyChanged(string property, object value)
        {
            _appContext.Send(contextState =>
            {
                TryRaiseTargetAdded(SharingServiceObjectModel.HandleProviderPropertyChanged(this, CurrentRoom, property, value));
            }, null);
        }

        /// <summary>
        /// Handle the provider sending a "PlayerAdded" event, and resend the event to this object's listeners.
        /// </summary>
        private void OnPlayerAdded(ISharingProvider sender, SharingServicePlayerData playerData)
        {
            SharingServicePlayer player = new SharingServicePlayer(this, playerData, sender.LocalPlayerId == playerData.PlayerId);
            player.SetProperty(SharableStrings.PlayerName, playerData.DisplayName);

            lock (_players)
            {
                _players[playerData.PlayerId] = player;
            }

            _appContext.Send(contextState =>
            {
                if (player.IsLocal)
                {
                    LocalPlayer = player;
                }

                PlayerAdded?.Invoke(this, player);
            }, null);
        }

        /// <summary>
        /// Handle the provider sending a "PlayerRemoved" event, and resend the event to this object's listeners.
        /// </summary>
        private void OnPlayerRemoved(ISharingProvider sender, SharingServicePlayerData playerData)
        {
            SharingServicePlayer player = null;
            lock (_players)
            {
                if (!string.IsNullOrEmpty(playerData.PlayerId) &&
                    _players.TryGetValue(playerData.PlayerId, out player))
                {
                    _players.Remove(playerData.PlayerId);
                }
            }

            if (player != null)
            {
                _appContext.Send(contextState =>
                {
                    PlayerRemoved?.Invoke(this, player);

                    if (LocalPlayer == player)
                    {
                        LocalPlayer = null;
                    }
                    player.Dispose();
                }, null);
            }
        }

        /// <summary>
        /// Handle the provider sending a "PlayerRotationChanged" event. Find the corresponding player object, and update its rotation information.
        /// </summary>
        private void OnPlayerPropertyChanged(ISharingProvider sender, string playerId, string property, object value)
        {
            SharingServicePlayer player;
            lock (_players)
            {
                _players.TryGetValue(playerId, out player);
            }

            if (player != null)
            {
                _appContext.Send(contextState =>
                {
                    player.ReceivedPropertiesChanged(property, value);
                    PlayerPropertyChanged?.Invoke(player, property, value);
                }, null);
            }
        }


        /// <summary>
        /// Handle the provider sending a "PlayerDisplayNameChanged" event. Find the corresponding player object, and update its rotation information.
        /// </summary>
        private void OnPlayerDisplayNameChanged(ISharingProvider sender, string playerId, string name)
        {
            SharingServicePlayer player;
            lock (_players)
            {
                _players.TryGetValue(playerId, out player);
            }

            if (player != null)
            {
                _appContext.Send(contextState =>
                {
                    player.ReceivedPlayerDisplayName(name);
                    PlayerDisplayNameChanged?.Invoke(player, name);
                }, null);
            }
        }

        /// <summary>
        /// Handle providers address changing.
        /// </summary>
        private void OnAddressChanged(ISharingProvider sendering, SharingServiceAddress address)
        {
            _appContext.Send(contextState =>
            {
                AddressChanged?.Invoke(this, address);
            }, null);
        }

        /// <summary>
        /// Handle user's changing at provider's primary address.
        /// </summary>

        private void OnAddressUsersChanged(ISharingProvider obj)
        {
            _appContext.Send(contextState =>
            {
                AddressUsersChanged?.Invoke(this);
            }, null);
        }

        /// <summary>
        /// Handle providers addresses changing.
        /// </summary>
        private void OnLocalAddressesChanged(ISharingProvider sendering, IReadOnlyList<SharingServiceAddress> addresses)
        {
            _appContext.Send(contextState =>
            {
                LocalAddressesChanged?.Invoke(this, addresses);
            }, null);
        }

        /// <summary>
        /// Handle audio setting changes.
        /// </summary>
        private void OnAudioSettingsChanged(ISharingProvider sender, SharingServiceAudioSettings settings)
        {
            AudioSettingsChanged?.Invoke(this, settings);
        }

        /// <summary>
        /// Handle avatar setting changes.
        /// </summary>
        private void OnAvatarSettingsChanged(ISharingProvider sender, SharingServiceAvatarSettings settings)
        {
            AvatarSettingsChanged?.Invoke(this, settings);
        }


        private void OnPingReturned(ISharingProvider sender, string senderId, SharingServicePingResponse response)
        {
            // request timeline  c-----s-d-|----e
            // response timeline         d c----e
            // d is the delta from the last point just before we send the data
            //     to the time the response is received on the same device
            _appContext.Send(contextState =>
            {
                if (_pingRequests.ContainsKey(senderId) && (_pingRequests[senderId].Id == BLAST_ID || _pingRequests[senderId].Id == response.Id))
                {
                    // get delta
                    var request = _pingRequests[senderId];
                    var delta = request.GetDelta(ref response);
                    var otherPlayer = Players.FirstOrDefault(player => player.Data.PlayerId == senderId);
                    if (otherPlayer != null)
                    { 
                        otherPlayer.SetProperty(SharableStrings.PlayerLatency, delta);
                    }

                    PingReturned?.Invoke(this, senderId, delta);
                }
                else
                {
                    // error state for a ping response we are not expecting
                    PingReturned?.Invoke(this, senderId, TimeSpan.MaxValue);
                }
            }, null);
        }

        /// <summary>
        /// Clear all players in sharing room. This is typically done during disconnection.
        /// </summary>
        private void ForceClearPlayers()
        {
            List<SharingServicePlayer> removed = new List<SharingServicePlayer>(_players.Count);
            lock (_players)
            {
                foreach (var player in _players)
                {
                    removed.Add(player.Value);
                }
                _players.Clear();
            }

            _appContext.Send(contextState =>
            {
                foreach (var player in removed)
                {
                    PlayerRemoved?.Invoke(this, player);

                    if (LocalPlayer == player)
                    {
                        LocalPlayer = null;
                    }

                    player.Dispose();
                }
            }, null);
        }

        /// <summary>
        /// Check if the provider has been initialized, and is ready to be consumed.
        /// </summary>
        private bool ValidateProviderReady()
        {
            if (Provider == null)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
        #endregion

        #region Private Class
        private class SharingServiceDynamicSpawn : MonoBehaviour
        {
            public GameObject OriginalPrefab { get; private set; }

            public object[] CreationData { get; private set; }

            /// <summary>
            /// Track this as a dynamic spawn, created by this client
            /// </summary>
            public static void Instance(GameObject target, GameObject originalPrefab, object[] data = null)
            {
                if (target != null)
                {
                    var spawn = target.EnsureComponent<SharingServiceDynamicSpawn>();
                    spawn.OriginalPrefab = originalPrefab;
                    spawn.CreationData = data;
                }
            }
        }

        private struct SpawnInformation
        {
            /// <summary>
            /// The original prefab
            /// </summary>
            public GameObject Original { get; set; }

            /// <summary>
            /// The original spawn data
            /// </summary>
            public object[] CreationData { get; set; }

            /// <summary>
            /// A list of properties to apply to this object
            /// </summary>
            public IReadOnlyDictionary<string, object> Properties { get; set; }

            /// <summary>
            /// The sharing id of the object
            /// </summary>
            public string SharingId { get; set; }
        }
        #endregion Private Class

        #region Private Enums
        private enum JoinRoomOptions
        {
            /// <summary>
            /// Cancel joining the sharing room/session
            /// </summary>
            Cancel,

            /// <summary>
            /// Join the sharing room/session and bring along the app's current sharable objects.
            /// </summary>
            JoinAndBringObjects,

            /// <summary>
            /// Join the sharing room/session and clear all the app's current sharable objects.
            /// </summary>
            JoinAndClearObjects,
        }
        #endregion Private Enums
    }
}
