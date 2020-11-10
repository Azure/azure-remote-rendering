// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using UnityEngine;

/// <summary>
/// A helper to set the pointer state to the serialized inputs
/// </summary>
public class SetPointerState : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    [Tooltip("The pointer mode to apply when apply is called.")]
    private PointerMode mode;

    /// <summary>
    /// The pointer mode to apply when apply is called.
    /// </summary>
    public PointerMode Mode
    {
        get => mode;
        set => mode = value;
    }

    [SerializeField]
    [Tooltip("The pointer mode data to apply when apply is called.")]
    private UnityEngine.Object modeData;

    /// <summary>
    /// The pointer mode data to apply when apply is called.
    /// </summary>
    public UnityEngine.Object ModeData
    {
        get => modeData;
        set => modeData = value;
    }
    #endregion Serialized Fields

    #region Public Methods
    public void Apply()
    {
        AppServices.PointerStateService.SetModeWithData(mode, modeData);
    }
    #endregion Public Methods
}
