// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

#if WINDOWS_UWP
using Windows.Media.Capture;
#endif 

public class AppCaptureStatus : IDisposable
{
    #region Public Properties
    /// <summary>
    /// Get if app is cpaturing Mixed Reality Captures.
    /// </summary>
    public bool IsCapturing { get; private set; }
    #endregion

    #region Public Events
#pragma warning disable 0067
    /// <summary>
    /// Event raised when capturing changes
    /// </summary>
    public event Action<AppCaptureStatus, bool> IsCapturingChanged;
#pragma warning restore 0067
    #endregion Public Events

    #region Public Constructors
    public AppCaptureStatus()
    {
        AddAppCaptureHandlers();
    }
    #endregion Public Constructors

    #region Public Functions
    public void Dispose()
    {
        RemoveAppCaptureHandlers();
    }
    #endregion Public Functions

#if WINDOWS_UWP
    private void AddAppCaptureHandlers()
    {
        var appCapture = AppCapture.GetForCurrentView();
        if (appCapture != null)
        {
            IsCapturing = appCapture.IsCapturingVideo;
            appCapture.CapturingChanged += OnCapturingChanged;
        }
    }

    private void OnCapturingChanged(AppCapture appCapture, object args)
    {
        SetIsCapturing(appCapture.IsCapturingVideo);
    }

    private void SetIsCapturing(bool isCapturing)
    {
        if (isCapturing != IsCapturing)
        {
            IsCapturing = isCapturing;
            UnityEngine.WSA.Application.InvokeOnAppThread(() =>
            {
                IsCapturingChanged?.Invoke(this, isCapturing);
            }, false);
        }
    }

    private void RemoveAppCaptureHandlers()
    {
        var appCapture = AppCapture.GetForCurrentView();
        if (appCapture != null)
        {
            appCapture.CapturingChanged -= OnCapturingChanged;
        }
    }
#else
    private void AddAppCaptureHandlers()
    {
    }

    private void RemoveAppCaptureHandlers()
    {
    }
#endif
}
