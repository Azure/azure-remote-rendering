// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.UI;
using System.Collections;
using UnityEngine;

/// <summary>
/// Convert a PinchSlider Value (0.0 - 1.0) to a scaled value between min and max.
/// </summary>
[RequireComponent(typeof(PinchSlider))]
public class PinchSliderScaledValue : MonoBehaviour
{
    private IMixedRealityPointer _lastPointerToMoveSlider = null;
    private PinchSlider _slider = null;
    private float _unscaledValue = 0.0f;
    private float _scaleValue = 0.0f;
    private float? _pendingValue = null;
    public bool _started = false;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The minimum scaled value.")]
    private float minValue = 0.0f;

    /// <summary>
    /// The minimum scaled value.
    /// </summary>
    public float MinValue
    {
        get => minValue;
        set => minValue = value;
    }

    [SerializeField]
    [Tooltip("The maximum scaled value.")]
    private float maxValue = 1.0f;

    /// <summary>
    /// The maximum scaled value.
    /// </summary>
    public float MaxValue
    {
        get => maxValue;
        set => maxValue = value;
    }

    [Header("Events")]

    [SerializeField]
    [Tooltip("The event fired when the scaled value changes.")]
    private SliderEvent scaledValueChanged = new SliderEvent();

    /// <summary>
    /// The event fired when the scaled value changes.
    /// </summary>
    public SliderEvent ScaledValueChanged => scaledValueChanged;
    #endregion Serialized Fields

    #region Properties
    /// <summary>
    /// Get the scaled value of the slider
    /// </summary>
    public float ScaledValue
    {
        get => _pendingValue != null ? _pendingValue.Value : _scaleValue;
        set => SetScaledValue(value);
    }
    #endregion Properties

    #region MonoBehavior Methods
    private void Start()
    {
        _started = true;
        _slider = GetComponent<PinchSlider>();
        if (_slider != null)
        {
            _slider.OnValueUpdated.AddListener(OnSliderValueChange);
            ApplyPendingOrUpdateValue();
        }
    }

    private void OnDestroy()
    {
        if (_slider != null)
        {
            _slider.OnValueUpdated.RemoveListener(OnSliderValueChange);
        }
    }
    #endregion MonoBehavior Methods

    #region Private Methods
    private void OnSliderValueChange(SliderEventData data)
    {
        _lastPointerToMoveSlider = data.Pointer;
        UpdateScaledValue();
    }

    private void UpdateScaledValue()
    {
        _unscaledValue = _slider.SliderValue;

        Debug.Assert(_unscaledValue <= 1.0f && _unscaledValue >= 0.0f, $"Invalid unscaled value. (value: {_unscaledValue})");
        Debug.Assert(maxValue >= minValue, $"Invalid min and max values (min: {minValue}) (max: {maxValue})");

        _unscaledValue = Mathf.Clamp01(_unscaledValue);

        float oldValue = _scaleValue;
        _scaleValue = ScaleValue(_unscaledValue);

        if (oldValue != _scaleValue)
        {
            scaledValueChanged?.Invoke(new SliderEventData(oldValue, _scaleValue, _lastPointerToMoveSlider, _slider));
        }
    }

    private void ApplyPendingOrUpdateValue()
    {
        if (_pendingValue != null)
        {
            float value = _pendingValue.Value;
            _pendingValue = null;
            StartCoroutine(LateSetScaledValue(value));
        }
        else
        {
            UpdateScaledValue();
        }
    }

    private IEnumerator LateSetScaledValue(float value)
    {
        yield return new WaitForEndOfFrame();
        SetScaledValue(value);
    }

    private void SetScaledValue(float value)
    {
        // If slider value is set before start, the position of the "grabber" becomes invalid.
        // Workaround this by deferring the application of the value during start.
        if (!_started)
        {
            _pendingValue = value;
            return;
        }


        // Make sure the behaviour is sync'ed with the slider control.
        UpdateScaledValue();

        if (_scaleValue != value)
        {
            var oldValue = _scaleValue;
            _scaleValue = value;
            _unscaledValue = UnscaleValue(value);
            if (_slider != null)
            {
                _slider.SliderValue = _unscaledValue;
            }
            scaledValueChanged?.Invoke(new SliderEventData(oldValue, _scaleValue, _lastPointerToMoveSlider, _slider));
        }
    }

    private float ScaleValue(float unscaledValue)
    {
        return (unscaledValue * (maxValue - minValue)) + minValue;
    }

    private float UnscaleValue(float scaledValue)
    {
        return (scaledValue - minValue) / (maxValue - minValue);
    }
    #endregion Private Methods
}
