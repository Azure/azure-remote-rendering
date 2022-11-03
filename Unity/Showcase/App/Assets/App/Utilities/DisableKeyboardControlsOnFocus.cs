// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.MixedReality.Toolkit.Utilities;
using TMPro;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Input
{
    /// <summary>
    /// Utility component disable keyboard controls, like camera movement, when the input fields has focus
    /// </summary>
    public class DisableKeyboardControlsOnFocus : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The input field that will be watched for focus events.")]
        private TMP_InputField inputField;

        public TMP_InputField InputField
        { 
            get => inputField;
            set => inputField = value;
        }

        #region MonoBehavior Methods
        private void OnEnable()
        {
            if (inputField == null)
            {
                inputField = GetComponent<TMP_InputField>();
            }

            if (inputField != null)
            {
                inputField.onSelect.AddListener(OnSelected);
                inputField.onDeselect.AddListener(OnDeselected);
            }
        }

        private void OnDisable()
        {
            if (inputField != null)
            {
                inputField.onSelect.RemoveListener(OnSelected);
                inputField.onDeselect.RemoveListener(OnDeselected);
            }
        }
        #endregion MonoBehavior Methods

        #region Private Methods
        public void OnSelected(string value)
        {
            SetCameraControlsEnablement(false);
            SetInputSimulationEnablement(false);
            // Disable speech, becuase of speech shortcuts
            SetSpeechEnablement(false);
        }
        public void OnDeselected(string value)
        {
            SetCameraControlsEnablement(true);
            SetInputSimulationEnablement(true);
            SetSpeechEnablement(true);
        }

        private void SetCameraControlsEnablement(bool enable)
        {
            var cameraControls = CameraCache.Main.GetComponentInChildren<CameraControl>();
            if (cameraControls != null)
            {
                cameraControls.enabled = enable;
            }
        }

        private void SetInputSimulationEnablement(bool enable)
        {
            var service = CoreServices.GetInputSystemDataProvider<InputSimulationService>();
            if (service != null)
            {
                if (enable)
                {
                    service.Enable();
                }
                else
                {
                    service.Disable();
                }

                service.UserInputEnabled = enable;
            }
        }

        private void SetSpeechEnablement(bool enable)
        {
            var service = CoreServices.GetInputSystemDataProvider<IMixedRealitySpeechSystem>();
            if (service != null)
            {
                if (enable)
                {
                    service.Enable();
                }
                else
                {
                    service.Disable();
                }
            }
        }
        #endregion Private Methods
    }
}