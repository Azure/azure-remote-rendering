// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if PHOTON_INSTALLED
using ExitGames.Client.Photon;
using Photon.Voice.PUN;
using Photon.Voice.Unity;
using UnityEngine;
using UnityEngine.Events;

using static Photon.Voice.Unity.Recorder;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    /// <summary>
    /// Ensures that all the required mesh componets are available in the scene.
    /// </summary>
    public class PhotonComponents
    {
        private GameObject _root;
        private GameObject _components;
        private GameObject _rootComponents;
        private SharingServiceAudioSettings _audioSettings;
        private LogHelper<PhotonComponents> _logger = new LogHelper<PhotonComponents>();

        #region Public Properties
        /// <summary>
        /// Get or set the container for all sharing related game objects.
        /// </summary>
        public GameObject Root
        {
            get => _root;

            set
            {
                if (_root != value)
                {
                    _root = value;

                    if (Components != null)
                    {
                        MoveToRoot(Components.transform);
                    }
                }
            }
        }

        /// <summary>
        /// Components that need to be at the "session origin" will placed on the component
        /// </summary>
        public GameObject Components
        {
            get => _components;

            private set
            {
                if (_components != value)
                {
                    _components = value;

                    if (_components != null)
                    {
                        MoveToRoot(_components.transform);
                    }
                }
            }
        }

        /// <summary>
        /// Components that need to be at the "app root" will placed on the component
        /// </summary>
        public GameObject RootComponents
        {
            get => _rootComponents;

            private set
            {
                if (_rootComponents != value)
                {
                    _rootComponents = value;
                }
            }
        }

        /// <summary>
        /// Get Photon's VoiceNetwork component
        /// </summary>
        public PhotonVoiceNetwork VoiceNetwork { get; private set; }

        /// <summary>
        /// Get Photon's recorder
        /// </summary>
        public Recorder VoiceRecorder { get; private set; }

        /// <summary>
        /// Get the component for controlling microphone volume.
        /// </summary>
        public PhotonMicrophoneVolumeControl VolumeControl { get; private set; }
        #endregion Public Properties

        #region Constructor
        private PhotonComponents(SharingServiceProfile profile, SharingServiceAudioSettings audioSettings, GameObject root)
        {
            _root = root;
            _audioSettings = audioSettings;
            _logger.Verbose = profile.VerboseLogging ? LogHelperState.Always : LogHelperState.Default;
        }
        #endregion Constructor

        #region Public Functions
        public static PhotonComponents Create(SharingServiceProfile profile, SharingServiceAudioSettings audioSettings, GameObject root)
        {
            var result = new PhotonComponents(profile, audioSettings, root);
            result.EnsureDynamicComponentsContainer(active: false);
            result.EnsureVoiceComponents();
            result.EnsureDynamicComponentsContainer(active: true);
            return result;
        }
        #endregion Public Functions

        #region Private Functions
        /// <summary>
        /// A helper to move the given transform to the root game object.
        /// </summary>
        private void MoveToRoot(Transform moveThis)
        {
            if (moveThis != null && moveThis.gameObject != Root)
            {
                if (Root != null)
                {
                    moveThis.transform.SetParent(Root.transform, false);
                }
                else
                {
                    moveThis.transform.SetParent(null, false);
                }
            }
        }

        /// <summary>
        /// Create a container for placing dynamically created components.
        /// </summary>
        private void EnsureDynamicComponentsContainer(bool active)
        {
            if (Components == null)
            {
                Components = new GameObject("PhotonDynamicComponents");
            }

            if (RootComponents == null)
            {
                RootComponents = new GameObject("PhotonDynamicRootComponents");
            }

            Components.SetActive(active);
            RootComponents.SetActive(active);
        }

        /// <summary>
        /// Ensure voice components exist.
        /// </summary>
        private void EnsureVoiceComponents()
        {
            if (PhotonFeatureSupport.HasVoice)
            {
                VoiceNetwork = EnsureComponent<PhotonVoiceNetwork>(RootComponents);
                VoiceRecorder = EnsureComponent<Recorder>(Components);
                VoiceRecorder.MicrophoneType = MicType.Photon;
                VoiceRecorder.RecordOnlyWhenEnabled = true;
                VoiceRecorder.SourceType = InputSourceType.Microphone;
                VoiceRecorder.MicrophoneType = Application.isEditor ? MicType.Unity : MicType.Photon;
                VoiceRecorder.TransmitEnabled = true;
                VoiceRecorder.SamplingRate = GetSamplingRate(fallback: VoiceRecorder.SamplingRate);
                VoiceRecorder.VoiceDetection = true;
                VoiceRecorder.VoiceDetectionThreshold = 0.015f;
                VoiceRecorder.LogLevel = _logger.Verbose == LogHelperState.Always ? DebugLevel.ALL : DebugLevel.WARNING;
                InitializeMicrophoneDevice();

                VolumeControl = EnsureComponent<PhotonMicrophoneVolumeControl>(Components);
                VolumeControl.VerboseLogging = _logger.Verbose == LogHelperState.Always;
                VolumeControl.Volume = _audioSettings.MicrophoneAdjustment;

                VoiceNetwork.PrimaryRecorder = VoiceRecorder;
            }
            else
            {
                DeleteComponent<PhotonVoiceNetwork>();
                DeleteComponent<Recorder>();
            }
        }

        /// <summary>
        /// Get teh systems sample plyback rate.
        /// </summary>
        private POpusCodec.Enums.SamplingRate GetSamplingRate(POpusCodec.Enums.SamplingRate fallback)
        {
            int sampleRate = AudioSettings.GetConfiguration().sampleRate;
            switch (sampleRate)
            {
                case 48000:
                    return POpusCodec.Enums.SamplingRate.Sampling48000;
                case 24000:
                    return POpusCodec.Enums.SamplingRate.Sampling24000;
                case 16000:
                    return POpusCodec.Enums.SamplingRate.Sampling16000;
                case 12000:
                    return POpusCodec.Enums.SamplingRate.Sampling12000;
                case 08000:
                    return POpusCodec.Enums.SamplingRate.Sampling08000;
                default:
                    return fallback;
            }
        }

        /// <summary>
        /// Delete components of type T from the Unity scene.
        /// </summary>
        private bool DeleteComponent<T>() where T : Component
        {
            var component = UnityEngine.Object.FindObjectOfType<T>(includeInactive: true);
            if (component != null)
            {
                UnityEngine.Object.Destroy(component);
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Ensure the scene has the given component. If missing this type gets added to the given playground.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="playground"></param>
        /// <returns></returns>
        private T EnsureComponent<T>(GameObject playground) where T : Component
        {
            return UnityEngine.Object.FindObjectOfType<T>(includeInactive: true) ?? playground.EnsureComponent<T>();
        }

        /// <summary>
        /// Get if the given unity event has a present event handler with the given name and target.
        /// </summary>
        private bool HasCallback<T, U>(UnityEvent<T, U> unityEvent, UnityEngine.Object target, string methodName)
        {
            bool hasCallback = false;
            if (unityEvent != null)
            {
                int handlerCount = unityEvent.GetPersistentEventCount();
                for (int i = 0; i < handlerCount; i++)
                {
                    if (unityEvent.GetPersistentMethodName(i) == methodName &&
                        unityEvent.GetPersistentTarget(i) == target)
                    {
                        hasCallback = true;
                    }
                }
            }
            return hasCallback;
        }

        private void InitializeMicrophoneDevice()
        {
            var devices = VoiceRecorder.MicrophonesEnumerator;
            if (devices == null || !devices.IsSupported)
            {
                _logger.LogError("No microphone devices found.");
            }
            else
            {
                foreach (var device in devices)
                {
                    _logger.LogVerbose("[Microphone Device] '{0}' ({1}:{2})", device.Name, device.IDInt, device.IDString);
                    VoiceRecorder.MicrophoneDevice = device;
                    break;
                }
            }
        }
        #endregion Private Region
    }
}
#endif // PHOTON_INSTALL
