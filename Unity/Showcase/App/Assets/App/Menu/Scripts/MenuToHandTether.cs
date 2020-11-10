// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

/// <summary>
/// Draws a tether between an anchor point and the palm of a hand that fades over time.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class MenuToHandTether : MonoBehaviour
{
    private LineRenderer _lineRenderer;
    private float _currentTime = 0f;

    #region Serialized Fields
    [Header("Animation Settings")]

    [SerializeField]
    [Tooltip("The length, in seconds, of the fade animation.")]
    private float fadeTime = 1f;

    /// <summary>
    /// The length, in seconds, of the fade animation.
    /// </summary>
    public float FadeTime
    {
        get => fadeTime;
        set => fadeTime = value;
    }

    [SerializeField]
    [Tooltip("The animation curve of the fade animation.")]
    private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    /// <summary>
    /// The animation curve of the fade animation.
    /// </summary>
    public AnimationCurve Curve
    {
        get => curve;
        set => curve = value;
    }

    [Header("Parts")]

    [SerializeField]
    [Tooltip("A line is draw from the 'wait timer' to the 'menu'. This is the 'wait timer', which should be placed on the user's hand.")]
    private GameObject waitTimerObject = null;

    /// <summary>
    /// A line is draw from the 'wait timer' to the 'menu'. This is the 'wait timer', which should be placed on the user's hand.
    /// </summary>
    public GameObject WaitTimerObject
    {
        get => waitTimerObject;
        set => waitTimerObject = value;
    }

    [SerializeField]
    [Tooltip("A line is drawn from the 'wait timer' to the 'menu'. This is the 'menu' point, which should be placed at the bottom of the app's menu control.")]
    private GameObject menuAnchor = null;

    /// <summary>
    /// A line is drawn from the 'wait timer' to the 'menu'. This is the 'menu' point, which should be placed at the bottom of the app's menu control.
    /// </summary>
    public GameObject MenuAnchor
    {
        get => menuAnchor;
        set => menuAnchor = value;
    }
    #endregion Serialized Fields

    #region Public Properties
    /// <summary>
    /// Get or set the current hand position.
    /// </summary>
    public Vector3 HandPosition { get; set; } = Vector3.zero;
    #endregion Public Properties

    #region MonoBehavior Functions
    private void Start()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        ResetLine();
    }

    private void Update()
    {
        if (_lineRenderer == null)
        {
            return;
        }

        if (menuAnchor)
        {
            _lineRenderer.SetPosition(0, menuAnchor.transform.position);
            _lineRenderer.SetPosition(1, waitTimerObject.transform.position);
        }

        if (_currentTime < fadeTime)
        {
            _currentTime += Time.deltaTime;
            Color c = Color.Lerp(Color.white, new Color(1f, 1f, 1f, 0.4f), curve.Evaluate(_currentTime / fadeTime));
            _lineRenderer.material.color = c;
            _lineRenderer.startWidth = Mathf.Lerp(0.0025f, 0.001f, curve.Evaluate(_currentTime / fadeTime));
            _lineRenderer.endWidth = Mathf.Lerp(0.0025f, 0.001f, curve.Evaluate(_currentTime / fadeTime));
        }
    }
    #endregion MonoBehavior Functions

    #region Public Functions
    public void TriggerHand()
    {
        _currentTime = 0f;
    }

    public void ResetLine()
    {
        if (_lineRenderer != null)
        {
            _currentTime = fadeTime + 1f;
            _lineRenderer.startWidth = 0f;
            _lineRenderer.endWidth = 0f;
            _lineRenderer.material.color = Color.clear;
        }
    }
    #endregion Public Functions
}
