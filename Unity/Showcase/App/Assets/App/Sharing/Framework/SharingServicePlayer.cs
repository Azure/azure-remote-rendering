// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// Represents a client that has joined a sharing service room (or session).
    /// </summary>
    public class SharingServicePlayer : ISharingServicePlayer, IDisposable
    {
        private ISharingService _service;
        private Dictionary<string, object> _properties = new Dictionary<string, object>();
        private SharingServicePlayerData _data = default;

        /// <summary>
        /// Create a player that is part of the current room/session
        /// </summary>
        public SharingServicePlayer(ISharingService service, SharingServicePlayerData data, bool isLocal)
        {
            _service = service;
            this.Data = data;
            this.IsLocal = isLocal;
        }

        #region ISharingServicePlayer Properties
        /// <summary>
        /// The player information
        /// </summary>
        public SharingServicePlayerData Data
        {
            get => _data;

            set
            {
                _data = value;
                DataChanged?.Invoke(this, value);
            }
        }

        /// <summary>
        /// Get if this is the local player.
        /// </summary>
        public bool IsLocal { get; }

        /// <summary>
        /// Get if this is an offline player. Offline meaning not connected to the current room/session.
        /// </summary>
        public bool InRoom => string.IsNullOrEmpty(Data.PlayerId);

        #endregion ISharingServicePlayer Properties

        #region ISharingServicePlayer Events
        /// <summary>
        /// Event fired when a property changes.
        /// </summary>
        public event Action<ISharingServicePlayer, string, object> PropertyChanged;

        /// <summary>
        /// Something in the player data has changed.
        /// </summary>
        public event Action<ISharingServicePlayer, SharingServicePlayerData> DataChanged;
        #endregion ISharingServicePlayer Events

        #region ISharingServicePlayer Functions
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

            _service?.SetPlayerProperty(Data.PlayerId, property, value);
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
                    (_service.TryGetPlayerProperty(Data.PlayerId, property, out value));
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
                     _service.HasPlayerProperty(Data.PlayerId, property);
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

        /// <summary>
        /// Invoked by the sharing service when the player's display name changed
        /// </summary>
        public void ReceivedPlayerDisplayName(string name)
        {
            if (Data.DisplayName != name)
            {
                var newData = _data;
                newData.DisplayName = name;
                Data = newData;
            }
        }
        #endregion Public Functions
    }
}

