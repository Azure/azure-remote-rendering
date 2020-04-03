// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using TMPro;

/// <summary>
/// Used to update a text object over time given an array of strings
/// </summary>
public class AnimateText : MonoBehaviour
{
    TextMeshPro _text;
    int _index = 0;
    float _time = 0f;

    public class TextData
    {
        public TextData(string text, AppNotificationType type)
        {
            Text = text;
            Type = type;
        }

        public string Text;
        public AppNotificationType Type;
    }

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The color to use for normal text.")]
    private Color infoColor;

    /// <summary>
    /// The color to use for info text.
    /// </summary>
    public Color InfoColor
    {
        get => infoColor;
        set => infoColor = value;
    }

    [SerializeField]
    [Tooltip("The color to use for warnings text.")]
    private Color warningColor;

    /// <summary>
    /// The color to use for info text.
    /// </summary>
    public Color WarningColor
    {
        get => warningColor;
        set => warningColor = value;
    }

    [SerializeField]
    [Tooltip("The color to use for error text.")]
    private Color errorColor;

    /// <summary>
    /// The color to use for info text.
    /// </summary>
    public Color ErrorColor
    {
        get => errorColor;
        set => errorColor = value;
    }

    [SerializeField]
    [Tooltip("The strings to start animating.")]
    private TextData[] textDataToAnimate = new TextData[0];

    /// <summary>
    /// The strings to start animating.
    /// </summary>
    public TextData[] TextDataToAnimate
    {
        get => textDataToAnimate;
        set => textDataToAnimate = value;
    }

    [SerializeField]
    [Tooltip("The total length of the animation, in seconds.")]
    private float animationLength = 0.3f;

    /// <summary>
    /// The total length of the animation, in seconds.
    /// </summary>
    public float AnimationLength
    {
        get => animationLength;
        set => animationLength = value;
    }

    [SerializeField]
    [Tooltip("Should the text be animated.")]
    private bool shouldAnimate = true;

    /// <summary>
    /// Should the text be animated.
    /// </summary>
    public bool ShouldAnimate
    {
        get => shouldAnimate;
        set => shouldAnimate = value;
    }
    #endregion Serialized Fields

    private void UpdateColor(AppNotificationType type)
    {
        switch (type)
        {
            case AppNotificationType.Info:
                _text.color = infoColor;
                break;
            case AppNotificationType.Warning:
                _text.color = warningColor;
                break;
            case AppNotificationType.Error:
                _text.color = errorColor;
                break;
        }
    }

    #region MonoBehavior Functions
    private void Start()
    {
        _text = GetComponent<TextMeshPro>();
        if (textDataToAnimate != null && textDataToAnimate.Length > 0)
        {
            _text.text = TextDataToAnimate[0].Text;
            UpdateColor(TextDataToAnimate[0].Type);
        }
    }

    private void Update()
    {
        if (shouldAnimate && textDataToAnimate != null && textDataToAnimate.Length > 0)
        {
            _time += Time.deltaTime;
            if (_time >= animationLength)
            {
                _time = 0f;
                _index = (_index + 1) % TextDataToAnimate.Length;
                _text.text = TextDataToAnimate[_index].Text;
                UpdateColor(TextDataToAnimate[_index].Type);
            }
        }
    }
    #endregion MonoBehavior Functions
}
