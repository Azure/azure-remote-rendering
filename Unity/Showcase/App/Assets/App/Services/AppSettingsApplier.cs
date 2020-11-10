// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using UnityEngine;

/// <summary>
/// A helper class to isolate components from the application settings service.
/// </summary>
public class AppSettingsApplier : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    [Tooltip("The 'Remote Focus Highlight' object whose settings will be changed using the app settings.")]
    private RemoteFocusHighlight focusHighlight;

    /// <summary>
    /// The 'Remote Focus Highlight' object whose settings will be changed using the app settings.
    /// </summary>
    public RemoteFocusHighlight FocusHighlight
    {
        get => focusHighlight;
        set => focusHighlight = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Methods
    private void Start()
    {
        AppServices.AppSettingsService.SettingsChanged += AppSettingsService_SettingsChanged;
        UpdateFocusHighlight();
    }

    private void OnDestroy()
    {
        AppServices.AppSettingsService.SettingsChanged -= AppSettingsService_SettingsChanged;
    }
    #endregion MonoBehavior Methods

    #region Private Methods
    private void AppSettingsService_SettingsChanged(object sender, System.EventArgs e)
    {
        UpdateFocusHighlight();
    }

    private void UpdateFocusHighlight()
    {
        if (focusHighlight == null)
        {
            return;
        }

        focusHighlight.Settings = AppServices.AppSettingsService.FocusHighlightSettings;
    }
    #endregion Private Methods
}
