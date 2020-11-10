// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// Internal class used for implementation of a sharing protocol.
    /// </summary>
    public interface ISharingProvider
    {
        /// <summary>
        /// Get if the provider is connected
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// The player id of the local player.
        /// </summary>
        int LocalPlayerId { get; }

        /// <summary>
        /// The list of current rooms.
        /// </summary>
        IReadOnlyCollection<ISharingServiceRoom> Rooms { get; }

        /// <summary>
        /// Get the current room.
        /// </summary>
        ISharingServiceRoom CurrentRoom { get; }

        /// <summary>
        /// Event raised when a new client is connected
        /// </summary>
        event Action<ISharingProvider> Connected;

        /// <summary>
        /// Event raised when a new client is disconnected
        /// </summary>
        event Action<ISharingProvider> Disconnected;

        /// <summary>
        /// Event raised when a new message was received
        /// </summary>
        event Action<ISharingProvider, ISharingServiceMessage> MessageReceived;

        /// <summary>
        /// A specialized message optimized for sending a transform to a target.
        /// </summary>
        event Action<ISharingProvider, string, SharingServiceTransform> TransformMessageReceived;

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
        /// Event fired when a new player has been added.
        /// </summary>
        event Action<ISharingProvider, int> PlayerAdded;

        /// <summary>
        /// Event fired when a player has been removed.
        /// </summary>
        event Action<ISharingProvider, int> PlayerRemoved;

        /// <summary>
        /// Event fired when a player's property changes.
        /// </summary>
        event Action<ISharingProvider, int, string, object> PlayerPropertyChanged;

        /// <summary>
        /// Event fired when a player's position and rotation has changed.
        /// </summary>
        event Action<ISharingProvider, int, Pose> PlayerPoseChanged;

        /// <summary>
        /// Connect and join the sharing service's lobby. This allows the client to see the available sharing rooms.
        /// </summary>
        void Connect();

        /// <summary>
        /// Disconnected from sharing service. This leave the lobby and the client can no longer see the available sharing rooms.
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Update provider every frame.
        /// </summary>
        void Update();

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
        /// Sends a private reply to a message another client sent
        /// </summary>
        /// <param name="original">Original message to reply to</param>
        /// <param name="reply">Reply to send</param>
        void SendReply(ISharingServiceMessage original, ISharingServiceMessage reply);

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
    }
}
