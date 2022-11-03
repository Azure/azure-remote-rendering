// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// Internal class used for implementation of a sharing protocol.
    /// </summary>
    public interface ISharingProvider : IDisposable
    {
        /// <summary>
        /// Get if the provider is connected
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Get if the provider is connecting
        /// </summary>
        bool IsConnecting { get; }

        /// <summary>
        /// True if connected to sharing service and logged in. But not necessarily in a session
        /// </summary>
        bool IsLoggedIn { get; }

        /// <summary>
        /// Get the provider's status message
        /// </summary>
        string StatusMessage { get; }

        /// <summary>
        /// The player id of the local player.
        /// </summary>
        string LocalPlayerId { get; }

        /// <summary>
        /// Get the invalid player id
        /// </summary>
        string InvalidPlayerId { get; }

        /// <summary>
        /// The list of current rooms.
        /// </summary>
        IReadOnlyCollection<ISharingServiceRoom> Rooms { get; }

        /// <summary>
        /// Get the current room.
        /// </summary>
        ISharingServiceRoom CurrentRoom { get; }

        /// <summary>
        /// Get if this provider supports private sharing sessions/rooms.
        /// </summary>
        bool HasPrivateRooms { get; }

        /// <summary>
        /// The user's current address.
        /// </summary>
        SharingServiceAddress PrimaryAddress { get; }

        /// <summary>
        /// All the known local addresses that are available to make a primary.
        /// </summary>
        IReadOnlyList<SharingServiceAddress> LocalAddresses { get; }

        /// <summary>
        /// Get the container for all sharing related game objects, such as new avatars. Avatar positioning will be relative to this container.
        /// </summary>
        GameObject SharingRoot { get; }

        /// <summary>
        /// Get or set the provider's audio settings.
        /// </summary>
        SharingServiceAudioSettings AudioSettings { get; set; }

        /// <summary>
        /// Get the provider's audio capabilities.
        /// </summary>
        SharingServiceAudioCapabilities AudioCapabilities { get; }

        /// <summary>
        /// Get or set the provider's avatar settings
        /// </summary>
        SharingServiceAvatarSettings AvatarSettings { get; set; }

        /// <summary>
        /// Get if offline spawning of objects is supported by this provider.
        /// </summary>
        bool OfflineSpawningSupported { get; }

        /// <summary>
        /// Get if the data parameters used doing spawning should be wrapped into ProtocolMessages.
        /// </summary>
        bool WrapSpawningData { get; }

        /// <summary>
        /// Event raised when a new client is connected
        /// </summary>
        event Action<ISharingProvider> Connected;

        /// <summary>
        /// Event raised when a new client is connecting
        /// </summary>
        event Action<ISharingProvider> Connecting;

        /// <summary>
        /// Event raised when a new client is disconnected
        /// </summary>
        event Action<ISharingProvider> Disconnected;

        /// <summary>
        /// Event raised when the provider's status message changes.
        /// </summary>
        event Action<ISharingProvider, string> StatusMessageChanged;

        /// <summary>
        /// Event raised when a new message was received
        /// </summary>
        event Action<ISharingProvider, ISharingServiceMessage> MessageReceived;

        /// <summary>
        /// A specialized message optimized for sending a transform to a target.
        /// </summary>
        event Action<ISharingProvider, string, SharingServiceTransform> TransformMessageReceived;

        /// <summary>
        /// A specialized message when response from a ping request
        /// </summary>
        event Action<ISharingProvider, string, SharingServicePingResponse> PingReturned;

        /// <summary>
        /// Event fired when a property changes.
        /// </summary>
        event Action<ISharingProvider, string, object> PropertyChanged;

        /// <summary>
        /// Event fired when the current room has changed.
        /// </summary>
        event Action<ISharingProvider, ISharingServiceRoom> CurrentRoomChanged;

        /// <summary>
        /// Event fired when the rooms have changed.
        /// </summary>
        event Action<ISharingProvider, IReadOnlyCollection<ISharingServiceRoom>> RoomsChanged;

        /// <summary>
        /// Event fired when an invitation is received.
        /// </summary>
        event Action<ISharingProvider, ISharingServiceRoom> RoomInviteReceived;

        /// <summary>
        /// Event fired when a new player has been added.
        /// </summary>
        event Action<ISharingProvider, SharingServicePlayerData> PlayerAdded;

        /// <summary>
        /// Event fired when a player has been removed.
        /// </summary>
        event Action<ISharingProvider, SharingServicePlayerData> PlayerRemoved;

        /// <summary>
        /// Event fired when a player's property changes.
        /// </summary>
        event Action<ISharingProvider, string, string, object> PlayerPropertyChanged;

        /// <summary>
        /// Event fired when a player's name changes.
        /// </summary>
        event Action<ISharingProvider, string, string> PlayerDisplayNameChanged;

        /// <summary>
        /// Event fired when a user's address has changed.
        /// </summary>
        event Action<ISharingProvider, SharingServiceAddress> PrimaryAddressChanged;

        /// <summary>
        /// Event fired when the users are the current primary address have changed.
        /// </summary>
        event Action<ISharingProvider> PrimaryAddressUsersChanged;

        /// <summary>
        /// Event fired when the known local addresses have changed.
        /// </summary>
        event Action<ISharingProvider, IReadOnlyList<SharingServiceAddress>> LocalAddressesChanged;

        /// <summary>
        /// Event fired when audio settings changed.
        /// </summary>
        event Action<ISharingProvider, SharingServiceAudioSettings> AudioSettingsChanged;

        /// <summary>
        /// Event fired when avatar settings changed.
        /// </summary>
        event Action<ISharingProvider, SharingServiceAvatarSettings> AvatarSettingsChanged;

        /// <summary>
        /// Connect and join the sharing service's lobby. This allows the client to see the available sharing rooms.
        /// </summary>
        Task Login();

        /// <summary>
        /// Disconnected from sharing service. This leave the lobby and the client can no longer see the available sharing rooms.
        /// </summary>
        void Logout();

        /// <summary>
        /// Update provider every frame.
        /// </summary>
        void Update();

        /// <summary>
        /// LateUpdate provider every frame.
        /// </summary>
        void LateUpdate();

        /// <summary>
        /// Create and join a new public sharing room.
        /// </summary>
        Task CreateAndJoinRoom();

        /// <summary>
        /// Create and join a new private sharing room. Only the given list of players can join the room.
        /// </summary>
        Task CreateAndJoinRoom(IEnumerable<SharingServicePlayerData> inviteList);

        /// <summary>
        /// Join the given room.
        /// </summary>
        Task JoinRoom(ISharingServiceRoom room);

        /// <summary>
        /// Join the given room by string id
        /// </summary>
        Task JoinRoom(string id);

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
        /// Updates list of available rooms
        /// </summary>
        Task<IReadOnlyCollection<ISharingServiceRoom>> UpdateRooms();

        /// <summary>
        /// Send a ping
        /// if player is not null, will send it to that person only
        /// </summary>
        SharingServicePingRequest? SendPing(byte id, string targetRecipientId = null);

        /// <summary>
        /// Set primary address to the current sharing root position
        /// </summary>
        Task CreateAddress(CancellationToken ct = default);

        /// <summary>
        /// Set primary address to the given address.
        /// </summary>
        Task SetAddress(SharingServiceAddress address, CancellationToken ct = default);

        /// <summary>
        /// Get if the participant is co-located with the local user.
        /// </summary>
        bool Colocated(string participantId);

        /// <summary>
        /// Start finding all local known addresses. Also if primary address is null, this will default primary to first known address.
        /// </summary>
        Task FindAddresses(CancellationToken ct = default);

        /// <summary>
        /// Find sharing service players by a name. These player might not be in the current session.
        /// </summary>
        Task<IList<SharingServicePlayerData>> FindPlayers(string prefix, CancellationToken ct = default(CancellationToken));

        /// <summary>
        /// Spawn a network object that is shared across all clients
        /// </summary>
        Task<GameObject> SpawnTarget(GameObject original, object[] data);

        /// <summary>
        /// Despawn a network object that is shared across all clients
        /// </summary>
        Task DespawnTarget(GameObject gameObject);

        /// <summary>
        /// Initialize a network object with sharing components needed for the selected provider
        /// </summary>
        /// <param name="gameObject"></param>
        void EnsureNetworkObjectComponents(GameObject gameObject);

        /// <summary>
        /// Calibrate the sharing provider's microphone so to better detect voices, and eliminate background noise.
        /// </summary>
        Task<bool> CalibrateVoiceDetection();
    }
}