// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if PHOTON_INSTALLED
using Photon.Voice.Unity;
using Photon.Voice.Unity.UtilityScripts;
using System;
using System.Threading.Tasks;
using UnityEngine;

#if UNITY_WINRT && !UNITY_EDITOR
using Windows.Media.Capture;
#endif

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    [RequireComponent(typeof(Recorder))]
    public class PhotonMicrophoneVolumeControl : MonoBehaviour
    {
#if UNITY_WINRT && !UNITY_EDITOR
        private Task<MediaCapture> _initializingMediaCaptureTask = null;
#endif // UNITY_WINRT

        private float _minVolume = 0.0f;
        private float _maxVolume = 100.0f;

        private float _volume = -1.0f;
        private float _voiceDetectionThreshold = 0.02f;
        private bool _started = false;
        private string _lastDeviceId = null;
        private Recorder _recorder = null;
        private MicAmplifier _amplifier = null;
        private LogHelper<PhotonMicrophoneVolumeControl> _logger = new LogHelper<PhotonMicrophoneVolumeControl>();

        public bool VerboseLogging
        {
            get => _logger.Verbose == LogHelperState.Always;
            set
            {
                _logger.Verbose = value ? LogHelperState.Always : LogHelperState.Default;
            }
        }

        public float Volume
        {
            get => _volume;
            set => SetVolume(value);
        }

        /// <summary>
        /// Get or set the voice detection threshold of the Photon Recorder when volume is at max value
        /// </summary>
        public float VoiceDetectionThreshold
        {
            get => _voiceDetectionThreshold;
        }

        /// <summary>
        /// Can voice detection threshold be calibrated
        /// </summary>
        public bool CanCalibrate
        {
            get
            {
                var recorder = GetRecorder();
                return recorder != null && recorder.IsRecording;
            }
        }

        /// <summary>
        /// Event fired when the volume was changed by an external component, and not this,
        /// </summary>
        public event Action<PhotonMicrophoneVolumeControl, float> ExternalVolumeChange;

        private void Awake()
        {
            GetRecorder();
            GetMicAmplifier();
        }

        private void Start()
        {
            _started = true;
            if (_volume >= _minVolume)
            {
                _ = ApplyVolume();
            }
        }

        private void Update()
        {
            LoadVolume();
        }

        /// <summary>
        /// Invoke to calibrate voice threshold
        /// </summary>
        public async Task<bool> Calibrate()
        {
            var recorder = GetRecorder();
            if (recorder == null || !recorder.IsRecording)
            {
                return false;
            }

            float originalVolume = _volume;
            recorder.TransmitEnabled = true;
            if (_volume != _maxVolume)
            {
                _volume = _maxVolume;
                await ApplyVolume();
            }

            var calibrationTask = new TaskCompletionSource<float>();
            // prevent mic audio from being heard during calibration
            recorder.VoiceDetectionThreshold = 1.0f;
            recorder.VoiceDetectorCalibrate(durationMs: 10000, (float newValue) => { calibrationTask.TrySetResult(newValue); });
            var callibrationResult = await calibrationTask.Task.AwaitWithTimeout(TimeSpan.FromSeconds(30));

            if (callibrationResult.success)
            {
                _logger.LogInformation("Voice dector calibrated (threshold: {0}->{1})", _voiceDetectionThreshold, callibrationResult.result);
                _voiceDetectionThreshold = callibrationResult.result;
            }
            else
            {
                _logger.LogError("Voice dector calibration failed.");
            }
            
            if (_volume != originalVolume)
            {
                _volume = originalVolume;
                await ApplyVolume();
            }

            return callibrationResult.success;
        }

        private void SetVolume(float value)
        {
            value = Mathf.Clamp(value, _minVolume, _maxVolume);
            if (_volume != value)
            {
                _volume = value;
                _ = ApplyVolume();
            }
        }

        private void ApplyVolumeWithAmplifier()
        {
            var amplifier = GetMicAmplifier();
            if (amplifier != null)
            {
                float scaleFactor = Mathf.Clamp01(_volume / _maxVolume);
                _logger.LogVerbose("Setting volume via amplifier ({0})", scaleFactor);
                _amplifier.enabled = true;
                amplifier.AmplificationFactor = scaleFactor;
            }
        }

        private void ApplyVoiceDetectionThreshold()
        {
            var recorder = GetRecorder();
            if (recorder != null)
            {
                recorder.VoiceDetectionThreshold = (_voiceDetectionThreshold * _volume) / _maxVolume;
            }
        }

        private void LoadVolumeWithAmplifier()
        {
            var amplifier = GetMicAmplifier();
            if (amplifier != null)
            {
                float oldVolume = _volume;
                float scaleFactor = amplifier.AmplificationFactor;
                _volume = Mathf.Clamp(scaleFactor * _maxVolume, _minVolume, _maxVolume);
                if (oldVolume != _volume)
                {
                    _logger.LogVerbose("Loaded volume via amplifier ({0})", _volume);
                    ExternalVolumeChange?.Invoke(this, _volume);
                }
            }
        }

        private string GetDeviceId()
        {
            var recorder = GetRecorder();
            if (recorder != null && recorder.MicrophoneDevice != null)
            {
                return recorder.MicrophoneDevice.IDString;
            }
            else
            {
                return string.Empty;
            }
        }

        private Recorder GetRecorder()
        {
            if (_recorder == null)
            {
                _recorder = GetComponent<Recorder>();
                _voiceDetectionThreshold = _recorder.VoiceDetectionThreshold;
            }
            return _recorder;
        }

        private MicAmplifier GetMicAmplifier()
        {
            if (_amplifier == null)
            {
                _logger.LogVerbose("Creating mic amplifier");
                _amplifier = gameObject.EnsureComponent<MicAmplifier>();
                _logger.LogVerbose("Created mic amplifier");
            }
            return _amplifier;
        }

        private void DisableMicAmplifier()
        {
            if (_amplifier != null)
            {
                _amplifier.AmplificationFactor = 1.0f;
                _amplifier.enabled = false;
            }
        }

#if UNITY_WINRT && !UNITY_EDITOR
        private async Task ApplyVolume()
        {
            if (!_started)
            {
                return;
            }

            var mediaCapture = await GetMediaCatpure();
            if (mediaCapture != null)
            {
                _logger.LogVerbose("Setting volume ({0})", _volume);
                DisableMicAmplifier();
                mediaCapture.AudioDeviceController.VolumePercent = _volume;
            }
            else
            {
                ApplyVolumeWithAmplifier();
            }

            ApplyVoiceDetectionThreshold();
        }

        private async void LoadVolume()
        {
            var mediaCapture = await GetMediaCatpure();
            if (mediaCapture != null)
            {
                float oldVolume = _volume;
                _volume = mediaCapture.AudioDeviceController.VolumePercent;
                if (oldVolume != _volume)
                {
                    _logger.LogVerbose("Loaded volume ({0})", _volume);
                    ExternalVolumeChange?.Invoke(this, _volume);
                }
            }
            else
            {
                LoadVolumeWithAmplifier();
            }
        }

        private Task<MediaCapture> GetMediaCatpure()
        {
            var deviceId = GetDeviceId();
            if (string.IsNullOrEmpty(deviceId))
            {
                _logger.LogWarning("Ignoring media capture initialization. Device id is empty.");
                return null;
            }

            if (_lastDeviceId != deviceId)
            {
                _lastDeviceId = deviceId;
                DisposeMediaCapture();
            }

            if (_initializingMediaCaptureTask != null)
            {
                return _initializingMediaCaptureTask;
            }

            _initializingMediaCaptureTask = InitializeMediaCapture(deviceId);
            return _initializingMediaCaptureTask;
        }

        private async Task<MediaCapture> InitializeMediaCapture(string deviceId)
        { 
            _logger.LogVerbose("Initializing media capture ({0})", deviceId);

            var mediaCapture = new MediaCapture();
            MediaCaptureInitializationSettings settings = new MediaCaptureInitializationSettings()
            {
                AudioDeviceId = deviceId
            };

            try
            {
                await mediaCapture.InitializeAsync(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to initialize media capture device ({0}) Exception: {1}", deviceId, ex);
            }

            return mediaCapture;
        }

        private async void DisposeMediaCapture()
        {
            if (_initializingMediaCaptureTask == null)
            {
                return;
            }

            var oldTask = _initializingMediaCaptureTask;
            _initializingMediaCaptureTask = null;
            var mediaCapture = await oldTask;
            if (mediaCapture != null)
            {
                mediaCapture.Dispose();
            }
        }
#else
        private Task ApplyVolume()
        {
            if (!_started)
            {
                return Task.CompletedTask;
            }

            ApplyVolumeWithAmplifier();
            ApplyVoiceDetectionThreshold();
            return Task.CompletedTask;
        }

        private void LoadVolume()
        {
            LoadVolumeWithAmplifier();
        }
#endif // UNITY_WINRT
    }
}
#endif
