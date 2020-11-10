// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Input;
using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// PointerMode and UnityEvent pair that ties pointer modes to UnityEvents wired up in the inspector.
/// </summary>
[Serializable]
public struct PointerModeAndResponse
{
    /// <summary>
    /// Constructor.
    /// </summary>
    public PointerModeAndResponse(PointerMode mode, EnabledEvent enabled, DisabledEvent disabled, ClickEvent clicked)
    {
        this.mode = mode;
        this.enabled = enabled;
        this.disabled = disabled;
        this.clicked = clicked;
    }

    #region Serialized Fields

    [SerializeField]
    [Tooltip("The pointer mode to listen for.")]
    private PointerMode mode;

    /// <summary>
    /// The pointer mode to listen for.
    /// </summary>
    public PointerMode Mode => mode;

    [SerializeField]
    [Tooltip("The handler to be invoked when the pointer mode is active.")]
    private EnabledEvent enabled;

    /// <summary>
    /// The handler to be invoked when the pointer mode is active.
    /// </summary>
    public EnabledEvent Enabled => enabled;

    [SerializeField]
    [Tooltip("The handler to be invoked when the pointer mode is deactivated.")]
    private DisabledEvent disabled;

    /// <summary>
    /// The handler to be invoked when the pointer mode is deactivated.
    /// </summary>
    public DisabledEvent Disabled => disabled;

    [SerializeField]
    [Tooltip("The handler to be invoked when the pointer mode is active and a pointer click occurs.")]
    private ClickEvent clicked;

    /// <summary>
    /// The handler to be invoked when the pointer mode is active.
    /// </summary>
    public ClickEvent Clicked => clicked;
    #endregion Serialized Fields

    #region Public Class
    [Serializable]
    public class EnabledEvent : UnityEvent<PointerMode, object>
    { }

    [Serializable]
    public class DisabledEvent : UnityEvent<PointerMode, object>
    { }

    [Serializable]
    public class ClickEvent : UnityEvent<MixedRealityPointerEventData, object>
    { }
    #endregion Public Class
}
