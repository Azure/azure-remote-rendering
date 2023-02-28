// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Logic for how the wait timer fills
/// </summary>
public class SummonMenuWaitTimer : MonoBehaviour
{
    private Coroutine _fillRoutine = null;
    private bool _fillRoutineRunning = false;
    private Vector3 _startScale;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The time, in seconds, to wait before playing an animation.")]
    private float waitTime = 1f;

    /// <summary>
    /// The time, in seconds, to wait before playing an animation.
    /// </summary>
    public float WaitTime
    {
        get => waitTime;
        set => waitTime = value;
    }

    [SerializeField]
    [Tooltip("The animcation curve used to animate in the wait circle.")]
    private AnimationCurve fillCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    /// <summary>
    /// The animcation curve used to animate in the wait circle.
    /// </summary>
    public AnimationCurve FillCurve
    {
        get => fillCurve;
        set => fillCurve = value;
    }

    [SerializeField]
    [Tooltip("The circle image to animate in.")]
    private Image circleImage;

    /// <summary>
    /// The circle image to animate in.
    /// </summary>
    public Image CircleImage
    {
        get => circleImage;
        set => circleImage = value;
    }

    [SerializeField]
    [Tooltip("Event fired when animation has completed.")]
    public UnityEvent onTimerComplete;

    /// <summary>
    /// Event fired when animation has completed.
    /// </summary>
    public UnityEvent OnTimerComplete
    {
        get => onTimerComplete;
        set => onTimerComplete = value;
    }

    [SerializeField]
    [Tooltip("The time to wait before fire completed event.")]
    public float delayAfterFill = 0.1f;

    /// <summary>
    /// The time to wait before fire completed event.
    /// </summary>
    public float DelayAfterFill
    {
        get => delayAfterFill;
        set => delayAfterFill = value;
    }

    [SerializeField]
    [Tooltip("The time to wait before resetting the state.")]
    public float delayBeforeReset = 0.3f;

    /// <summary>
    /// The time to wait before resetting the state.
    /// </summary>
    public float DelayBeforeReset
    {
        get => delayBeforeReset;
        set => delayBeforeReset = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Functions
    private void Awake()
    {
        _startScale = transform.localScale;
        circleImage.fillAmount = 0f;
    }
    #endregion MonoBehavior Functions

    #region Public Functions
    public void TriggerTimer()
    {
        StopTimer();
        _fillRoutine = StartCoroutine(FillRoutine());
    }

    public void StopTimer()
    {
        if (_fillRoutineRunning && _fillRoutine != null)
        {
            StopCoroutine(_fillRoutine);
            _fillRoutine = null;
        }

        _fillRoutineRunning = false;
        circleImage.fillAmount = 0f;
        gameObject.transform.localScale = _startScale;
        circleImage.color = Color.white;
    }
    #endregion Public Functions

    #region Private Functions
    private IEnumerator FillRoutine()
    {
        _fillRoutineRunning = true;

        float time = 0f;
        while (time < waitTime)
        {
            time += Time.deltaTime;
            circleImage.fillAmount = Mathf.Lerp(0f, 1f, fillCurve.Evaluate(time / waitTime));
            yield return null;
        }

        if (delayAfterFill > 0f)
        {
            yield return new WaitForSeconds(delayAfterFill);
        }

        onTimerComplete.Invoke();

        if (delayBeforeReset > 0f)
        {
            yield return new WaitForSeconds(delayBeforeReset);
        }

        _fillRoutineRunning = false;
    }
    #endregion Private Functions
}
