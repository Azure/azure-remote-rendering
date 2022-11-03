// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Used to control the fill of a progress bar
/// </summary>
public class ProgressBarFill : MonoBehaviour
{
    private Coroutine _fillRoutine = null;
    private bool _fillRoutineRunning = false;
    float _fillAmount = 0f;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The animation curve used to fill the progress bar.")]
    public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    /// <summary>
    /// The animation curve used to fill the progress bar.
    /// </summary>
    public AnimationCurve Curve
    {
        get => curve;
        set => curve = value;
    }

    [SerializeField]
    [Tooltip("The time, in seconds, taken to fill the bar to the current goal.")]
    public float fillTime = 0.5f;

    /// <summary>
    /// The time, in seconds, taken to fill the bar to the current goal.
    /// </summary>
    public float FillTime
    {
        get => fillTime;
        set => fillTime = value;
    }

    [SerializeField]
    [Tooltip("The progress bar image to fill.")]
    private Image progressBarImage = null;

    /// <summary>
    /// The progress bar image to fill.
    /// </summary>
    public Image ProgressBarImage
    {
        get => progressBarImage;
        set => progressBarImage = value;
    }
    #endregion Serialized Fields

    #region Public Properties
    /// <summary>
    /// Get or set the current fill amount goal.
    /// </summary>
    public float FillAmount
    {
        get => _fillAmount;
        set => SetFillNow(value);
    }
    #endregion Public Properties

    #region Public Functions
    /// <summary>
    /// Set the current fill amount using the animation curve.
    /// </summary>
    public void AnimateFill(float fillAmount)
    {
        _fillAmount = fillAmount;

        float goalFill = Mathf.Clamp01(fillAmount);
        if (_fillRoutineRunning && _fillRoutine != null)
        {
            StopCoroutine(_fillRoutine);
            _fillRoutine = null;
        }

        if (isActiveAndEnabled)
        {
            _fillRoutine = StartCoroutine(FillRoutine(goalFill));
        }
        else
        {
            SetFillNow(goalFill);
        }
    }
    #endregion Public Functions

    #region Private Functions
    /// <summary>
    /// Set the current fill amount without using an animation.
    /// </summary>
    private void SetFillNow(float fillPercent01)
    {
        _fillAmount = fillPercent01;
        progressBarImage.fillAmount = Mathf.Clamp01(fillPercent01);
    }

    private IEnumerator FillRoutine(float goalFill)
    {
        _fillRoutineRunning = true;

        float startFill = progressBarImage.fillAmount;
        float t = 0f;

        while (t < FillTime)
        {
            t += Time.deltaTime;
            progressBarImage.fillAmount = Mathf.Lerp(startFill, goalFill, Curve.Evaluate(t / FillTime));
            yield return null;
        }

        _fillRoutineRunning = false;
    }
    #endregion Private Functions
}
