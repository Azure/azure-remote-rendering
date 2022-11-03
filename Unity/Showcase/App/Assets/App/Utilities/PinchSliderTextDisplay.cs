// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.UI;
using TMPro;
using UnityEngine;

/// <summary>
/// Handle pinch slider events, and display the resulting text.
/// </summary>
public class PinchSliderTextDisplay : MonoBehaviour
{
    float _lastValue = 0;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The text mesh that will render value.")]
    private TextMeshPro textMesh;

    /// <summary>
    /// The text mesh that will render value.
    /// </summary>
    public TextMeshPro TextMesh
    {
        get => textMesh;
        set => textMesh = value;
    }

    [SerializeField]
    [Tooltip("The initial value to apply to the text mesh, before HandleSliderChange is called.")]
    private float initialValue = 0.0f;

    /// <summary>
    /// The initial value to apply to the text mesh, before HandleSliderChange is called.
    /// </summary>
    public float InitialValue
    {
        get => initialValue;
        set => initialValue = value;
    }

    [SerializeField]
    [Tooltip("The format string to apply to value.")]
    private string numberFormat = "{0:'+'0.00;-0.00;0}";

    /// <summary>
    /// The format string to apply to value.
    /// </summary>
    public string NumberFormat
    {
        get => numberFormat;
        set
        {
            if (numberFormat != value)
            {
                numberFormat = value;
                UpdateText(_lastValue);
            }
        }
    }
    #endregion Serialized Fields

    #region MonoBehavior Methods
    private void Start()
    {
        UpdateText(_lastValue);
    }
    #endregion MonoBehavior Methods

    #region Public Methods
    public void HandleSliderChange(SliderEventData data)
    {
        UpdateText(data.NewValue);
    }
    #endregion Public Methods

    #region Private Methods
    private void UpdateText(float value)
    {
        _lastValue = value;
        if (textMesh != null)
        {
            textMesh.text = string.Format(numberFormat, value);
        }
    }
    #endregion Private Methods
}
