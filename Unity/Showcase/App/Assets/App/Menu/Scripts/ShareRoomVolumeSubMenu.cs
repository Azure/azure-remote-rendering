// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine;

public class ShareRoomVolumeSubMenu : SubMenu
{
    ISharingService _sharingService = null;

    #region Serialized Fields
    [Header("Microphone Settings")]

    [SerializeField]
    [Tooltip("A UX componet for the shared session volume.")]
    public PinchSliderScaledValue microphoneVolumeValue = null;

    /// <summary>
    /// A UX componet for the shared session volume.
    /// </summary>
    private PinchSliderScaledValue MicrophoneVolumeValue
    {
        get => microphoneVolumeValue;
        set => microphoneVolumeValue = value;
    }

    [SerializeField]
    [Tooltip("The text displaying the microphone adjustment value.")]
    public PinchSliderTextDisplay microphoneVolumeDisplay = null;

    [SerializeField]
    [Tooltip("The UI used to enter voice calibration. This will be hidden if the sharing provider doesn't support calibration.")]
    public GameObject microphoneCalibrationUI = null;

    /// <summary>
    /// The UI used to enter voice calibration. This will be hidden if the
    /// sharing provider doesn't support calibration.
    /// </summary>
    private GameObject MicrophoneCalibrationUI
    {
        get => microphoneCalibrationUI;
        set => microphoneCalibrationUI = value;
    }

    [Header("Avatar Settings")]

    [SerializeField]
    [Tooltip("A UX componet for the avatar falloff volume.")]
    public PinchSliderScaledValue avatarFalloffValue = null;

    /// <summary>
    /// A UX componet for the shared session volume.
    /// </summary>
    private PinchSliderScaledValue AvatarFalloffValue
    {
        get => avatarFalloffValue;
        set => avatarFalloffValue = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Functions
    private void OnEnable()
    {
        _sharingService = AppServices.SharingService;
        UpdateVolumeSettingsUI();

        if (_sharingService != null)
        {
            _sharingService.AudioSettingsChanged += OnAudioSettingsChanged;
        }

        microphoneVolumeValue.ScaledValueChanged.AddListener(SetMicrophoneAdjustment);
        avatarFalloffValue.ScaledValueChanged.AddListener(SetAvatarFalloff);
    }

    private void OnDisable()
    {

        if (_sharingService != null)
        {
            _sharingService.AudioSettingsChanged -= OnAudioSettingsChanged;
        }

        microphoneVolumeValue.ScaledValueChanged.RemoveListener(SetMicrophoneAdjustment);
        avatarFalloffValue.ScaledValueChanged.RemoveListener(SetAvatarFalloff);
    }
    #endregion MonoBehavior Functions

    #region Private Functions
    private void OnAudioSettingsChanged(ISharingService sender, SharingServiceAudioSettings args)
    {
        UpdateVolumeSettingsUI();
    }

    private void SetMicrophoneAdjustment(SliderEventData data)
    {
        SetMicrophoneGain(data.NewValue);
    }

    private void SetMicrophoneGain(float decibels)
    {
        if (_sharingService != null)
        {
            var settings = _sharingService.AudioSettings;
            settings.MicrophoneAdjustment = decibels;
            AppServices.SharingService.AudioSettings = settings;
        }
    }

    private void SetAvatarFalloff(SliderEventData data)
    {
        SetAvatarFalloff(data.NewValue);
    }

    private void SetAvatarFalloff(float distance)
    {
        if (_sharingService != null)
        {
            var settings = _sharingService.AudioSettings;
            settings.PlaybackFalloffDistance = distance;
            AppServices.SharingService.AudioSettings = settings;
        }
    }

    private void UpdateVolumeSettingsUI()
    {
        if (_sharingService != null && _sharingService.IsReady)
        {
            var audioCapabilities = _sharingService.AudioCapabilities;
            microphoneVolumeDisplay.NumberFormat = audioCapabilities.MicrophoneAdjustmentNumberFormat;
            microphoneVolumeValue.MinValue = audioCapabilities.MinMicrophoneAdjustment;
            microphoneVolumeValue.MaxValue = audioCapabilities.MaxMicrophoneAdjustment;
            microphoneVolumeValue.ScaledValue = _sharingService.AudioSettings.MicrophoneAdjustment;

            if (microphoneCalibrationUI != null)
            {
                microphoneCalibrationUI.SetActive(audioCapabilities.SupportsVoiceCalibration);
            }
        }
    }
    #endregion Private Functions
}
