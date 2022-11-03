// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// A helper to set falloff values base on sharing service settings.
    /// </summary>
    class AvatarAudio : AvatarComponent
    {
        #region Serializable Fields
        [SerializeField]
        [Tooltip("The Unity output audio source")]
        private AudioSource outputAudioSource;

        /// <summary>
        /// Get or set the Unity output audio source.
        /// </summary>
        public AudioSource OutputAudioSource
        {
            get => outputAudioSource;
            set => outputAudioSource = value;
        }
        #endregion Serializable Fields

        #region MonoBehavior Functions
        protected override void OnDestroy()
        {
            if (Service != null)
            {
                Service.AudioSettingsChanged -= OnAudioSettingsChanged;
            }

            base.OnDestroy();
        }
        #endregion MonoBehavior Functions

        #region Protected Functions
        protected override void OnInitialized()
        {
            base.OnInitialized();

            if (Service != null)
            {
                Service.AudioSettingsChanged += OnAudioSettingsChanged;
            }

            SetFalloff(Service.AudioSettings.PlaybackFalloffDistance);
        }
        #endregion Protected Functions

        #region Private Functions

        private void OnAudioSettingsChanged(ISharingService sender, SharingServiceAudioSettings args)
        {
            SetFalloff(args.PlaybackFalloffDistance);
        }

        private void SetFalloff(float falloff)
        {
            if (outputAudioSource != null)
            {
                outputAudioSource.minDistance = Mathf.Clamp(falloff, 1.0f, 10.0f);
            }
        }
        #endregion Private Functions
    }
}

