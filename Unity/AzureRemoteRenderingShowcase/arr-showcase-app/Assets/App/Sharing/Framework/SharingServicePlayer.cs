// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// Represents a client that has joined a sharing service room (or session).
    /// </summary>
    public class SharingServicePlayer : ISharingServicePlayer, IDisposable
    {
        private Pose _pose;
        private ISharingService _service;
        private Dictionary<string, object> _properties = new Dictionary<string, object>();

        public SharingServicePlayer(ISharingService service, int playerId, bool isLocal)
        {
            _service = service ?? throw new ArgumentNullException("Sharing service can't be null");
            PlayerId = playerId;
            IsLocal = isLocal;
        }

        #region ISharingServicePlayer Properties
        /// <summary>
        /// The id of this player.
        /// </summary>
        public int PlayerId { get; }

        /// <summary>
        /// Get if this is the local player.
        /// </summary>
        public bool IsLocal { get; }

        /// <summary>
        /// The pose of the player
        /// </summary>
        public Pose Pose => _pose;

        /// <summary>
        /// Get the current properties for this player.
        /// </summary>
        public IReadOnlyDictionary<string, object> Properties => _properties;
        #endregion ISharingServicePlayer Properties

        #region ISharingServicePlayer Events
        /// <summary>
        /// Event fired when a property changes.
        /// </summary>
        public event Action<ISharingServicePlayer, string, object> PropertyChanged;
        #endregion ISharingServicePlayer Events

        #region ISharingServicePlayer Functions
        /// <summary>
        /// Set the local player's postion and rotation. This is ignored if the player is not the local player.
        /// </summary>
        public void SetTransform(Vector3 position, Quaternion rotation)
        {
            if (_service == null || !IsLocal)
            {
                return;
            }

            // Always send position and rotation, even if not changed, as new clients would have missed old events.
            // Also, when running on device, these values will likely be changing every frame anyways.

            _pose.position = position;
            _pose.rotation = rotation;
            _service.SendLocalPlayerPose(_pose);
        }

        /// <summary>
        /// Set a property on the given target to a praticular value.
        /// </summary>
        /// <param name="property">The property to set</param>
        /// <param name="value">The value to set.</param>
        public void SetProperty(string property, object value)
        {
            if (value == null)
            {
                _properties.Remove(property);
            }
            else
            {
                _properties[property] = value;
            }

            _service?.SetPlayerProperty(PlayerId, property, value);
        }

        /// <summary>
        /// Try and get a property value of a given target.
        /// </summary>
        /// <param name="property">The property to get</param>
        /// <param name="value">The value of the property.</param>
        public bool TryGetProperty(string property, out object value)
        {
            bool found;
            if (_service == null)
            {
                found = false;
                value = null;
            }
            else
            {
                found = (_properties.TryGetValue(property, out value) && value != null) ||
                    (_service.TryGetPlayerProperty(PlayerId, property, out value));
            }
            return found;
        }

        /// <summary>
        /// Try and get a property value of a given target.
        /// </summary>
        /// <param name="property">The property to get</param>
        /// <param name="value">The value of the property.</param>
        public bool TryGetProperty<T>(string property, out T value)
        {
            value = default;
            object obj;
            if (TryGetProperty(property, out obj) && obj is T)
            {
                value = (T)obj;
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Does this target have the given property
        /// </summary>
        public bool HasProperty(string property)
        {
            bool found;
            if (_service == null)
            {
                found = false;
            }
            else
            {
                object value;
                found = (_properties.TryGetValue(property, out value) && value != null) ||
                     _service.HasPlayerProperty(PlayerId, property);
            }
            return found;
        }
        #endregion ISharingServicePlayer Functions

        #region IDisposable Functions
        /// <summary>
        /// Release the sharing service.
        /// </summary>
        public void Dispose()
        {
            _service = null;
        }
        #endregion IDisposable Functions

        #region Public Functions
        /// <summary>
        /// Invoked by the sharing service when a new player position is received.
        /// </summary>
        public void ReceivedPose(Pose pose)
        {
            _pose = pose;
        }

        /// <summary>
        /// Invoked by the sharing service when a new property value is received.
        /// </summary>
        public void ReceivedPropertiesChanged(string property, object value)
        {
            if (value == null)
            {
                _properties.Remove(property);
            }
            else
            {
                _properties[property] = value;
            }
            PropertyChanged?.Invoke(this, property, value);
        }
        #endregion Public Functions
    }
}

