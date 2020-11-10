// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine;

/// <summary>
/// A button class for setting the Remote Rendering Service's size setting
/// </summary>
[RequireComponent(typeof(Interactable))]
public class SessionSizeButton : ClickableButton
{
    #region Serilaized Fields
    [Header("Region Settings")]

    [SerializeField]
    [Tooltip("The size of the session server to set when the button is clicked.")]
    public RenderingSessionVmSize size;

    /// <summary>
    /// The size of the session server to set when the button is clicked.
    /// </summary>
    public RenderingSessionVmSize Size
    {
        get => size;
        set => size = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Methods
    private void OnValidate()
    {
        UpdateLabelText();
    }

    protected override void Start()
    {
        base.Start();
        UpdateLabelText();
    }

    private void Update()
    {
        Selected = AppServices.RemoteRendering.LoadedProfile.Size == size;
    }
    #endregion MonoBehavior Methods

    #region Protected Methods
    protected override void OnClicked()
    {
        AppServices.RemoteRendering.LoadedProfile.Size = size;
    }
    #endregion Protected Methods

    #region Private Methods
    private void UpdateLabelText()
    {
        LabelText = size.ToString();
    }
    #endregion Private Methods
}
