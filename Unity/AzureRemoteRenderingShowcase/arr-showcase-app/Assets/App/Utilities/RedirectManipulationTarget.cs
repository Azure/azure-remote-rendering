// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Physics;
using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine;

/// <summary>
/// This component handles pointer down events, and redirects manipulation's host transform to the pointer target.
/// </summary>
[RequireComponent(typeof(ManipulationHandler))]
public class RedirectManipulationTarget : MonoBehaviour, IMixedRealityPointerHandler
{
    private ManipulationHandler _manipulationHandler = null;
    private Transform _previousTarget = null;

    #region MonoBehavior Functions
    /// <summary>
    /// Capture a reference to the manipulation handler
    /// </summary>
    private void Start()
    {
        _manipulationHandler = GetComponent<ManipulationHandler>();
        Debug.Assert(_manipulationHandler != null, "RedirectManipulationTarget requires a ManipulationHandler");
    }
    #endregion MonoBehavior Functions

    #region IMixedRealityPointerHandler Functions
    /// <summary>
    /// Handle point down events, and redirect event to the manipulate handler.
    /// </summary>
    public void OnPointerClicked(MixedRealityPointerEventData eventData)
    {
    }

    /// <summary>
    /// Handle point down events, and redirect event to the manipulate handler after changing the handler's host transform.
    /// </summary>
    public void OnPointerDown(MixedRealityPointerEventData eventData)
    {
        FocusDetails focusDetails;
        if (_manipulationHandler != null &&
            CoreServices.InputSystem.FocusProvider.TryGetFocusDetails(eventData.Pointer, out focusDetails) &&
            focusDetails.Object != null)
        {
            Debug.Assert(_previousTarget == null, "Previouse target should have been null");
            _previousTarget = _manipulationHandler.HostTransform;
            _manipulationHandler.HostTransform = focusDetails.Object.transform;
        }
    }

    /// <summary>
    /// Handle point down events, and redirect event to the manipulate handler.
    /// </summary>
    public void OnPointerDragged(MixedRealityPointerEventData eventData)
    {
    }

    /// <summary>
    /// Handle point down events, and redirect event to the manipulate handler after resetting the handler's host transform.
    /// </summary>
    public void OnPointerUp(MixedRealityPointerEventData eventData)
    {
        if (_previousTarget != null)
        {
            _manipulationHandler.HostTransform = _previousTarget;
            _previousTarget = null;
        }
    }
    #endregion IMixedRealityPointerHandler Functions
}
