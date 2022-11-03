// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    public enum MicrophoneAdjustmentType
    {
        /// <summary>
        /// Microphone audio can't be adjusted.
        /// </summary>
        None,

        /// <summary>
        /// The microphone audio can be adjusted by adding a decibel gain.
        /// </summary>
        Gain,

        /// <summary>
        /// The microphone audio can be adjusted by scaling the audio input.
        /// </summary>
        ScaleFactor
    }


    /// <summary>
    /// Holds the audio capabilities of the sharing service
    /// </summary>
    public struct SharingServiceAudioCapabilities
    {
        /// <summary>
        /// The type of microphone adjustment that is available.
        /// </summary>
        public MicrophoneAdjustmentType MicrophoneAdjustment;

        /// <summary>
        /// The min value for the microphone audio adjustment.
        /// </summary>
        public float MinMicrophoneAdjustment;

        /// <summary>
        /// The max value for the microphone audio adjustment.
        /// </summary>
        public float MaxMicrophoneAdjustment;

        /// <summary>
        /// The number format for use when displaying the microphone adjustment amount.
        /// </summary>
        /// <remarks>To function correctly, this must can a {0} format string.</remarks>
        public string MicrophoneAdjustmentNumberFormat;

        /// <summary>
        /// Determine if the sharing provider supports voice calibration
        /// </summary>
        public bool SupportsVoiceCalibration;

        /// <summary>
        /// Create default capabilities struct
        /// </summary>
        public static SharingServiceAudioCapabilities Default
        {
            get
            {
                return new SharingServiceAudioCapabilities()
                {
                    MicrophoneAdjustment = MicrophoneAdjustmentType.None,
                    MinMicrophoneAdjustment = 0.0f,
                    MaxMicrophoneAdjustment = 1.0f,
                    MicrophoneAdjustmentNumberFormat = "{0}",
                    SupportsVoiceCalibration = false
                };
            }
        }
    }

    /// <summary>
    /// Hold various audio settings used by the sharing service
    /// </summary>
    public struct SharingServiceAudioSettings
    {
        /// <summary>
        /// Should the microphone be muted
        /// </summary>
        public bool MuteMicrophone;

        /// <summary>
        /// The adjustment to apply to microphone audio, either gain or scale factor
        /// </summary>
        public float MicrophoneAdjustment;

        /// <summary>
        /// The distance at which the playback volume starts to decrease.
        /// </summary>
        public float PlaybackFalloffDistance;

        /// <summary>
        /// Create default settings struct
        /// </summary>
        public static SharingServiceAudioSettings Default
        {
            get
            {
                return new SharingServiceAudioSettings()
                {
                    MuteMicrophone = false,
                    MicrophoneAdjustment = 0.0f,
                    PlaybackFalloffDistance = 1.0f
                };
            }
        }
    }
}
