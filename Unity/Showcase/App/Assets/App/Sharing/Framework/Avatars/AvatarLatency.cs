// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// An avatar metadata component that shows the users latency value when pinged
    /// </summary>
    public class AvatarLatency : AvatarComponent
    {
        #region Serialized Fields
        [SerializeField]
        [Tooltip("The Text for latency value.")]
        private TMPro.TMP_Text latencyText = null;

        /// <summary>
        /// The Text for latency value.
        /// </summary>
        public TMPro.TMP_Text LatencyText
        {
            get => latencyText;
            set => latencyText = value;
        }

        [SerializeField]
        [Tooltip("Delta when the text is updated to when it disappears.")]
        private float fadeTimeoutInSeconds = 5;

        public float FadeTimeoutInSeconds
        {
            get => fadeTimeoutInSeconds;
            set => fadeTimeoutInSeconds = value;
        }
        #endregion Serialized Fields

        #region Private Fields
        private Coroutine fadeRoutine = null;
        #endregion Private Fields

        #region MonoBehavior Functions
        private void OnEnable()
        {
            // start off disabled
            LatencyText.gameObject.SetActive(false);

            TimeSpan latency;
            if (TryGetProperty(SharableStrings.PlayerLatency, out latency))
            {
                UpdateLatencyValue(latency);
            }
            else
            {
                UpdateLatencyValue(latency: TimeSpan.Zero);
            }
        }

        private void OnDisable()
        {
            Reset();
        }
        #endregion MonoBehavior Functions

        #region Protected Functions
        /// <summary>
        /// Handle property changes for the current participant.
        /// </summary>
        protected override void OnPropertyChanged(string name, object value)
        {
            switch (value)
            {
                case TimeSpan latency when name == SharableStrings.PlayerLatency:
                    UpdateLatencyValue(latency);
                    break;
            }
        }
        #endregion Protected Function

        #region Private Functions
        private void UpdateLatencyValue(TimeSpan latency)
        {
            Reset();

            if (latency != TimeSpan.Zero)
            {
                LatencyText.gameObject.SetActive(true);
                latencyText.text = $"{latency.TotalMilliseconds} ms.";

                fadeRoutine = StartCoroutine(FadeTimer());
            }
        }

        private void Reset()
        {
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }

            LatencyText.gameObject.SetActive(false);
        }

        IEnumerator FadeTimer()
        {
            yield return new WaitForSeconds(fadeTimeoutInSeconds);

            LatencyText.gameObject.SetActive(false);

            fadeRoutine = null;
        }
        #endregion Private Functions
    }
}
