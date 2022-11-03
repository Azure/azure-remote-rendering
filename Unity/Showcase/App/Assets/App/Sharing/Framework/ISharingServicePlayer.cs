// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// Represents a client that has joined a sharing service room (or session).
    /// </summary>
    public interface ISharingServicePlayer
    {
        /// <summary>
        /// The player information
        /// </summary>
        SharingServicePlayerData Data { get; }

        /// <summary>
        /// Get if this is the local player.
        /// </summary>
        bool IsLocal { get; }

        /// <summary>
        /// Get if this is a player from the room/session.
        /// </summary>
        bool InRoom { get; }

        /// <summary>
        /// Event fired when a property changes.
        /// </summary>
        event Action<ISharingServicePlayer, string, object> PropertyChanged;

        /// <summary>
        /// Set a property on the given target to a praticular value.
        /// </summary>
        /// <param name="property">The property to set</param>
        /// <param name="value">The value to set.</param>
        void SetProperty(string property, object value);

        /// <summary>
        /// Try and get a property value of a given target.
        /// </summary>
        /// <param name="property">The property to get</param>
        /// <param name="value">The value of the property.</param>
        bool TryGetProperty(string property, out object value);

        /// <summary>
        /// Try and get a property value of a given target.
        /// </summary>
        /// <param name="property">The property to get</param>
        /// <param name="value">The value of the property.</param>
        bool TryGetProperty<T>(string property, out T value);

        /// <summary>
        /// Does this target have the given property
        /// </summary>
        bool HasProperty(string property);
    }
}

