// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// A service for sharing application state across other clients.
    /// </summary>
    public interface ISharingService : IMixedRealityExtensionService
    {
        /// <summary>
        /// True if connected to a session and able to communicate with other clients
        /// </summary>
        bool IsConnected { get; }

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
        /// Get the current room.
        /// </summary>
        ISharingServiceRoom CurrentRoom { get; }

        /// <summary>
        /// Event fired when the service is connected
        /// </summary>
        event Action<ISharingService> Connected;

        /// <summary>
        /// Event fired when the service disconnects
        /// </summary>
        event Action<ISharingService> Disconnected;

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
        /// A specialized message optimized for sending an array of floats to a target.
        /// </summary>
        event Action<ISharingProvider, ISharingServiceTarget, float[]> NumericMessageReceived;

        /// <summary>
        /// Event fired when a new sharing target has been added
        /// </summary>
        event Action<ISharingService, ISharingServiceTarget> TargetAdded;

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
        /// Connects to the session
        /// </summary>
        void Connect();

        /// <summary>
        /// Create and join a new sharing room.
        /// </summary>
        void CreateAndJoinRoom();

        /// <summary>
        /// Join the given room.
        /// </summary>
        void JoinRoom(ISharingServiceRoom room);

        /// <summary>
        /// Leave the currently joined sharing room, and join the default lobby.
        /// </summary>
        void LeaveRoom();

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
        void SetPlayerProperty(int playerId, string key, object value);

        /// <summary>
        /// Try to get a player's property value.
        /// </summary>
        /// <returns>True if a non-null property value was found.</returns>
        bool TryGetPlayerProperty(int playerId, string key, out object value);

        // <summary>
        /// Does this provider have the current property for the player.
        /// </summary>
        bool HasPlayerProperty(int playerId, string property);

        /// <summary>
        /// Send the local player's position and rotation
        /// </summary>
        void SendLocalPlayerPose(Pose pose);

        /// <summary>
        /// Create a unique target object that will be synced across clients.
        /// </summary>
        ISharingServiceTarget CreateTarget(SharingServiceTargetType type);

        /// <summary>
        /// Create a target object, with a given label, that will be synced across clients.
        /// </summary>
        ISharingServiceTarget CreateTarget(SharingServiceTargetType type, string label);

        /// <summary>
        /// Create a target object, from a sharing id.
        /// </summary>
        ISharingServiceTarget CreateTargetFromSharingId(string sharingId);
    }
}
