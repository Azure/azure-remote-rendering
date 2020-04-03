// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using UnityEngine;

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

    #region Serialized Fields
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
    /// The object that displays download progress to the user."
    /// </summary>
    public ProgressBarFill ProgressBar
    {
        get => progressBar;
        set => progressBar = value;
    }
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
                SetNotification(15.0f, data.Message, AppNotificationType.Info);
                break;
            case AppNotificationType.Warning:
                SetNotification(15.0f, data.Message, AppNotificationType.Warning);
                break;
            case AppNotificationType.Error:
                SetNotification(15.0f, data.Message, AppNotificationType.Error);
                break;
        }
    }

    private void Update()
    {
        if (_currentTime < _durationTime)
        {
            _currentTime += Time.deltaTime;
            if (_currentTime >= _durationTime)
            {
                notificationBar.SetActive(false);
            }
        }
        else
        {
            float progress = AppServices.RemoteObjectFactory.Progress;
            if (progress > 0f && progress < 1f)
            {
                string progressString = string.Format(_loadingModelStringFormat, progress * 100.0f);

                if (!_isLoadingModel)
                {
                    _isLoadingModel = true;
                    SetNotification(-1, progressString);
                }

                SetNotificationText(progressString);
                SetDownloadFill(progress);
            }
            else
            {
                if (_isLoadingModel)
                {
                    // reset the download bar, turn it off
                    _isLoadingModel = false;
                    progressBar.FillAmount = 0f;
                    notificationBar.SetActive(false);
                }
            }
        }
    }
    #endregion MonoBehavior Functions

    #region Public Functions
    public void HideNotification()
    {
        if (_textAnimation != null)
        {
            _textAnimation.TextDataToAnimate = new AnimateText.TextData[] { new AnimateText.TextData(string.Empty, AppNotificationType.Info) };
        }

        _durationTime = 0f;
        _currentTime = 0f;
        notificationBar.SetActive(false);
    }

    public void SetNotification(float duration, string message, AppNotificationType type = AppNotificationType.Info)
    {
        if (_textAnimation != null)
        {
            _textAnimation.TextDataToAnimate = new AnimateText.TextData[] { new AnimateText.TextData(message, type) };
        }

        _durationTime = duration;
        _currentTime = 0f;

        //re-enabling will make the bar flash
        if(notificationBar.activeInHierarchy)
        {
            notificationBar.SetActive(false);
        }
        notificationBar.SetActive(true);
    }

    public void SetNotification(float duration, float animTime, string[] messages, AppNotificationType type = AppNotificationType.Info)
    {
        if (_textAnimation != null)
        {
            _textAnimation.TextDataToAnimate = new AnimateText.TextData[messages.Length];
            for (int i = 0; i < messages.Length; i++)
            {
                _textAnimation.TextDataToAnimate[i] = new AnimateText.TextData(messages[i], type);
            }
            _textAnimation.AnimationLength = animTime;
        }

        _durationTime = duration;
        _currentTime = 0f;

        //re-enabling will make the bar flash
        if (notificationBar.activeInHierarchy)
        {
            notificationBar.SetActive(false);
        }
        notificationBar.SetActive(true);
    }

    public void SetNotificationText(string newText)
    {
        if (_textAnimation != null)
        {
            _textAnimation.TextDataToAnimate = new AnimateText.TextData[] { new AnimateText.TextData(newText, AppNotificationType.Info) };
        }
    }

    public void SetDownloadFill(float fillAmount)
    {
        progressBar.AnimateFill(fillAmount);
    }
    #endregion Public Functions
}
