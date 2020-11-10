// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using TMPro;
using UnityEngine;

/// <summary>
/// Used to update a text object over time given an array of strings
/// </summary>
public class AnimateText : MonoBehaviour
{
    private RectTransform _mainTextRectTransform;
    private RectTransform _cloneTextRectTransform;
    private TextData[] _textDataToAnimate = new TextData[0];

    private float _notificationBarWidth;
    private float _textAreaWidth;

    private bool _textNeedsScrolling = false;
    private float _scrollPosition = 0.0f;
    private Vector3 _startPosition;

    private int _currentTextIndex = 0;
    private float _currentAnimationTime = 0f;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The text scroll speed")]
    [Min(0.0f)]
    public float scrollSpeed = 0.03f;
    /// <summary>
    /// The text scroll speed.
    /// </summary>
    public float ScrollSpeed
    {
        get => scrollSpeed;
        set => scrollSpeed = value;
    }

    [SerializeField]
    [Tooltip("The gap after a scrolling message")]
    [Min(0.0f)]
    public float scrollGap = 0.02f;
    /// <summary>
    /// The gap after a scrolling message.
    /// </summary>
    public float ScrollGap
    {
        get => scrollGap;
        set => scrollGap = value;
    }

    [SerializeField]
    [Tooltip("The color to use for info text.")]
    public Color infoColor;
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
    [Tooltip("The duration that each message is shown in 'Switching' animation mode.")]
    [Min(0.0f)]
    private float textAnimationLength = 0.3f;

    /// <summary>
    /// The duration that each message is shown in 'Switching' animation mode.
    /// </summary>
    public float TextAnimationLength
    {
        get => textAnimationLength;
        set => textAnimationLength = value;
    }

    [Header("UI Parts")]

    [SerializeField]
    [Tooltip("The first text mesh to use when animating text.")]
    private TextMeshProUGUI textFront = null;

    /// <summary>
    /// The first text mesh to use when animating text.
    /// </summary>
    public TextMeshProUGUI TextFront
    {
        get => textFront;
        set => textFront = value;
    }

    [SerializeField]
    [Tooltip("The second text mesh to use when animating text, if the first mesh couldn't fit the entire string.")]
    private TextMeshProUGUI textBack = null;

    /// <summary>
    /// The first text mesh to use when animating text.
    /// </summary>
    public TextMeshProUGUI TextBack
    {
        get => textFront;
        set => textFront = value;
    }
    #endregion Serialized Fields

    #region Public Properties
    /// <summary>
    /// Message is visible at once or has fully scrolled through the text area at least once.
    /// </summary>
    public bool MessageShownCompletely { get; private set; } = false;

    /// <summary>
    /// The strings to start animating.
    /// </summary>
    public TextData[] TextDataToAnimate
    {
        get => _textDataToAnimate;

        set
        {
            _textDataToAnimate = value;
            _scrollPosition = 0.0f;
            _currentAnimationTime = 0.0f;
            _currentTextIndex = 0;

            var newText = value?.Length > 0 ? value[0].Text : "";
            UpdateText(newText);

            var newType = value?.Length > 0 ? value[0].Type : AppNotificationType.Info;
            UpdateColor(newType);
        }
    }

    /// <summary>
    /// The type of animation to be applied.
    /// </summary>
    public AnimationType CurrentAnimationType { get; set; } = AnimationType.Switching;
    #endregion Public Properties

    #region MonoBehavior Functions
    private void Awake()
    {

        _mainTextRectTransform = textFront.GetComponent<RectTransform>();
        Debug.Assert(_mainTextRectTransform != null, "Text Front needs to have a RectTransform.");

        _cloneTextRectTransform = textBack.GetComponent<RectTransform>();
        Debug.Assert(_cloneTextRectTransform != null, "Text Back needs to have a RectTransform.");

        _textAreaWidth = _mainTextRectTransform.rect.width;
        _startPosition = _mainTextRectTransform.localPosition;
        _notificationBarWidth = _textAreaWidth;

        if (_textDataToAnimate?.Length > 0)
        {
            UpdateText(_textDataToAnimate[0].Text);
            UpdateColor(_textDataToAnimate[0].Type);
        }
    }

    private void Update()
    {
        switch (CurrentAnimationType)
        {
            case AnimationType.Switching:
                if (_textDataToAnimate?.Length > 1 && textAnimationLength > 0.0f)
                {
                    _currentAnimationTime += Time.deltaTime;
                    if (_currentAnimationTime >= textAnimationLength)
                    {
                        _currentAnimationTime = 0f;
                        _currentTextIndex = (_currentTextIndex + 1) % _textDataToAnimate.Length;
                        UpdateText(_textDataToAnimate[_currentTextIndex].Text);
                        UpdateColor(_textDataToAnimate[_currentTextIndex].Type);
                    }
                }
                break;

            case AnimationType.Scrolling:
                if (_textNeedsScrolling)
                {
                    _mainTextRectTransform.localPosition = new Vector3(_startPosition.x - _scrollPosition, _startPosition.y, _startPosition.z);
                    _cloneTextRectTransform.localPosition = new Vector3(_startPosition.x - _scrollPosition + _mainTextRectTransform.rect.width, _startPosition.y, _startPosition.z);

                    _scrollPosition += scrollSpeed * Time.deltaTime;

                    if (_scrollPosition >= _mainTextRectTransform.rect.width)
                    {
                        _scrollPosition -= _mainTextRectTransform.rect.width;
                        MessageShownCompletely = true;
                    }
                }
                break;
        }
    }
    #endregion MonoBehavior Functions

    #region Private Functions
    private void UpdateColor(AppNotificationType type)
    {
        switch (type)
        {
            case AppNotificationType.Info:
                textFront.color = infoColor;
                textBack.color = infoColor;
                break;
            case AppNotificationType.Warning:
                textFront.color = warningColor;
                textBack.color = warningColor;
                break;
            case AppNotificationType.Error:
                textFront.color = errorColor;
                textBack.color = errorColor;
                break;
        }
    }

    private void UpdateText(string newText)
    {
        if (_mainTextRectTransform == null || _cloneTextRectTransform == null)
        {
            return;
        }

        textFront.text = newText;
        if (textFront.preferredWidth > _textAreaWidth)
        {
            textBack.text = newText;
            _mainTextRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textFront.preferredWidth + scrollGap);
            _mainTextRectTransform.localPosition = new Vector3(_startPosition.x, _startPosition.y, _startPosition.z);
            _cloneTextRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textBack.preferredWidth);
            _cloneTextRectTransform.localPosition = new Vector3(_startPosition.x + _mainTextRectTransform.rect.width, _startPosition.y, _startPosition.z);
            _textNeedsScrolling = true;
            MessageShownCompletely = false;
        }
        else
        {
            textBack.text = "";
            _mainTextRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _textAreaWidth);
            _textNeedsScrolling = false;
            MessageShownCompletely = true;
            _mainTextRectTransform.localPosition = new Vector3(_startPosition.x, _startPosition.y, _startPosition.z);
            _cloneTextRectTransform.localPosition = new Vector3(_startPosition.x + _notificationBarWidth, _startPosition.y, _startPosition.z);
        }
    }
    #endregion Public Functions

    #region Public Classes
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
    #endregion Public Classes

    #region Public Enum
    public enum AnimationType
    {
        // Displays a single text message, scrolling it through the text area if it is too long
        Scrolling,
        // Displays multiple messages, switching from one to the next in fixed time steps.
        Switching,
    }
    #endregion Public Enum
}
