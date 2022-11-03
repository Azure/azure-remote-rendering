// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// The base matedata behavior for displaying avatar metadata.
    /// </summary>
    [RequireComponent(typeof(AvatarComponentCollection))]
    public class AvatarComponent : MonoBehaviour
    {
        #region Serialized Fields
        #endregion Serialized Fields

        #region Public Properties
        /// <summary>
        /// Get the session participant data object.
        /// </summary>
        public SharingServicePlayerData PlayerData { get; private set; }

        /// <summary>
        /// Get if this component has been initialized
        /// </summary>
        public bool IsInitialized { get; private set; } = false;

        /// <summary>
        /// The sharing service this avatar belongs to
        /// </summary>
        public ISharingService Service { get; private set; } = null;
        #endregion Public Properties

        #region MonoBehavior Functions
        protected virtual void OnDestroy()
        {
            if (Service != null)
            {
                Service.PlayerPropertyChanged -= OnParticipantPropertyChanged;
                Service.PlayerDisplayNameChanged -= OnPlayerDisplayNameChanged;
                Service = null;
            }
        }
        #endregion MonoBehavior Functions

        #region Public Functions
        /// <summary>
        /// Initialize this metadata component.
        /// </summary>
        public void Initialize(SharingServicePlayerData playerData)
        {
            PlayerData = playerData;
            IsInitialized = true;
            Service = AppServices.SharingService;
            Service.PlayerPropertyChanged += OnParticipantPropertyChanged;
            Service.PlayerDisplayNameChanged += OnPlayerDisplayNameChanged;
            OnInitialized();
        }
        #endregion Public Functions

        #region Protected Functions
        /// <summary>
        /// Implement to handle object initialization
        /// </summary>
        protected virtual void OnInitialized() { }

        /// <summary>
        /// Implement to handle property changes for the current participant.
        /// </summary>
        protected virtual void OnPropertyChanged(string name, object value) { }

        /// <summary>
        /// Implement to handle when display name changes.
        /// </summary>
        protected virtual void OnDisplayNameChanged(string name) { }

        /// <summary>
        /// Try to get a session participant property.
        /// </summary>
        protected bool TryGetProperty<T>(string name, out T value)
        {
            if (!IsInitialized)
            {
                value = default;
                return false;
            }

            object objValue;
            bool result = Service.TryGetPlayerProperty(PlayerData.PlayerId, name, out objValue);

            if (result && objValue is T)
            {
                value = (T)objValue;
            }
            else
            {
                result = false;
                value = default;
            }

            return result;
        }
        #endregion Protected Functions

        #region Private Functions
        private void OnParticipantPropertyChanged(ISharingServicePlayer player, string name, object value)
        {
            if (!IsInitialized ||
                player == null ||
                player.Data.PlayerId != PlayerData.PlayerId)
            {
                return;
            }

            OnPropertyChanged(name, value);
        }

        private void OnPlayerDisplayNameChanged(ISharingServicePlayer player, string name)
        {
            if (!IsInitialized ||
                player == null ||
                player.Data.PlayerId != PlayerData.PlayerId)
            {
                return;
            }

            OnDisplayNameChanged(name);
        }
        #endregion Private Functions
    }
}
