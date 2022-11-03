// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering.Unity;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Physics;
using Microsoft.MixedReality.Toolkit.UI;
using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// This component handles pointer down events, and redirects manipulation's host transform to the pointer target, if 
/// that target is not a remote rendered object.
/// </summary>
/// <remarks>
/// This component is used for manipulating locally rendered model pieces.
/// </remarks>
[RequireComponent(typeof(ObjectManipulator))]
public class RedirectManipulationTarget : MonoBehaviour, IMixedRealityPointerHandler
{
    private ObjectManipulator _objectManipulator = null;
    private Transform _previousTarget = null;
    private bool _previousManipulatorEnabled = true;
    private bool _handlingPointerDown = false;

    #region MonoBehavior Functions
    /// <summary>
    /// Capture a reference to the manipulation handler
    /// </summary>
    private void Start()
    {
        _objectManipulator = GetComponent<ObjectManipulator>();
        Debug.Assert(_objectManipulator != null, "RedirectManipulationTarget requires a ObjectManipulator");
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
        if (!_handlingPointerDown &&
            _objectManipulator != null &&
            CoreServices.InputSystem.FocusProvider.TryGetFocusDetails(eventData.Pointer, out focusDetails) &&
            focusDetails.Object != null &&
            focusDetails.Object.GetComponent<RemoteEntitySyncObject>() == null)
        {
            Debug.Assert(_previousTarget == null, "Previous target should have been null");

            _handlingPointerDown = true;
            _previousTarget = _objectManipulator.HostTransform;
            _objectManipulator.HostTransform = focusDetails.Object.transform;

            _previousManipulatorEnabled = _objectManipulator.enabled;
            _objectManipulator.enabled = true;
            this.EnsureComponent<DelayPointerDown>().Fire(eventData, () =>
            {
                _handlingPointerDown = false;
            });
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
            _objectManipulator.enabled = _previousManipulatorEnabled;
            _objectManipulator.HostTransform = _previousTarget;
            _previousTarget = null;
        }
    }
    #endregion IMixedRealityPointerHandler Functions

    #region Private Classes
    /// <summary>
    // Handle forwarding events to proxy object late in the frame. During OnPointerDown Start()
    // for the ObjectManipulator component on the proxy object hasn't been called in all cases.
    // So we wait with event forwarding to prevent jumpy behavior of the moved pieces.
    /// </summary>
    private class DelayPointerDown : MonoBehaviour
    {
        private MixedRealityPointerEventData _pendingPointerDown = null;
        private bool _started = false;
        private Action _pendingFired = null;

        public void Start()
        {
            _started = true;
            if (_pendingPointerDown != null)
            {
                Fire(_pendingPointerDown, _pendingFired);
                _pendingPointerDown = null;
                _pendingFired = null;
            }
        }

        public void Fire(MixedRealityPointerEventData pointerDownEventData, Action fired = null)
        {
            if (_started)
            {
                RouteEventToCurrentObject(pointerDownEventData, OnPointerDownEventHandler);
                _pendingPointerDown = null;
                fired?.Invoke();
            }
            else
            {
                _pendingPointerDown = pointerDownEventData;
                _pendingFired = fired;
            }
        }

        private static readonly ExecuteEvents.EventFunction<IMixedRealityPointerHandler> OnPointerDownEventHandler =
            delegate (IMixedRealityPointerHandler handler, BaseEventData eventData)
            {
                if (eventData != null)
                {
                    var casted = ExecuteEvents.ValidateEventData<MixedRealityPointerEventData>(eventData);
                    handler.OnPointerDown(casted);
                }
            };

        private void RouteEventToCurrentObject<T>(BaseEventData eventData, ExecuteEvents.EventFunction<T> eventFunction) where T : IEventSystemHandler
        {
            ExecuteEvents.Execute(gameObject, eventData, eventFunction);
        }
    }
    #endregion Private Classes
}
