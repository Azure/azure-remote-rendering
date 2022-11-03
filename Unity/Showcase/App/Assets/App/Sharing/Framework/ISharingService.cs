// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// A service for sharing application state across other clients.
    /// </summary>
    public interface ISharingService : IMixedRealityExtensionService
    {
        /// <summary>
        /// True if service is ready for use.
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// True if connected to a session and able to communicate with other clients
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Get if the service is connecting
        /// </summary>
        bool IsConnecting { get; }

        /// <summary>
        /// True if connected to sharing service and logged in. But not necessarily in a session
        /// </summary>
        bool IsLoggedIn { get; }

        /// <summary>
        /// Get the service's status message
        /// </summary>
        string StatusMessage { get; }

        /// <summary>
        /// Get all known sharing targets.
        /// </summary>
        IReadOnlyCollection<ISharingServiceObject> Targets { get; }

        /// <summary>
        /// The list of current rooms.
        /// </summary>
        IReadOnlyCollection<ISharingServiceRoom> Rooms { get; }

        /// <summary>
        /// The list of current players.
        /// </summary>
        IReadOnlyCollection<ISharingServicePlayer> Players { get; }

        /// <summary>
        /// The local player.
        /// </summary>
        ISharingServicePlayer LocalPlayer { get; }

        /// <summary>
        /// Get the invalid player id
        /// </summary>
        string InvalidPlayerId { get; }

        /// <summary>
        /// Get the current room.
        /// </summary>
        ISharingServiceRoom CurrentRoom { get; }

        /// <summary>
        /// Get if the sharing service's configuration supports private sharing session/rooms.
        /// </summary>
        bool HasPrivateRooms { get; }

        /// <summary>
        /// The user's current location id.
        /// </summary>
        SharingServiceAddress PrimaryAddress { get; }

        /// <summary>
        /// Get the known addresses in the user's local space.
        /// </summary>
        IReadOnlyList<SharingServiceAddress> LocalAddresses { get; }

        /// <summary>
        /// Get the container for all sharing related game objects, such as new avatars. Avatar positioning will be relative to this container.
        /// This must be setting before joining a sharing room. If not set, avatars will not appear.
        /// </summary>
        GameObject Root { get; }

        /// <summary>
        /// Get or set the sharing service's audio settings.
        /// </summary>
        SharingServiceAudioSettings AudioSettings { get; set; }

        /// <summary>
        /// Get the sharing service's audio capabilities.
        /// </summary>
        SharingServiceAudioCapabilities AudioCapabilities { get; }

        /// <summary>
        /// Get or set the sharing service's avatar settings
        /// </summary>
        SharingServiceAvatarSettings AvatarSettings { get; set; }

        /// <summary>
        /// Event fired when the service is connected to a session.
        /// </summary>
        event Action<ISharingService> Connected;

        /// <summary>
        /// Event fired when the service is connecting
        /// </summary>
        event Action<ISharingService> Connecting;

        /// <summary>
        /// Event fired when the service disconnects from a session
        /// </summary>
        event Action<ISharingService> Disconnected;

        /// <summary>
        /// Event fired when the service's status message has changed
        /// </summary>
        event Action<ISharingService, string> StatusMessageChanged;

        /// <summary>
        /// Event fired when the current room has changed.
        /// </summary>
        event Action<ISharingService, ISharingServiceRoom> CurrentRoomChanged;

        /// <summary>
        /// Event fired when the rooms have changed.
        /// </summary>
        event Action<ISharingService, IReadOnlyCollection<ISharingServiceRoom>> RoomsChanged;

        /// <summary>
        /// Event fired when a message is received from a remote client
        /// </summary>
        event Action<ISharingService, ISharingServiceMessage> MessageReceived;

        /// <summary>
        /// Event fired when an invitation is received.
        /// </summary>
        event Action<ISharingService, ISharingServiceRoom> RoomInviteReceived;

        /// <summary>
        /// Event fired when a new sharing object has been added
        /// </summary>
        event Action<ISharingService, ISharingServiceObject> ObjectAdded;

        /// <summary>
        /// Event fired when the local player object has changed
        /// </summary>
        event Action<ISharingService, ISharingServicePlayer> LocalPlayerChanged;

        /// <summary>
        /// Event fired when a new player has been added.
        /// </summary>
        event Action<ISharingService, ISharingServicePlayer> PlayerAdded;

        /// <summary>
        /// Event fired when a new player has been removed.
        /// </summary>
        event Action<ISharingService, ISharingServicePlayer> PlayerRemoved;

        /// <summary>
        /// Event fired when a player property has changed
        /// </summary>
        event Action<ISharingServicePlayer, string, object> PlayerPropertyChanged;

        /// <summary>
        /// Event fired when a player display name has changed
        /// </summary>
        event Action<ISharingServicePlayer, string> PlayerDisplayNameChanged;

        /// <summary>
        /// Event fired when a user's address has changed.
        /// </summary>
        event Action<ISharingService, SharingServiceAddress> AddressChanged;

        /// <summary>
        /// Event fired when the users at the user's address have changed.
        /// </summary>
        event Action<ISharingService> AddressUsersChanged;

        /// <summary>
        /// Event fired when a user's local addresses have changed.
        /// </summary>
        event Action<ISharingService, IReadOnlyList<SharingServiceAddress>> LocalAddressesChanged;

        /// <summary>
        /// Event fired when a ping response has been received
        /// </summary>
        event Action<ISharingService, string, TimeSpan> PingReturned;

        /// <summary>
        /// Event fired when audio settings changed.
        /// </summary>
        event Action<ISharingService, SharingServiceAudioSettings> AudioSettingsChanged;

        /// <summary>
        /// Event fired when avatar settings changed.
        /// </summary>
        event Action<ISharingService, SharingServiceAvatarSettings> AvatarSettingsChanged;

        /// <summary>
        /// Connects to the sharing service
        /// </summary>
        void Login();

        /// <summary>
        /// Create and join a new sharing room.
        /// </summary>
        Task CreateAndJoinRoom();

        /// <summary>
        /// Create and join a new private sharing room. Only the given list of players can join the room.
        /// </summary>
        Task CreateAndJoinRoom(IEnumerable<SharingServicePlayerData> inviteList);

        /// <summary>
        /// Join the given room.
        /// </summary>
        void JoinRoom(ISharingServiceRoom room);

        /// <summary>
        /// Join the given room by room id
        /// </summary>
        void JoinRoom(string roomId);

        /// <summary>
        /// Decline a room/session invitation.
        /// </summary>
        void DeclineRoom(ISharingServiceRoom room);

        /// <summary>
        /// If the current user is part of a private room, invite the given player to this room.
        /// </summary>
        Task<bool> InviteToRoom(SharingServicePlayerData player);

        /// <summary>
        /// Leave the currently joined sharing room, and join the default lobby.
        /// </summary>
        Task LeaveRoom();

        /// <summary>
        /// Send a specialized message that contains only a transform. 
        /// </summary>
        void SendTransformMessage(string target, SharingServiceTransform transform);

        /// <summary>
        /// Sends a message to all other clients
        /// </summary>
        /// <param name="message">Message to send</param>
        void SendMessage(ISharingServiceMessage message);

        /// <summary>
        /// Try to set a sharing service's property value.
        /// </summary>
        void SetProperty(string key, object value);

        /// <summary>
        /// Set a shared properties on the server. Setting to a value to null will clear the property from the server.
        /// </summary>
        void SetProperties(params object[] propertyNamesAndValues);

        /// <summary>
        /// Try to get a sharing service's property value.
        /// </summary>
        /// <returns>True if a non-null property value was found.</returns>
        bool TryGetProperty(string key, out object value);

        // <summary>
        /// Does this provider have the current property
        /// </summary>
        bool HasProperty(string property);

        /// <summary>
        /// Clear all properties with the given prefix.
        /// </summary>
        void ClearPropertiesStartingWith(string prefix);

        /// <summary>
        /// Try to set a player's property value.
        /// </summary>
        void SetPlayerProperty(string playerId, string key, object value);

        /// <summary>
        /// Try to get a player's property value.
        /// </summary>
        /// <returns>True if a non-null property value was found.</returns>
        bool TryGetPlayerProperty(string playerId, string key, out object value);

        // <summary>
        /// Does this provider have the current property for the player.
        /// </summary>
        bool HasPlayerProperty(string playerId, string property);

        /// <summary>
        /// Updates the list of rooms
        /// </summary>
        Task<IReadOnlyCollection<ISharingServiceRoom>> UpdateRooms();

        /// <summary>
        /// Send a ping
        /// if player is not null, will send it to that person only
        /// </summary>
        void SendPing(string targetRecipientId = null);

        /// <summary>
        /// Create a unique target object that will be synced across clients.
        /// </summary>
        ISharingServiceObject CreateTarget(SharingServiceObjectType type);

        /// <summary>
        /// Create a target object, with a given label, that will be synced across clients.
        /// </summary>
        ISharingServiceObject CreateTarget(SharingServiceObjectType type, string label);

        /// <summary>
        /// Spawn a network object that is shared across all clients
        /// </summary>
        Task<GameObject> SpawnTarget(GameObject original, object[] data = null);

        /// <summary>
        /// Despawn a network object that is shared across all clients
        /// </summary>
        Task DespawnTarget(GameObject gameObject);

        /// <summary>
        /// Create a target object, from a sharing id.
        /// </summary>
        ISharingServiceObject CreateTargetFromSharingId(string sharingId);

        /// <summary>
        /// Start finding the near addresses. If no primary address, default to the first found address.
        /// </summary>
        void FindAddresses();

        /// <summary>
        /// Start updating the address, using the current position of the sharing root.
        /// </summary>
        void CreateAddress();

        /// <summary>
        /// Set the user's primary address
        /// </summary>
        void SetAddress(SharingServiceAddress address);

        /// <summary>
        /// Get if the user is colocated
        /// </summary>
        bool Colocated(string participantId);

        /// <summary>
        /// Find sharing service players by a name. These player might not be in the current session.
        /// </summary>
        Task<IList<SharingServicePlayerData>> FindPlayers(string prefix, CancellationToken ct = default(CancellationToken));

        /// <summary>
        /// Initialize a network object with sharing components needed for the selected provider
        /// </summary>
        void EnsureNetworkObjectComponents(GameObject gameObject);

        /// <summary>
        /// Calibrate the sharing service's microphone so to better detect voices, and eliminate background noise.
        /// </summary>
        Task<bool> CalibrateVoiceDetection();
    }
}
