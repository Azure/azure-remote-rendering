// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;

/// <summary>
/// A behavior for hiding the menu while any camera movement is happening.
/// </summary>
public class HideMenuMovement : MonoBehaviour
{
    private Transform _camera;
    private Vector3 _lastMeterForward;
    private float _hideTimeInSeconds = float.MaxValue;
    private float _reshowTimeInSeconds = float.MaxValue;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The delay before the menu is hidden after camera movement.")]
    private float hideDelayInSeconds = 0.0f;

    /// <summary>
    /// The delay before the menu is hidden after camera movement.
    /// </summary>
    public float HideDelayInSeconds
    {
        get => hideDelayInSeconds;
        set => hideDelayInSeconds = value;
    }

    [SerializeField]
    [Tooltip("The delay before the menu is shown after camera movement.")]
    private float showDelayInSeconds = 0.3f;

    /// <summary>
    /// The delay before the menu is shown after camera movement.
    /// </summary>
    public float ShowDelayInSeconds
    {
        get => showDelayInSeconds;
        set => showDelayInSeconds = value;
    }

    [SerializeField]
    [Tooltip("The target to hide and show.")]
    private GameObject target = null;

    /// <summary>
    /// The target to hide and show.
    /// </summary>
    public GameObject Target
    {
        get => target;
        set => target = value;
    }
    #endregion Serialized Fields

    #region Public Properties
    /// <summary>
    /// Get if the menu should be visible.
    /// </summary>
    public bool IsVisible { get; private set; }
    #endregion Public Properties

    #region MonoBehavior Functions
    private void Start()
    {
        _camera = CameraCache.Main.transform;
        _lastMeterForward = _camera.position + _camera.forward;
        IsVisible = true;
    }

    private void LateUpdate()
    {
        var meterForward = _camera.position + _camera.forward;
        if (_lastMeterForward != meterForward)
        {
            _reshowTimeInSeconds = float.MaxValue;
            if (_hideTimeInSeconds == float.MaxValue)
            {
                _hideTimeInSeconds = Time.realtimeSinceStartup + hideDelayInSeconds;
            }
        }

        if (Time.realtimeSinceStartup >= _reshowTimeInSeconds)
        {
            _hideTimeInSeconds = float.MaxValue;
            _reshowTimeInSeconds = float.MaxValue;
            SetVisible(true);
        }
        else if (Time.realtimeSinceStartup >= _hideTimeInSeconds)
        {
            SetVisible(false); 
            if (_reshowTimeInSeconds == float.MaxValue)
            {
                _reshowTimeInSeconds = Time.realtimeSinceStartup + showDelayInSeconds;
            }
        }

        _lastMeterForward = meterForward;
    }
    #endregion MonoBehavior Functions

    #region Private Functions
    private void SetVisible(bool visible)
    {
        IsVisible = visible;
        if (target != null)
        {
            target.SetActive(visible);
        }
    }
    #endregion Private Functions
}