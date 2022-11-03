// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if PHOTON_INSTALLED
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    public class PhotonProperties : IInRoomCallbacks, IDisposable
    {
        private PhotonSharingRoom _room;
        private PhotonParticipants _participants;
        private LogHelper<PhotonProperties> _logger = new LogHelper<PhotonProperties>();
        private Hashtable _setPropertyBuffer = new Hashtable(1);
        private Hashtable _expectedPropertyBuffer = new Hashtable(1);
        private ISharingServiceProtocol _protocol;
        private HashSet<string> _privateProperties = new HashSet<string>();
        private PhotonPropertyCache _roomCache;
        private Dictionary<int, PhotonPropertyCache> _playerCache = new Dictionary<int, PhotonPropertyCache>();

        #region Constructor
        private PhotonProperties(
            SharingServiceProfile settings,
            PhotonSharingRoom room,
            PhotonParticipants participants,
            ISharingServiceProtocol protocol)
        {
            _logger.Verbose = settings.VerboseLogging ? LogHelperState.Always : LogHelperState.Default;
            _room = room ?? throw new ArgumentNullException("Room can't be null.");
            _participants = participants ?? throw new ArgumentNullException("Participants can't be null.");
            _protocol = protocol ?? throw new ArgumentNullException("Protocol can't be null.");
            _roomCache = new PhotonPropertyCache(_room.Inner, _protocol);
            _participants.PlayerRemoved += OnPlayerRoomed;
            PhotonNetwork.AddCallbackTarget(this);
        }
        #endregion Constructor

        #region Public Events
        /// <summary>
        /// Event fired when a property changes.
        /// </summary>
        public event Action<PhotonProperties, string, object> PropertyChanged;

        /// <summary>
        /// Event fired when a private framework property changes.
        /// </summary>
        public event Action<PhotonProperties, string, object> PrivatePropertyChanged;

        /// <summary>
        /// Event fired when a participant property changes.
        /// </summary>
        public event Action<PhotonProperties, string, string, object> PlayerPropertyChanged;

        /// <summary>
        /// Event fired when a participant's display name has changed.
        /// </summary>
        public event Action<PhotonProperties, string, string> PlayerDisplayNameChanged;

        /// <summary>
        /// Event fired when a provate framework participant property changes.
        /// </summary>
        public event Action<PhotonProperties, string, string, object, object> PrivatePlayerPropertyChanged;
        #endregion Public Events

        #region Public Functions
        public static PhotonProperties CreateFromRoom(
            SharingServiceProfile settings,
            PhotonSharingRoom room,
            PhotonParticipants participants,
            ISharingServiceProtocol protocol)
        {
            return new PhotonProperties(settings, room, participants, protocol);
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
            _participants.PlayerRemoved -= OnPlayerRoomed;
        }

        /// <summary>
        /// Register a private property name that want be exposed to other parts of the application.
        /// </summary>
        public void RegisterPrivateProperty(string name)
        {
            _privateProperties.Add(name);
        }

        /// <summary>
        /// Set a session wide property.
        /// </summary>
        public void SetSessionProperty(string name, object value)
        {
            SetPhotonCustomProperty(_room.Inner.CustomProperties, _room.Inner.SetCustomProperties, name, value);
        }

        /// <summary>
        /// Set a session wide properties.
        /// </summary>
        public void SetSessionProperties(params object[] propertyNamesAndValues)
        {
            SetPhotonCustomProperty(_room.Inner.CustomProperties, _room.Inner.SetCustomProperties, propertyNamesAndValues);
        }

        /// <summary>
        /// Set the current player's property.
        /// </summary>
        public void SetSessionParticipantProperty(string name, object value)
        {
            SetSessionParticipantProperty(_participants.LocalParticipant, name, value);
        }

        /// <summary>
        /// Set a session player/participant's property.
        /// </summary>
        public void SetSessionParticipantProperty(string playerId, string name, object value)
        {
            PhotonParticipant participant;
            _participants.TryFind(playerId, out participant);
            SetSessionParticipantProperty(participant, name, value);
        }

        /// <summary>
        /// Set a session player/participant's property.
        /// </summary>
        public void SetSessionParticipantProperty(PhotonParticipant participant, string name, object value)
        {
            SetSessionParticipantProperty(participant.Inner, name, value);
        }

        /// <summary>
        /// Set a session player/participant's property.
        /// </summary>
        public void SetSessionParticipantProperty(Player participant, string name, object value)
        {
            if (participant != null)
            {
                LogVerbose("SetPlayerProperty() {1} = {2} ({0})", participant.ActorNumber, name, value);
                SetPhotonCustomProperty(participant.CustomProperties, participant.SetCustomProperties, name, value);
            }
            else
            {
                LogError("SetPlayerProperty() error, unable to find player id. {1} = {2} ({0})", participant.ActorNumber, name, value);
            }
        }

        /// <summary>
        /// Does this session have the current property
        /// </summary>
        public bool HasSessionProperty(string name)
        {
            return HasProperty(_room.Inner.CustomProperties, name);
        }

        /// <summary>
        /// Try to get a sharing service's property value.
        /// </summary>
        /// <returns>True if a non-null property value was found.</returns>
        public bool TryGetSessionProperty(string name, out object value)
        {
            return _roomCache.TryGet(name, out value);
        }

        /// <summary>
        /// Does this session have the current property
        /// </summary>
        public bool HasSessionParticipantProperty(string participantId, string name)
        {
            return HasSessionParticipantProperty(FindParticipant(participantId), name);
        }

        /// <summary>
        /// Does this session have the current property
        /// </summary>
        public bool HasSessionParticipantProperty(Player participant, string name)
        {
            return HasProperty(participant?.CustomProperties, name);
        }

        /// <summary>
        /// Try to get a sharing service's property value for partipant.
        /// </summary>
        /// <returns>True if a non-null property value was found.</returns>
        public bool TryGetSessionParticipantProperty(string participantId, string name, out object value)
        {
            return TryGetSessionParticipantProperty(FindParticipant(participantId), name, out value);
        }

        /// <summary>
        /// Try to get a sharing service's property value for partipant.
        /// </summary> 
        /// <returns>True if a non-null property value was found.</returns>
        public bool TryGetSessionParticipantProperty(Player participant, string name, out object value)
        {
            return GetPlayerCache(participant).TryGet(name, out value);
        }

        /// <summary>
        /// Clear all properties with the given prefix.
        /// </summary>
        public void ClearSessionPropertiesStartingWith(string prefix)
        {
            Hashtable properties = _room.Inner.CustomProperties;
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
        /// Replay all property change events
        /// </summary>
        public void ReplayPropertyChangeEvents()
        {
            ReceivedRoomPropertiesChanged(_room.Inner?.CustomProperties);

            if (_participants.Participants != null)
            {
                foreach (var participant in _participants.Participants)
                {
                    ReceivedPlayerPropertiesChanged(participant.Inner, participant.Inner.CustomProperties);

                    // Replay player names. Since they may have changed while joining a room,
                    // and consumers may have missed the change.
                    var innerProperties = new Hashtable();
                    innerProperties.Add(ActorProperties.PlayerName, participant.Inner.NickName);
                    ReceivedPlayerPropertiesChanged(participant.Inner, innerProperties);
                }
            }
        }
        #endregion Public Function

        #region IInRoomCallbacks
        /// <summary>
        /// Called after switching to a new MasterClient when the current one leaves.
        /// </summary>
        public void OnMasterClientSwitched(Player newMasterClient)
        {
            //LogVerbose("OnMasterClientSwitched() (master player id = {0})", newMasterClient.ActorNumber);
        }

        /// <summary>
        /// Called when a remote player entered the room. This Player is already added to the playerlist.
        /// </summary>
        public void OnPlayerEnteredRoom(Player newPlayer)
        {
            LogVerbose("OnPlayerEnteredRoom() (player id = {0})", newPlayer.ActorNumber);
            ReceivedPlayerPropertiesChanged(newPlayer, newPlayer.CustomProperties);
        }

        /// <summary>
        /// Called when a remote player left the room or became inactive. Check otherPlayer.IsInactive.
        /// </summary>
        public void OnPlayerLeftRoom(Player otherPlayer)
        {
            //LogVerbose("OnPlayerLeftRoom() (player id = {0})", otherPlayer.ActorNumber);
        }

        /// <summary>
        /// Called when custom player-properties are changed. Player and the changed properties are passed as object[].
        /// </summary>
        public void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable propertiesThatChanged)
        {
            LogVerbose("OnPlayerPropertiesUpdate() (player id = {0})", targetPlayer.ActorNumber);
            ReceivedPlayerPropertiesChanged(targetPlayer, propertiesThatChanged);
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

        #region Private Functions
        /// <summary>
        /// Find the participant in the room.
        /// </summary>
        /// <param name="participantId"></param>
        /// <returns></returns>
        private Player FindParticipant(string participantId)
        {
            Player player;
            _room.Inner.Players.TryGetValue(PhotonHelpers.UserIdFromString(participantId), out player);
            return player;
        }

        /// <summary>
        /// Does this hashtable have the current property
        /// </summary>
        private bool HasProperty(Hashtable properties, string name)
        {
            if (string.IsNullOrEmpty(name) || properties == null)
            {
                return false;
            }
            else
            {
                object value;
                return properties.TryGetValue(name, out value) && value != null;
            }
        }

        /// <summary>
        /// Set a Photon custom properties using the given 'setter' action.
        /// </summary>
        /// <param name="propertyNamesAndValues">
        /// A collection of property names and values, alterating between name and value. The first value is a property
        /// name, the next is its value, and soon on.
        /// </param>
        private void SetPhotonCustomProperty(
            Hashtable properties,
            Func<Hashtable, Hashtable, WebFlags, bool> setter, 
            params object[] propertyNamesAndValues)
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
                    object oldValue;
                    string propertyName = propertyNamesAndValues[i] as string;
                    object newValue = propertyNamesAndValues[i + 1];

                    if (propertyName == null)
                    {
                        continue;
                    }

                    // perform our own serialization
                    newValue = _protocol.SerializeToString(newValue);

                    // search for current known value
                    if (properties.TryGetValue(propertyName, out oldValue))
                    {
                        LogVerbose("SetPhotonCustomProperty() {0} = {1} -> {2}", propertyName, oldValue, newValue);
                        _expectedPropertyBuffer[propertyName] = oldValue;
                    }
                    else
                    {
                        LogVerbose("SetPhotonCustomProperty() {0} = {1}", propertyName, newValue);
                    }

                    properties[propertyName] = newValue;
                    _setPropertyBuffer[propertyName] = newValue;
                }

                setter(_setPropertyBuffer, _expectedPropertyBuffer, null);
            }
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
            var propertyNames = propertiesThatChanged.Keys.ToArray();
            foreach (var propertyNameObject in propertyNames)
            {
                string propertyName = propertyNameObject as string;
                if (propertyName == null)
                {
                    continue;
                }

                object encodedValue = propertiesThatChanged[propertyName];
                LogVerbose("ReceivedRoomPropertiesChanged() {0} = {1}", propertyName, encodedValue);

                object propertyValue;
                if (_protocol.DeserializeFromString(encodedValue as string, out propertyValue))
                {
                    _roomCache.Set(propertyName, propertyValue);
                    if (_privateProperties.Contains(propertyName))
                    {
                        PrivatePropertyChanged?.Invoke(this, propertyName, propertyValue);
                    }
                    else
                    {
                        PropertyChanged?.Invoke(this, propertyName, propertyValue);
                    }
                }
            }
        }

        /// <summary>
        /// Handle a set of room property changes, and raise 'player property changed' events. 
        /// </summary>
        private void ReceivedPlayerPropertiesChanged(Player player, Hashtable propertiesThatChanged)
        {
            if (player == null)
            {
                return;
            }

            // copy keys as the dictionary can change during callbacks
            var propertyKeys = propertiesThatChanged.Keys.ToArray();
            foreach (var propertyKey in propertyKeys)
            {
                if (propertyKey is string)
                {
                    ReceivedPlayerPropertyChanged(player, (string)propertyKey, propertiesThatChanged[propertyKey]);
                }
                else if (propertyKey is byte)
                {
                    ReceivedPlayerSpecialPropertyChanged(player, (byte)propertyKey, propertiesThatChanged[propertyKey]);
                }
            }
        }

        /// <summary>
        /// Handle a set of room property changes, and raise 'player property changed' events. 
        /// </summary>
        private void ReceivedPlayerPropertyChanged(Player player, string propertyName, object encodedValue)
        {
            LogVerbose("ReceivedPlayerPropertyChanged() {1} = {2} ({0})", player.ActorNumber, propertyName, encodedValue);

            object propertyValue;
            if (_protocol.DeserializeFromString(encodedValue as string, out propertyValue))
            {
                var playerCache = GetPlayerCache(player);
                playerCache.TryGet(propertyName, out object propertyValueOld);
                playerCache.Set(propertyName, propertyValue);
                if (_privateProperties.Contains(propertyName))
                {
                    PrivatePlayerPropertyChanged?.Invoke(this, PhotonHelpers.UserIdToString(player), propertyName, propertyValue, propertyValueOld);
                }
                else
                {
                    PlayerPropertyChanged?.Invoke(this, PhotonHelpers.UserIdToString(player), propertyName, propertyValue);
                }
            }
        }

        /// <summary>
        /// Handle Photon specialized player properties. 
        /// </summary>
        private void ReceivedPlayerSpecialPropertyChanged(Player player, byte key, object value)
        {
            switch (key)
            {
                case ActorProperties.PlayerName:
                    PlayerDisplayNameChanged?.Invoke(this, PhotonHelpers.UserIdToString(player), value as string);
                    break;
            }
        }

        private PhotonPropertyCache GetPlayerCache(Player player)
        {
            PhotonPropertyCache result = null;
            if (player != null)
            {
                lock (_playerCache)
                {
                    if (!_playerCache.TryGetValue(player.ActorNumber, out result))
                    {
                        result = new PhotonPropertyCache(player, _protocol);
                        _playerCache[player.ActorNumber] = result;
                    }
                }
            }
            else
            {
                LogError("Creating cache for a null player");
                result = new PhotonPropertyCache(player, _protocol);
            }

            return result;
        }

        private void RemovePlayerCache(Player player)
        {
            if (player != null)
            {
                lock (_playerCache)
                {
                    _playerCache.Remove(player.ActorNumber);
                }
            }
        }

        private void OnPlayerRoomed(PhotonParticipants sender, PhotonParticipant player)
        {
            RemovePlayerCache(player.Inner);
        }
        #endregion Private Functions

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

        #region Private Classes
        private class PhotonPropertyCache
        {
            private ISharingServiceProtocol _protocol;
            private Room _room;
            private Player _player;
            private Dictionary<string, object> _cache = new Dictionary<string, object>();

            /// <summary>
            /// Create a cache for a photon room.
            /// </summary>
            public PhotonPropertyCache(Room room, ISharingServiceProtocol protocol)
            {
                _room = room;
                _protocol = protocol;
            }

            /// <summary>
            /// Create a cache for a photon player.
            /// </summary>
            public PhotonPropertyCache(Player player, ISharingServiceProtocol protocol)
            {
                _player = player;
                _protocol = protocol;
            }

            /// <summary>
            /// Try getting an unencoded property by name.
            /// </summary>
            public bool TryGet(string name, out object value)
            {
                bool result = false;
                lock (_cache)
                {
                    if (_cache.TryGetValue(name, out value))
                    {
                        result = true;
                    }
                    else
                    {
                        object encoded;
                        if (GetHashtable().TryGetValue(name, out encoded) &&
                            _protocol.DeserializeFromString(encoded as string, out value))
                        {
                            result = true;
                            _cache[name] = value;
                        }
                    }
                }
                return result;
            }

            /// <summary>
            /// Set a cache value. If null, the cache item is removed.
            /// </summary>
            public void Set(string name, object value)
            {
                lock (_cache)
                {
                    if (name != null)
                    {
                        if (value == null)
                        {
                            _cache.Remove(name);
                        }
                        else
                        {
                            _cache[name] = value;
                        }
                    }
                }

            }

            /// <summary>
            /// Get the inner hashtable to use for cache misses.
            /// </summary>
            private Hashtable GetHashtable()
            {
                if (_player != null)
                {
                    return _player.CustomProperties;
                }
                else
                {
                    return _room.CustomProperties;
                }
            }
        }
        #endregion Private Classes 
    }
}
#endif
