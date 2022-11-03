// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using UnityEngine;

/// <summary>
/// Registers an app dialog object that will host dialogs inside the app menu.
/// </summary>
public class MenuDialog : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    [Tooltip("The app dialog that will host dialogs within the app menu.")]
    private AppDialog source;

    /// <summary>
    /// The app dialog that will host dialogs within the app menu.
    /// </summary>
    public AppDialog Source
    {
        get => source;
        set => source = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior 
    /// <summary>
    /// Register the app dialog source with the notification service.
    /// </summary>
    private void OnEnable()
    {
        if (source != null)
        {
            AppServices.AppNotificationService.RegisterMenuDialog(source);
        }
    }
    #endregion MonoBehavior
}
