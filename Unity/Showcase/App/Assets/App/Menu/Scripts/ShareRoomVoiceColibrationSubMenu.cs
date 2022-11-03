// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.UI;
using System;
using System.Collections;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public class ShareRoomVoiceColibrationSubMenu : SubMenu
{
    TaskCompletionSource<bool> _progressIndicatorReady;
    ISharingService _sharingService = null;
    bool _calibrating = false;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The UI shown when voice calibration is active.")]
    public GameObject microphoneCalibratingUI = null;

    /// <summary>
    /// The UI shown when voice calibration is active.
    /// </summary>
    private GameObject MicrophoneCalibratingUI
    {
        get => microphoneCalibratingUI;
        set => microphoneCalibratingUI = value;
    }

    [SerializeField]
    [Tooltip("The progress indicator to shown when microphone is being calibrated.")]
    private ProgressIndicatorOrbsRotator microphoneProgressIndicator = null;

    /// <summary>
    /// The progress indicator to shown when microphone is being calibrated.
    /// </summary>
    public ProgressIndicatorOrbsRotator MicrophoneProgressIndicator
    {
        get => microphoneProgressIndicator;
        set => microphoneProgressIndicator = value;
    }

    [SerializeField]
    [Tooltip("The UI shown when voice calibration is done.")]
    public GameObject microphoneCalibrationCompletedUI = null;

    /// <summary>
    /// The UI shown when voice calibration is done.
    /// </summary>
    private GameObject MicrophoneCalibrationCompletedUI
    {
        get => microphoneCalibrationCompletedUI;
        set => microphoneCalibrationCompletedUI = value;
    }

    [SerializeField]
    [Tooltip("The text field that will display result status messages.")]
    public TextMeshPro resultTextField = null;

    /// <summary>
    /// The text field that will display result status messages.
    /// </summary>
    private TextMeshPro ResultTextField
    {
        get => resultTextField;
        set => resultTextField = value;
    }
    #endregion Serialized Fields

    #region Public Functions
    /// <summary>
    /// Invoke this when the sub-menu has been activated.
    /// </summary>
    public override void OnActivated()
    {
        base.OnActivated();
        _sharingService = AppServices.SharingService;
        StartWork();
    }

    /// <summary>
    /// Invoke this when the sub-menu has been deactivated.
    /// </summary>
    public override void OnDeactivated()
    {
        base.OnDeactivated();
        HideUI();
        _progressIndicatorReady?.TrySetResult(false);
    }
    #endregion Public Functions

    #region Private Functions
    private async void StartWork()
    {
        InitializeResultStatus();
        ShowUI(calibrating: true);
        await UpdateProgressIndicator(play: true);
        if (_calibrating)
        {
            return;
        }

        _calibrating = true;

        // Avoid calibration if calibration UI was disabled
        bool success = false;
        if (isActiveAndEnabled)
        {
            success = await _sharingService.CalibrateVoiceDetection();
        }

        await UpdateProgressIndicator(play: false);
        UpdateResultStatus(success);
        ShowUI(calibrating: false);

        _calibrating = false;
    }

    private void InitializeResultStatus()
    {
        if (resultTextField != null)
        {
            resultTextField.text = string.Empty;
        }
    }

    private void UpdateResultStatus(bool success)
    {
        if (success)
        {
            resultTextField.text = "Success! Calibration has finished";
        }
        else
        {
            resultTextField.text = "Unable to calibrate your microphone";
        }
    }

    private void HideUI()
    {
        if (microphoneCalibratingUI != null)
        {
            microphoneCalibratingUI.SetActive(false);
        }

        if (microphoneCalibrationCompletedUI != null)
        {
            microphoneCalibrationCompletedUI.SetActive(false);
        }
    }

    private void ShowUI(bool calibrating)
    {
        if (microphoneCalibratingUI != null)
        {
            microphoneCalibratingUI.SetActive(calibrating);
        }

        if (microphoneCalibrationCompletedUI != null)
        {
            microphoneCalibrationCompletedUI.SetActive(!calibrating);
        }
    }

    private async Task UpdateProgressIndicator(bool play)
    {
        if (await WaitForActiveIndicator())
        {
            if (play)
            {
                if (microphoneProgressIndicator.State == ProgressIndicatorState.Closed)
                {
                    await microphoneProgressIndicator.OpenAsync();
                }
            }
            else if (microphoneProgressIndicator.State == ProgressIndicatorState.Open)
            {
                await microphoneProgressIndicator.CloseAsync();
            }
        }
    }

    private Task<bool> WaitForActiveIndicator()
    {
        if (!isActiveAndEnabled || microphoneProgressIndicator == null)
        {
            return Task.FromResult(false);
        }

        if (ProgressIndicatorReady())
        {
            return Task.FromResult(true);
        }

        if (_progressIndicatorReady == null || _progressIndicatorReady.Task.IsCompleted)
        {
            _progressIndicatorReady = new TaskCompletionSource<bool>();
        }

        StartCoroutine(WaitForActiveWorker(_progressIndicatorReady));
        return _progressIndicatorReady.Task;
    }

    private IEnumerator WaitForActiveWorker(TaskCompletionSource<bool> taskSource)
    {
        do
        {
            yield return new WaitForEndOfFrame();
        } while (microphoneProgressIndicator != null && !ProgressIndicatorReady());
        taskSource.TrySetResult(microphoneProgressIndicator != null);
    }

    private bool ProgressIndicatorReady()
    {
        return microphoneProgressIndicator.isActiveAndEnabled &&
            (microphoneProgressIndicator.State == ProgressIndicatorState.Closed ||
            microphoneProgressIndicator.State == ProgressIndicatorState.Open);
    }
    #endregion Private Functions
}
