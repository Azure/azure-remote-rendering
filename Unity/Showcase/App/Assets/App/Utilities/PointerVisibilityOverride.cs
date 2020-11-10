// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using UnityEngine;

/// <summary>
/// The behavior helps to always hide or always show the given point type. When destroyed, this will release its override request.
/// </summary>
public class PointerVisibilityOverride : MonoBehaviour
{
    private bool _alwaysShow;
    private bool _alwaysHide;
    private IPointerStateVisibilityOverride _visibilityOverride;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The pointer type to always show or always hide.")]
    private PointerType type = PointerType.Gaze;

    /// <summary>
    /// The pointer type to always show or always hide.
    /// </summary>
    public PointerType Type
    {
        get => type;

        set
        {
            if (type != value)
            {
                type = value;
                if (_alwaysHide)
                {
                    AlwaysHide();
                }
                else if (_alwaysShow)
                {
                    AlwaysShow();
                }
            }
        }
    }
    #endregion Serialized Fields

    #region MonoBehavior Functions
    /// <summary>
    /// Release the current visibility override
    /// </summary>
    private void OnDestroy()
    {
        ResetState();
    }
    #endregion MonoBehavior Functions

    #region Public Functions
    /// <summary>
    /// Always show the current pointer type.
    /// </summary>
    public void AlwaysShow()
    {
        ResetState();
        _visibilityOverride = AppServices.PointerStateService.ShowPointer(type);
    }

    /// <summary>
    /// Always hide the current pointer type.
    /// </summary>
    public void AlwaysHide()
    {
        ResetState();
        _visibilityOverride = AppServices.PointerStateService.HidePointer(type);
    }

    /// <summary>
    /// Restore the pointer's visibility its default state.
    /// </summary>
    public void ResetState()
    {
        _visibilityOverride?.Dispose();
        _visibilityOverride = null;
        _alwaysHide = false;
        _alwaysShow = false;
    }
    #endregion Public Functions
}
