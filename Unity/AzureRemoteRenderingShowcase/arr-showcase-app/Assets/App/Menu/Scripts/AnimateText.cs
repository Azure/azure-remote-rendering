// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using TMPro;

/// <summary>
/// Used to update a text object over time given an array of strings
/// </summary>
public class AnimateText : MonoBehaviour
{
    private TextMeshProUGUI _mainText;
    private Transform _mainTextTransform;
    private RectTransform _mainTextRectTransform;

    private TextMeshProUGUI _cloneText;
    private Transform _cloneTextTransform;
    private RectTransform _cloneTextRectTransform;

    private float _notificationBarWidth;
    private float _textAreaWidth;

    private bool _textNeedsScrolling = false;
    private float _scrollPosition = 0.0f;
    private Vector3 _startPosition;

    private int _currentTextIndex = 0;
    private float _currentAnimationTime = 0f;

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

    public enum AnimationType
    {
        // Displays a single text message, scrolling it through the text area if it is too long
        Scrolling,
        // Displays multiple messages, switching from one to the next in fixed time steps.
        Switching,
    }

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
    #endregion Serialized Fields

    private bool messageShownCompletely = false;

    /// <summary>
    /// Message is visible at once or has fully scrolled through the text area at least once.
    /// </summary>
    public bool MessageShownCompletely
    {
        get => messageShownCompletely;
    }

    private TextData[] textDataToAnimate = new TextData[0];

    /// <summary>
    /// The strings to start animating.
    /// </summary>
    public TextData[] TextDataToAnimate
    {
        set
        {
            textDataToAnimate = value;
            _scrollPosition = 0.0f;
            _currentAnimationTime = 0.0f;
            _currentTextIndex = 0;
            var newText = value?.Length > 0 ? value[0].Text : "";
            UpdateText(newText);
            var newType = value?.Length > 0 ? value[0].Type : AppNotificationType.Info;
            UpdateColor(newType);
        }
    }

    private AnimationType currentAnimationType = AnimationType.Switching;
    public AnimationType CurrentAnimationType
    {
        get => currentAnimationType;
        set => currentAnimationType = value;
    }

    private void UpdateColor(AppNotificationType type)
    {
        switch (type)
        {
            case AppNotificationType.Info:
                _mainText.color = infoColor;
                _cloneText.color = infoColor;
                break;
            case AppNotificationType.Warning:
                _mainText.color = warningColor;
                _cloneText.color = warningColor;
                break;
            case AppNotificationType.Error:
                _mainText.color = errorColor;
                _cloneText.color = errorColor;
                break;
        }
    }

    private void UpdateText(string newText)
    {
        _mainText.text = newText;
        if (_mainText.preferredWidth > _textAreaWidth)
        {
            _cloneText.text = newText;
            _mainTextRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _mainText.preferredWidth + scrollGap);
            _mainTextRectTransform.position = new Vector3(_startPosition.x, _startPosition.y, _startPosition.z);
            _cloneTextRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _cloneText.preferredWidth);
            _cloneTextRectTransform.position = new Vector3(_startPosition.x + _mainTextRectTransform.rect.width, _startPosition.y, _startPosition.z);
            _textNeedsScrolling = true;
            messageShownCompletely = false;
        }
        else
        {
            _cloneText.text = "";
            _mainTextRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _textAreaWidth);
            _textNeedsScrolling = false;
            messageShownCompletely = true;
            _mainTextRectTransform.position = new Vector3(_startPosition.x, _startPosition.y, _startPosition.z);
            _cloneTextRectTransform.position = new Vector3(_startPosition.x + _notificationBarWidth, _startPosition.y, _startPosition.z);
        }
    }

    #region MonoBehavior Functions
    private void Awake()
    {
        _mainTextTransform = gameObject.transform.Find("TextFront");
        _mainText = _mainTextTransform.GetComponent<TextMeshProUGUI>();
        _mainTextRectTransform = _mainText.GetComponent<RectTransform>();

        _cloneTextTransform = gameObject.transform.Find("TextBack");
        _cloneText = _cloneTextTransform.GetComponent<TextMeshProUGUI>();
        _cloneTextRectTransform = _cloneText.GetComponent<RectTransform>();

        _textAreaWidth = _mainTextRectTransform.rect.width;
        _startPosition = _mainTextRectTransform.position;
        _notificationBarWidth = _mainText.transform.GetComponent<RectTransform>().rect.width;

        if (textDataToAnimate?.Length > 0)
        {
            UpdateText(textDataToAnimate[0].Text);
            UpdateColor(textDataToAnimate[0].Type);
        }
    }

    private void Update()
    {
        switch (currentAnimationType)
        {
            case AnimationType.Switching:
                if (textDataToAnimate?.Length > 1 && textAnimationLength > 0.0f)
                {
                    _currentAnimationTime += Time.deltaTime;
                    if (_currentAnimationTime >= textAnimationLength)
                    {
                        _currentAnimationTime = 0f;
                        _currentTextIndex = (_currentTextIndex + 1) % textDataToAnimate.Length;
                        UpdateText(textDataToAnimate[_currentTextIndex].Text);
                        UpdateColor(textDataToAnimate[_currentTextIndex].Type);
                    }
                }
                break;
            case AnimationType.Scrolling:
                if (_textNeedsScrolling)
                {
                    _mainTextRectTransform.position = new Vector3(_startPosition.x - _scrollPosition, _startPosition.y, _startPosition.z);
                    _cloneTextRectTransform.position = new Vector3(_startPosition.x - _scrollPosition + _mainTextRectTransform.rect.width, _startPosition.y, _startPosition.z);

                    _scrollPosition += scrollSpeed * Time.deltaTime;

                    if (_scrollPosition >= _mainTextRectTransform.rect.width)
                    {
                        _scrollPosition -= _mainTextRectTransform.rect.width;
                        messageShownCompletely = true;
                    }
                }
                break;
        }
    }
    #endregion MonoBehavior Functions
}
