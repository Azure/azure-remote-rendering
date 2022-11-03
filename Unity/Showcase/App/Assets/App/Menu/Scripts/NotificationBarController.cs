// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using UnityEngine;
using UnityEngine.Events;

using static AnimateText;

/// <summary>
/// Controls when the notification bar appears, how long it appears, and what it says.
/// </summary>
public class NotificationBarController : MonoBehaviour
{
    private AnimateText _textAnimation;
    private float _currentTime = 0f;
    private float _durationTime = 0f;
    private bool _isLoadingModel = false;
    private const string _loadingModelStringFormat = "Loading model: {0:F2}%";
    // Notifications override download progress
    private bool _isDisplayingNotification = false;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("How long a notification is displayed.")]
    [Min(0.0f)]
    private float notificationDuration = 15.0f;

    /// <summary>
    /// How long a notification is displayed.
    /// </summary>
    public float NotificationDuration
    {
        get => notificationDuration;
        set => notificationDuration = value;
    }

    [SerializeField]
    [Tooltip("The object that displays notification messages to the user.")]
    private GameObject notificationBar = null;

    /// <summary>
    /// The object that displays notification messages to the user.
    /// </summary>
    public GameObject NotificationBar
    {
        get => notificationBar;
        set => notificationBar = value;
    }

    [SerializeField]
    [Tooltip("The object that displays download progress to the user.")]
    private ProgressBarFill progressBar = null;

    /// <summary>
    /// The object that displays download progress to the user.
    /// </summary>
    public ProgressBarFill ProgressBar
    {
        get => progressBar;
        set => progressBar = value;
    }

    [Header("Events")]

    [SerializeField]
    [Tooltip("Event raised when the notification bar is hidden.")]
    private UnityEvent notificationBarHidden = new UnityEvent();

    /// <summary>
    /// Event raised when the notification bar is hidden.
    /// </summary>
    public UnityEvent NotificationBarHidden => notificationBarHidden;
    #endregion Serialized Fields

    #region MonoBehavior Functions
    private void Start()
    {
        _textAnimation = notificationBar.GetComponentInChildren<AnimateText>();
    }

    private void Awake()
    {
        AppServices.AppNotificationService.NotificationRaised += OnNotification;
    }

    private void OnDestroy()
    {
        AppServices.AppNotificationService.NotificationRaised -= OnNotification;
    }

    private void OnNotification(object sender, IAppNotificationRaisedData data)
    {
        switch (data.Type)
        {
            case AppNotificationType.Info:
                SetScrollableNotification(notificationDuration, data.Message, AppNotificationType.Info);
                break;
            case AppNotificationType.Warning:
                SetScrollableNotification(notificationDuration, data.Message, AppNotificationType.Warning);
                break;
            case AppNotificationType.Error:
                SetScrollableNotification(notificationDuration, data.Message, AppNotificationType.Error);
                break;
        }
    }

    private void Update()
    {
        if (_isDisplayingNotification)
        {
            _currentTime += Time.deltaTime;
            if (_durationTime >= 0.0f && _currentTime >= _durationTime && _textAnimation != null && _textAnimation.MessageShownCompletely)
            {
                HideNotification();
            }
        }
        else
        {
            float progress = AppServices.RemoteObjectFactory?.Progress ?? 0f;
            if (progress > 0f && progress < 1f)
            {
                if (!_isLoadingModel)
                {
                    _isLoadingModel = true;
                    notificationBar.SetActive(true);
                }

                string progressString = string.Format(_loadingModelStringFormat, progress * 100.0f);

                if (_textAnimation != null)
                {
                    _textAnimation.TextDataToAnimate = new AnimateText.TextData[] { new AnimateText.TextData(progressString, AppNotificationType.Info) };
                }

                if (progressBar != null)
                {
                    progressBar.AnimateFill(progress);
                }
            }
            else if (_isLoadingModel)
            {
                // reset the download bar, turn it off
                _isLoadingModel = false;

                if (progressBar != null)
                {
                    progressBar.FillAmount = 0f;
                }

                HideNotification();
            }
        }
    }
    #endregion MonoBehavior Functions

    #region Public Functions
    public void SetScrollableNotification(float duration, string message, AppNotificationType type = AppNotificationType.Info)
    {
        UpdateTextAnimation(0.0f, ConvertToTextData(new string[] { message }, type), AnimationType.Scrolling);
        SetNotificationBarDisplayDuration(duration);
    }

    public void SetNotification(float duration, string message, AppNotificationType type = AppNotificationType.Info)
    {
        SetNotification(duration, 0.0f, new string[] { message }, type);
    }

    public void SetNotification(float duration, float animTime, string[] messages, AppNotificationType type = AppNotificationType.Info)
    {
        UpdateTextAnimation(animTime, ConvertToTextData(messages, type), AnimationType.Switching);
        SetNotificationBarDisplayDuration(duration);
    }

    public void HideNotification()
    {
        UpdateTextAnimation(0.0f, new AnimateText.TextData[] { new AnimateText.TextData(string.Empty, AppNotificationType.Info) }, AnimationType.Switching);

        _durationTime = 0f;
        _currentTime = 0f;
        _isDisplayingNotification = false;
        notificationBar.SetActive(false);
        notificationBarHidden?.Invoke();
    }
    #endregion Public Functions

    #region Private Functions
    private TextData[] ConvertToTextData(string[] messages, AppNotificationType type = AppNotificationType.Info)
    {
        var newTextData = new TextData[messages.Length];
        for (int i = 0; i < messages.Length; i++)
        {
            newTextData[i] = new  TextData(messages[i], type);
        }
        return newTextData;
    }

    private void UpdateTextAnimation(float animTime, TextData[] textData, AnimationType animationType)
    {
        if (_textAnimation != null)
        {
            _textAnimation.TextAnimationLength = animTime;
            _textAnimation.TextDataToAnimate = textData;
            _textAnimation.CurrentAnimationType = animationType;
        }
    }

    private void SetNotificationBarDisplayDuration(float duration)
    {
        _durationTime = duration;
        _currentTime = 0f;
        _isDisplayingNotification = true;

        // re-enabling will make the bar flash
        if (notificationBar.activeInHierarchy)
        {
            notificationBar.SetActive(false);
        }
        notificationBar.SetActive(true);
    }
    #endregion Private Functions
}
