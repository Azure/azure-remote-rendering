// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if PHOTON_INSTALLED
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections.Generic;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    public class PhotonParticipants :
        IInRoomCallbacks,
        IDisposable
    {
        private PhotonSharingRoom _room;
        private Dictionary<int, PhotonParticipant> _participants = new Dictionary<int, PhotonParticipant>();
        private LogHelper<PhotonParticipants> _logger = new LogHelper<PhotonParticipants>();

        #region Constructor
        private PhotonParticipants(SharingServiceProfile settings, PhotonSharingRoom room)
        {
            _logger.Verbose = settings.VerboseLogging ? LogHelperState.Always : LogHelperState.Default;
            _room = room ?? throw new ArgumentNullException("Room can't be null.");
            PhotonNetwork.AddCallbackTarget(this);

            OnPlayerEnteredRoom(PhotonNetwork.LocalPlayer);
            var players = _room.Inner?.Players;
            if (players != null)
            {
                foreach (var player in _room.Inner.Players)
                {
                    OnPlayerEnteredRoom(player.Value);
                }
            }
        }
        #endregion Constructor

        #region Public Properties
        /// <summary>
        /// Get the current set of participants
        /// </summary>
        public IReadOnlyCollection<PhotonParticipant> Participants => _participants.Values;

        /// <summary>
        /// Get the local participant.
        /// </summary>
        public PhotonParticipant LocalParticipant { get; private set; }

        /// <summary>
        /// Get the local participant actor number
        /// </summary>
        public int LocalActorNumber => PhotonNetwork.LocalPlayer?.ActorNumber ?? -1;
        #endregion Public Properties

        #region Public Events
        /// <summary>
        /// Event fired when a new player has been added.
        /// </summary>
        public event Action<PhotonParticipants, PhotonParticipant> PlayerAdded;

        /// <summary>
        /// Event fired when a player has been removed.
        /// </summary>
        public event Action<PhotonParticipants, PhotonParticipant> PlayerRemoved;
        #endregion Public Events

        #region Public Functions
        /// <summary>
        /// Create an new participant manager.
        /// </summary>
        /// <remarks>
        /// This may become async in the future. Hence a factory function.
        /// </remarks>
        public static PhotonParticipants CreateFromRoom(SharingServiceProfile settings, PhotonSharingRoom room)
        {
            return new PhotonParticipants(settings, room);
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public void Dispose()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
        }

        /// <summary>
        /// Replay all player adds
        /// </summary>
        public void ReplayPlayerAddedEvents()
        {
            List<PhotonParticipant> added;
            lock (_participants)
            {
                LogVerbose("ReplayPlayerAddedEvents() (players = {0})", _participants.Count);

                added = new List<PhotonParticipant>(_participants.Count);
                foreach (var entry in _participants)
                {
                    added.Add(entry.Value);
                }
            }

            foreach (var entry in added)
            {
                PlayerAdded?.Invoke(this, entry);
            }
        }

        /// <summary>
        /// Try to find participant by id
        /// </summary>
        public bool TryFind(string id, out PhotonParticipant participant)
        {
            return TryFind(PhotonHelpers.UserIdFromString(id), out participant);
        }

        /// <summary>
        /// Try to find participant by id
        /// </summary>
        public bool TryFind(int id, out PhotonParticipant participant)
        {
            if (id == LocalActorNumber)
            {
                id = -1;
            }

            lock (_participants)
            {
                return _participants.TryGetValue(id, out participant);
            }
        }
        #endregion Public Functions

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
            PhotonParticipant participant;
            if (!newPlayer.IsLocal)
            {
                participant = new PhotonParticipant(newPlayer);
                lock (_participants)
                {
                    _participants[newPlayer.ActorNumber] = participant;
                }
            }
            else
            {
                lock (_participants)
                {
                    LocalParticipant = participant = new PhotonParticipant(newPlayer);
                    _participants[-1] = LocalParticipant;
                }
            }
            PlayerAdded?.Invoke(this, participant);
        }

        /// <summary>
        /// Called when a remote player left the room or became inactive. Check otherPlayer.IsInactive.
        /// </summary>
        public void OnPlayerLeftRoom(Player otherPlayer)
        {
            LogVerbose("OnPlayerLeftRoom() (player id = {0})", otherPlayer.ActorNumber);
            if (!otherPlayer.IsLocal)
            {
                bool removed;
                PhotonParticipant participant;
                lock (_participants)
                {
                    removed = _participants.TryGetValue(otherPlayer.ActorNumber, out participant);
                }

                if (removed)
                {
                    PlayerRemoved?.Invoke(this, participant);
                }
            }
        }

        /// <summary>
        /// Called when custom player-properties are changed. Player and the changed properties are passed as object[].
        /// </summary>
        public void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable propertiesThatChanged)
        {
            //LogVerbose("OnPlayerPropertiesUpdate() (player id = {0})", targetPlayer.ActorNumber);
        }

        /// <summary>
        /// Called when a room's custom properties changed. The propertiesThatChanged contains all that was set via Room.SetCustomProperties.
        /// </summary>
        public void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
            //LogVerbose("OnRoomPropertiesUpdate() (room = {0})", PhotonNetwork.CurrentRoom.Name);
        }
        #endregion IInRoomCallbacks

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
    }
}

#endif // PHOTON_INSTALLED