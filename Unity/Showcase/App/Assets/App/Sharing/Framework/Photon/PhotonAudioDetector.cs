// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if PHOTON_INSTALLED
using Photon.Voice.Unity;
using System;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    public class PhotonAudioDetector
    {
        Recorder _recorder;
        PhotonProperties _properties;
        VoiceDetectedValue _state = VoiceDetectedValue.Unknown;
        MutedValue _mutedState = MutedValue.Unknown;
        DateTimeOffset _stateStartTime = DateTimeOffset.MinValue;
        private LogHelper<PhotonAudioDetector> _logger = new LogHelper<PhotonAudioDetector>();

        public PhotonAudioDetector(PhotonComponents components, PhotonProperties properties)
        {
            components = components ?? throw new ArgumentNullException("Components can't be null");
            _properties = properties ?? throw new ArgumentNullException("Properties can't be null");
            _recorder = components?.VoiceRecorder;
        }

        #region Public Properties 
        /// <summary>
        /// Get if the user's voice is detected.
        /// </summary>
        public bool VoiceDetected
        {
            // If exitting, assume voice is still detected, since exitting might be canceled
            get => _state == VoiceDetectedValue.Entered || _state == VoiceDetectedValue.Exitting;
        }

        /// <summary>
        /// Get if the user is muted.
        /// </summary>
        public bool Muted => _mutedState == MutedValue.True;

        /// <summary>
        /// Get if verbose logging is enabled
        /// </summary>
        public bool VerboseLogging
        {
            get => _logger.Verbose == LogHelperState.Always;
            set => _logger.Verbose = (value) ? LogHelperState.Always : LogHelperState.Default;
        }

        /// <summary>
        /// The AMPs needed to enter voice detected
        /// </summary>
        public float EnterVoiceDetectedLevel { get; set; } = 0.01f;

        /// <summary>
        /// The AMPs needed to exit voice detected
        /// </summary>
        public float ExitVoiceDetectedLevel { get; set; } = 0.005f;

        /// <summary>
        /// The time, in seconds, to delay exitting the "voice detected" state.
        /// </summary>
        public float ExitVoiceDetectedDelay { get; set; } = 0.5f;
        #endregion Public Properties

        #region Public Functions
        public void Update()
        {
            if (_recorder != null)
            {
                UpdateState(_recorder.IsCurrentlyTransmitting && _recorder.LevelMeter != null ? 
                    _recorder.LevelMeter.CurrentPeakAmp : 0.0f);

                UpdateMutedState(_recorder.TransmitEnabled);
            }
        }
        #endregion Public Functions

        #region Private Functions
        private void UpdateState(float currentPeakAmp)
        {
            VoiceDetectedValue oldState = _state;
            VoiceDetectedValue newState = oldState;
            
            switch (oldState)
            {
                case VoiceDetectedValue.Unknown:
                case VoiceDetectedValue.Exitted:
                    if (currentPeakAmp >= EnterVoiceDetectedLevel)
                    {
                        newState = VoiceDetectedValue.Entered;
                    }
                    else
                    {
                        newState = VoiceDetectedValue.Exitted;
                    }
                    break;

                case VoiceDetectedValue.Entered:
                    if (currentPeakAmp <= ExitVoiceDetectedLevel)
                    {
                        newState = VoiceDetectedValue.Exitting;
                    }
                    break;

                case VoiceDetectedValue.Exitting:
                    if (currentPeakAmp > ExitVoiceDetectedLevel)
                    {
                        newState = VoiceDetectedValue.Entered;
                    }
                    else if (TimeInState() > ExitVoiceDetectedDelay)
                    {
                        newState = VoiceDetectedValue.Exitted;
                    }
                    break;
            }

            if (newState != oldState)
            {
                _logger.LogVerbose("State Changing ({0}->{1}) ({2})", oldState, newState, currentPeakAmp);


                switch (newState)
                {
                    case VoiceDetectedValue.Entered:
                        SetPlayerSpeaking(true);
                        break;

                    case VoiceDetectedValue.Exitted:
                        SetPlayerSpeaking(false);
                        break;
                }

                _state = newState;
                _stateStartTime = DateTimeOffset.UtcNow;
            }
        }

        private void UpdateMutedState(bool isTransmitting)
        {
            MutedValue oldState = _mutedState;
            MutedValue newState = oldState;

            if (isTransmitting)
            {
                newState = MutedValue.False;
            }
            else
            {
                newState = MutedValue.True;
            }

            if (oldState != newState)
            {
                _logger.LogVerbose("Muted State Changing ({0}->{1})", oldState, newState);

                SetPlayerMuted(newState == MutedValue.True);
                _mutedState = newState;
            }
        }

        private float TimeInState()
        {
            return (float)(DateTimeOffset.UtcNow - _stateStartTime).TotalSeconds;
        }

        private void SetPlayerSpeaking(bool speaking)
        {
            _properties.SetSessionParticipantProperty(SharableStrings.PlayerSpeaking, speaking);
        }

        private void SetPlayerMuted(bool muted)
        {
            _properties.SetSessionParticipantProperty(SharableStrings.PlayerMuted, muted);
        }
        #endregion Private Functions

        #region Private Enum
        private enum VoiceDetectedValue
        {
            Unknown,
            Entered,
            Exitting,
            Exitted,
        }

        private enum MutedValue
        {
            Unknown,
            True,
            False
        }
        #endregion Private Enum
    }
}
#endif
