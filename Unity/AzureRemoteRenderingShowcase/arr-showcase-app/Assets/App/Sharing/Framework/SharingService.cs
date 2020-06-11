// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication;
using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// A service for sharing application state across other clients.
    /// </summary>
    [MixedRealityExtensionService(SupportedPlatforms.WindowsStandalone | SupportedPlatforms.WindowsUniversal | SupportedPlatforms.WindowsEditor)]
    public class SharingService : BaseExtensionService, ISharingService, IMixedRealityExtensionService
    {
        private SharingServiceProfile _defaultProfile;
        private SharingServiceProfile _loadedProfile;
        private HashSet<ISharingServiceTarget> _targets = new HashSet<ISharingServiceTarget>();
        private Dictionary<int, SharingServicePlayer> _players = new Dictionary<int, SharingServicePlayer>();
        private ISharingServicePlayer _localPlayer;
        private SynchronizationContext _appContext;

        public SharingService(string name, uint priority, BaseMixedRealityProfile profile)
            : base(name, priority, profile)
        {
            _defaultProfile = profile as SharingServiceProfile;
            if (_defaultProfile == null)
            {
                _defaultProfile = ScriptableObject.CreateInstance<SharingServiceProfile>();
            }
        }

        #region ISharingService Properties
        /// <summary>
        /// True if connected to a session and able to communicate with other clients
        /// </summary>
        public bool IsConnected => Provider?.IsConnected ?? false;

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
        /// Get the current room.
        /// </summary>
        public ISharingServiceRoom CurrentRoom => Provider?.CurrentRoom;
        #endregion ISharingService Properties

        #region Private Properties
        /// <summary>
        /// Get the sharing provider.
        /// </summary>
        private ISharingProvider Provider { get; set; }
        #endregion Private Properties

        #region ISharingService Events
        /// <summary>
        /// Event fired when the service is connected
        /// </summary>
        public event Action<ISharingService> Connected;

        /// <summary>
        /// Event fired when the service disconnects
        /// </summary>
        public event Action<ISharingService> Disconnected;

        /// <summary>
        /// Event fired when the current room has changed.
        /// </summary>
        public event Action<ISharingService, ISharingServiceRoom> CurrentRoomChanged;

        /// <summary>
        /// Event fired when the rooms have changed.
        /// </summary>
        public event Action<ISharingService, IReadOnlyCollection<ISharingServiceRoom>> RoomsChanged;

        /// <summary>
        /// Event fired when a message is received from a remote client
        /// </summary>
        public event Action<ISharingService, ISharingServiceMessage> MessageReceived;
 
        /// <summary>
        /// A specialized message optimized for sending an array of floats to a target.
        /// </summary>
        public event Action<ISharingProvider, ISharingServiceTarget, float[]> NumericMessageReceived;

        /// <summary>
        /// Event fired when a new sharing target has been added 
        /// </summary>
        public event Action<ISharingService, ISharingServiceTarget> TargetAdded;

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
        #endregion ISharingService Events

        #region ISharingService Methods
        /// <summary>
        /// Connects to the session
        /// </summary>
        public void Connect()
        {
            if (ValidateProviderReady())
            {
                Provider.Connect();
            }
        }

        /// <summary>
        /// Create and join a new sharing room.
        /// </summary>
        public void CreateAndJoinRoom()
        {
            if (ValidateProviderReady())
            {
                Provider.CreateAndJoinRoom();
            }
        }

        /// <summary>
        /// Join the given room.
        /// </summary>
        public void JoinRoom(ISharingServiceRoom room)
        {
            if (ValidateProviderReady())
            {
                Provider.JoinRoom(room);
            }
        }

        /// <summary>
        /// Leave the currently joined sharing room, and join the default lobby.
        /// </summary>
        public void LeaveRoom()
        {
            if (ValidateProviderReady())
            {
                Provider.LeaveRoom();
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
        public void SetPlayerProperty(int playerId, string key, object value)
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
        public bool TryGetPlayerProperty(int playerId, string key, out object value)
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

        // <summary>
        /// Does this provider have the current property for the player.
        /// </summary>
        public bool HasPlayerProperty(int playerId, string property)
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
        /// Send the local player's position and rotation
        /// </summary>
        public void SendLocalPlayerPose(Pose pose)
        {
            if (ValidateProviderReady())
            {
                Provider.SendLocalPlayerPose(pose);
            }
        }

        /// <summary>
        /// Create a unique target object that will be synced across clients. 
        /// A unique identification will be automatically created once client
        /// connects to the sharing service.
        /// </summary>
        public ISharingServiceTarget CreateTarget(SharingServiceTargetType type)
        {
            return SharingServiceTarget.Create(
                this, new SharingServiceTarget.Identification()
                {
                    Type = type
                });
        }

        /// <summary>
        /// Create a target object, with a given label, that will be synced across clients.
        /// </summary>
        public ISharingServiceTarget CreateTarget(SharingServiceTargetType type, string label)
        {
            return SharingServiceTarget.Create(
                this, new SharingServiceTarget.Identification()
                {
                    Type = type,
                    Label = label
                });
        }

        /// <summary>
        /// Create a target object, from a sharing id.
        /// </summary>
        public ISharingServiceTarget CreateTargetFromSharingId(string sharingId)
        {
            return SharingServiceTarget.Create(this, sharingId);
        }
        #endregion ISharingService Methods

        #region BaseExtensionService Methods
        /// <summary>
        /// Initialize the sharing service, and initialize the internal sharing provider. A provider is a wrapper around a 
        /// praticular networking service. Currently Photon is the only supported provider.
        /// </summary>
        public override async void Initialize() 
        {
            _appContext = SynchronizationContext.Current;
            _loadedProfile = await SharingServiceProfileLoader.Load(_defaultProfile);

            if (_loadedProfile.Provider == SharingServiceProfile.ProviderService.Photon &&
                Application.isPlaying)
            {
                Provider = CreatePhotonProvider();
            }

            if (Provider != null)
            { 
                Provider.Connected += OnConnected;
                Provider.Disconnected += OnDisconnected;
                Provider.RoomsChanged += OnRoomsChanged;
                Provider.CurrentRoomChanged += OnCurrentRoomChanged;
                Provider.MessageReceived += OnMessageReceived;
                Provider.TransformMessageReceived += OnTransformMessageReceived;
                Provider.PropertyChanged += OnPropertyChanged;
                Provider.PlayerAdded += OnPlayerAdded;
                Provider.PlayerRemoved += OnPlayerRemoved;
                Provider.PlayerPoseChanged += OnPlayerPoseChanged;
                Provider.PlayerPropertyChanged += OnPlayerPropertyChanged;

                if (_loadedProfile.AutoConnect)
                {
                    Connect();
                }
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
        /// Destroy the internal provider class.
        /// </summary>
        public override void Destroy()
        {
            if (Provider != null)
            {
                Provider.Connected -= OnConnected;
                Provider.Disconnected -= OnDisconnected;
                Provider.RoomsChanged -= OnRoomsChanged;
                Provider.CurrentRoomChanged -= OnCurrentRoomChanged;
                Provider.MessageReceived -= OnMessageReceived;
                Provider.TransformMessageReceived -= OnTransformMessageReceived;
                Provider.PropertyChanged -= OnPropertyChanged;
                Provider.PlayerAdded -= OnPlayerAdded;
                Provider.PlayerRemoved -= OnPlayerRemoved;
                Provider.PlayerPoseChanged -= OnPlayerPoseChanged;
                Provider.Disconnect();
                Provider = null;
            }
        }
        #endregion BaseExtensionService Methods

        #region Private Methods
        /// <summary>
        /// Create a Photon sharing provider if supported.
        /// </summary>
        /// <returns>A sharing provider if Photon is installed, otherwise returns null.</returns>
        private ISharingProvider CreatePhotonProvider()
        {
#if PHOTON_INSTALLED
            return new Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon.PhotonSharingProvider(_loadedProfile);
#else
            var msg = "Photon is not installed. Inorder for the multi-user experence to function, please install the Photon PUN SDK. See this sample's documentation for details.";
            AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}", msg);
            return null;
#endif // PHOTON_INSTALLED
        }
        /// <summary>
        /// If the target is not known by this class, add it to the class's cache and raise an added event.
        /// </summary>
        private ISharingServiceTarget TryRaiseTargetAdded(ISharingServiceTarget target)
        {
            if (target != null && !_targets.Contains(target))
            {
                _targets.Add(target); 
                TargetAdded?.Invoke(this, target);
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

        private void ClearKnownPlayers()
        {
            lock (_players)
            {
            }
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
        /// Handle the provider sending a "Disconnected" event, and resend the event to this object's listeners.
        /// </summary>
        private void OnDisconnected(ISharingProvider provider)
        {
            _appContext.Send(contextState =>
            {
                ClearKnownTargets();
                Disconnected?.Invoke(this); 
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
                    var target = SharingServiceTarget.Create(this, message.Target);
                    target?.NotifyMessageReceived(message);
                }
            }, null);
        }

        /// <summary>
        /// Handle the provider sending a "transofmr" event. If there is a target id, resend the event to the 
        /// corresponding share target. If there is no target id (aka global event), the event is ignored.
        /// </summary>
        private void OnTransformMessageReceived(ISharingProvider sender, string targetId, SharingServiceTransform transform)
        {
            _appContext.Send(contextState =>
            {
                if (!string.IsNullOrEmpty(targetId))
                {
                    var target = SharingServiceTarget.Create(this, targetId);
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
            _appContext.Send(contextState =>
            {
                TryRaiseTargetAdded(SharingServiceTarget.HandleProviderPropertyChanged(this, property, value));
            }, null);
        }

        /// <summary>
        /// Handle the provider sending a "PlayerAdded" event, and resend the event to this object's listeners.
        /// </summary>
        private void OnPlayerAdded(ISharingProvider sender, int playerId)
        {
            SharingServicePlayer player = new SharingServicePlayer(this, playerId, sender.LocalPlayerId == playerId);
            lock (_players)
            {
                _players[playerId] = player;
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
        private void OnPlayerRemoved(ISharingProvider sender, int playerId)
        {
            SharingServicePlayer player;
            lock (_players)
            {
                if (_players.TryGetValue(playerId, out player))
                {
                    _players.Remove(playerId);
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
        /// Handle the provider sending a "PlayerTransformChanged" event. Find the corresponding player object, and update its transform information.
        /// </summary>
        private void OnPlayerPoseChanged(ISharingProvider sender, int playerId, Pose pose)
        {
            SharingServicePlayer player;
            lock (_players)
            {
                _players.TryGetValue(playerId, out player);
            }

            if (player != null)
            {
                player.ReceivedPose(pose);
            }
        }

        /// <summary>
        /// Handle the provider sending a "PlayerRotationChanged" event. Find the corresponding player object, and update its rotation information.
        /// </summary>
        private void OnPlayerPropertyChanged(ISharingProvider sender, int playerId, string property, object value)
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
                }, null);

                PlayerPropertyChanged?.Invoke(player, property, value);
            }
        }

        /// <summary>
        /// Check if the provider has been initialized, and is ready to be consumed.
        /// </summary>
        private bool ValidateProviderReady()
        {
            if (Provider == null)
            {
                Debug.LogWarning("Sharing service is still starting up. Unable to complete request.");
                return false;
            }
            else
            {
                return true;
            }
        }
        #endregion
    }
}
