// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Collections.Generic;

/// <summary>
/// This handles pointer events so to move a list via a drag action. For example, dragging your finger across the list.
/// </summary>
public class DragValue : InputSystemGlobalHandlerListener, IMixedRealityPointerHandler, IMixedRealityFocusHandler, IMixedRealityTouchHandler
{
    private float dragValue;
    private Vector3 startPointerPosition;
    private IMixedRealityPointer activeDragPointer;
    private Handedness activeTouch = Handedness.None;
    private HashSet<uint> blockedPointers = new HashSet<uint>();
    private float dragDelta = 0;
    private bool draggedThisUpdate;

    #region Serialized Fields
    [Header("Drag Track")]

    [Tooltip("The axis the drag moves along.")]
    [SerializeField]
    private DragAxisValue dragAxis = DragAxisValue.YAxis;

    /// <summary>
    /// The axis the drag moves along.
    /// </summary>
    public DragAxisValue DragAxis
    {
        get => dragAxis; 
        set => dragAxis = value;
    }

    [SerializeField]
    [Tooltip("Where the drag track starts, as distance from center along slider axis, in local space units.")]
    private float dragStartDistance = -.5f;

    /// <summary>
    /// Where the drag track starts, as distance from center along slider axis, in local space units.
    /// </summary>
    public float DragStartDistance
    {
        get => dragStartDistance; 
        set => dragStartDistance = value; 
    }

    [SerializeField]
    [Tooltip("Where the drag track ends, as distance from center along slider axis, in local space units.")]
    private float dragEndDistance = .5f;

    /// <summary>
    /// Where the drag track ends, as distance from center along slider axis, in local space units.
    /// </summary>
    public float DragEndDistance
    {
        get => dragEndDistance; 
        set => dragEndDistance = value;
    }

    [SerializeField]
    [Tooltip("The min amount of movement to consider a drag.")]
    private float minDragMovement = 0.01f;

    /// <summary>
    /// The min amount of movement to consider a drag.
    /// </summary>
    public float MinDragMovement
    {
        get => minDragMovement; 
        set => minDragMovement = value; 
    }

    [Header("Touch Settings")]

    [SerializeField]
    [Tooltip("This is used to calculate the position of the touch point.")]
    private float touchPointerStartOffset = -0.05f;

    /// <summary>
    /// This is used to calculate the position of the touch point
    /// </summary>
    public float TouchPointerStartOffset
    {
        get => touchPointerStartOffset; 
        set => touchPointerStartOffset = value; 
    }

    [SerializeField]
    [Tooltip("This is used to calculate how far the touch pointer ray cast should go.")]
    private float touchPointerDistance = 0.04f;

    /// <summary>
    /// This is used to calculate how far the touch pointer ray cast should go.
    /// </summary>
    public float TouchPointerDistance
    {
        get => touchPointerDistance; 
        set => touchPointerDistance = value; 
    }

    [Header("Drag Collider")]

    [SerializeField]
    [Tooltip("The collider used for dragging. If defined, this object is treated as a input modal dialog and all dragging events will be considered by this object.")]
    private Collider dragCollider = null;

    /// <summary>
    /// The collider used for dragging. If defined, this object is treated as a input modal dialog and all dragging events will be considered by this object.
    /// </summary>
    public Collider DragCollider
    {
        get => dragCollider; 
        set => dragCollider = value; 
    }

    [Header("Misc Behavior")]

    [SerializeField]
    [Tooltip("Block all other pointer input while dragging.")]
    private bool blockPointersWhileDragging = false;

    /// <summary>
    /// Block all other pointer input while dragging.
    /// </summary>
    public bool BlockPointersWhileDragging
    {
        get => blockPointersWhileDragging; 
        set => blockPointersWhileDragging = value; 
    }

    [SerializeField]
    [Tooltip("The list of actions that support drag.")]
    private MixedRealityInputAction[] supportedDragActions = new MixedRealityInputAction[0];

    /// <summary>
    /// The list of actions that support drag.
    /// </summary>
    public MixedRealityInputAction[] SupportedDragActions
    {
        get => supportedDragActions; 
        set => supportedDragActions = value; 
    }

    [SerializeField]
    [Tooltip("The list of actions to never handle.")]
    private MixedRealityInputAction[] ignorePointerActions = new MixedRealityInputAction[0];

    /// <summary>
    /// The list of actions that support drag.
    /// </summary>
    public MixedRealityInputAction[] IgnorePointerActions
    {
        get => ignorePointerActions; 
        set => ignorePointerActions = value; 
    }
    #endregion Serialized Fields

    #region Public Properties
    /// <summary>
    /// Gets the start position of the slider, in world space, or zero if invalid.
    /// Sets the start position of the slider, in world space, projected to the slider's axis.
    /// </summary>
    public Vector3 DragStartPosition
    {
        get { return transform.TransformPoint(GetSliderAxis() * dragStartDistance); }
        set { dragStartDistance = Vector3.Dot(transform.InverseTransformPoint(value), GetSliderAxis()); }
    }

    /// <summary>
    /// Gets the end position of the slider, in world space, or zero if invalid.
    /// Sets the end position of the slider, in world space, projected to the slider's axis.
    /// </summary>
    public Vector3 DragEndPosition
    {
        get { return transform.TransformPoint(GetSliderAxis() * dragEndDistance); }
        set { dragEndDistance = Vector3.Dot(transform.InverseTransformPoint(value), GetSliderAxis()); }
    }

    /// <summary>
    /// Returns the direction vector from the slider start to end positions
    /// </summary>
    public Vector3 DragTrackDirection
    {
        get { return DragEndPosition - DragStartPosition; }
    }
    #endregion Public Properties

    #region Event Handlers
    [Header("Events")]
    public DragValueEvent OnValueUpdated;
    public DragValueEvent OnInteractionStarted;
    public DragValueEvent OnInteractionEnded;
    #endregion

    #region Constants
    /// <summary>
    /// Minimum distance between start and end of slider, in world space
    /// </summary>
    private const float MinSliderLength = 0.001f;
    #endregion

    #region MonoBehavior Functions
    protected override void OnDisable()
    {
        base.OnDisable();
        EndDragging();
    }

    private void LateUpdate()
    {
        draggedThisUpdate = IsDragging();
    }
    #endregion MonoBehavior Functions

    #region Private Functions
    private bool IsDragging()
    {
        return activeDragPointer != null;
    }

    private bool HasDragged()
    {
        return draggedThisUpdate || IsDragging();
    }

    private IMixedRealityPointer GetTouchPointer(Handedness hand)
    {
        IMixedRealityController handControl = HandJointUtils.FindHand(hand);

        if (handControl?.InputSource?.Pointers != null)
        {
            foreach (IMixedRealityPointer pointer in handControl.InputSource.Pointers)
            {
                if (pointer is PokePointer)
                {
                    return pointer;
                }
            }
        }

        return null;
    }

    public Vector3 GetSliderAxis()
    {
        switch (dragAxis)
        {
            case DragAxisValue.XAxis:
                return Vector3.right;
            case DragAxisValue.YAxis:
                return Vector3.up;
            case DragAxisValue.ZAxis:
                return Vector3.forward;
            default:
                throw new ArgumentOutOfRangeException("Invalid drag axis");
        }
    }

    private void StartDragging(IMixedRealityPointer pointer)
    {
        if (pointer != null && !IsDragging())
        {
            draggedThisUpdate = true;
            activeDragPointer = pointer;
            dragValue = 0;
            dragDelta = 0;
            startPointerPosition = activeDragPointer.Position;
            OnInteractionStarted?.Invoke(new DragValueEventData(this.dragValue, this.dragValue, this.activeDragPointer, this));
        }
    }

    private void UpdateDragging()
    {
        if (activeDragPointer == null)
        {
            return;
        }

        var delta = Vector3.Dot(DragTrackDirection.normalized,
            activeDragPointer.Position - startPointerPosition);

        this.dragDelta = delta;
        this.UpdateDraggingValue();
    }

    private void UpdateDraggingValue()
    {
        var oldDragValue = this.dragValue;
        var newDragValue = (Mathf.Clamp(this.dragDelta / DragTrackDirection.magnitude, -1, 1));
        if (oldDragValue != newDragValue)
        {
            this.dragValue = newDragValue;
            OnValueUpdated?.Invoke(new DragValueEventData(oldDragValue, newDragValue, activeDragPointer, this));
        }
    }

    private void EndDragging()
    {
        if (activeDragPointer != null)
        {
            activeDragPointer = null;
            OnInteractionEnded?.Invoke(new DragValueEventData(dragValue, dragValue, activeDragPointer, this));
        }
    }

    private void EndTouchAndDragging()
    {
        activeTouch = Handedness.None;
        EndDragging();
    }
    
    /// <summary>
    /// Test if hit item was valid for dragging
    /// </summary>
    private bool HitValidDragCollider(IMixedRealityPointer pointer)
    {
        return HitDragCollider(pointer) || HitListItem(pointer);
    }

    /// <summary>
    /// Test if hit item was the drag collider
    /// </summary>
    private bool HitDragCollider(IMixedRealityPointer pointer)
    {
        if (dragCollider == null)
        {
            // assume hit
            return true;
        }

        bool hasHit = false;
        int index = 0;
        if (index >= 0 && index < pointer.Rays.Length)
        {
            Ray ray = pointer.Rays[0];
            RaycastHit hit = default(RaycastHit);
            hasHit = dragCollider.Raycast(ray, out hit, 10);
        }
        return hasHit;
    }
    
    /// <summary>
    /// Test if hit item was a list item inside a ListItemRepeater.
    /// </summary>
    private bool HitListItem(IMixedRealityPointer pointer)
    {
        GameObject focusedObject = CoreServices.InputSystem?.FocusProvider?.GetFocusedObject(pointer);
        return ListItemRepeater.IsListRepeaterChild(focusedObject?.GetComponentInChildren<ListItem>());
    }

    private void TryBlocking(MixedRealityPointerEventData eventData, PointerEvent eventType)
    {
        if (eventData?.Pointer == null || PointerIgnorable(eventData))
        {
            return;
        }

        uint id = eventData.Pointer.PointerId;
        if (eventType == PointerEvent.Down && HasDragged() && blockPointersWhileDragging)
        {
            blockedPointers.Add(id);
        }

        if (blockedPointers.Contains(id))
        {
            eventData.Use();
        }

        if (eventType == PointerEvent.Up)
        {
            blockedPointers.Remove(id);
        }
    }

    private bool PointerSupported(MixedRealityPointerEventData eventData)
    {
        return !(eventData is IMixedRealityNearPointer) && 
            Array.IndexOf(supportedDragActions, eventData.MixedRealityInputAction) >= 0;
    }

    private bool PointerIgnorable(MixedRealityPointerEventData eventData)
    {
        return Array.IndexOf(ignorePointerActions, eventData.MixedRealityInputAction) >= 0;
    }
    #endregion Private Functions

    #region InputSystemGlobalHandlerListener Functions
    protected override void RegisterHandlers()
    {
        CoreServices.InputSystem?.RegisterHandler<IMixedRealityPointerHandler>(this);
        CoreServices.InputSystem?.RegisterHandler<IMixedRealityFocusHandler>(this);
        CoreServices.InputSystem?.RegisterHandler<IMixedRealityTouchHandler>(this);
    }

    protected override void UnregisterHandlers()
    {
        CoreServices.InputSystem?.UnregisterHandler<IMixedRealityPointerHandler>(this);
        CoreServices.InputSystem?.UnregisterHandler<IMixedRealityFocusHandler>(this);
        CoreServices.InputSystem?.UnregisterHandler<IMixedRealityTouchHandler>(this);
    }
    #endregion InputSystemGlobalHandlerListener Functions

    #region IMixedRealityFocusHandler Functions
    public void OnFocusEnter(FocusEventData eventData)
    {
    }

    public void OnFocusExit(FocusEventData eventData)
    {
    }
    #endregion IMixedRealityFocusHandler Functions

    #region IMixedRealityPointerHandler Functions
    public void OnPointerDown(MixedRealityPointerEventData eventData)
    {
        TryBlocking(eventData, PointerEvent.Down);

        if (!IsDragging() && HitValidDragCollider(eventData?.Pointer) && PointerSupported(eventData))
        {
            StartDragging(eventData.Pointer);
        }
    }

    public void OnPointerDragged(MixedRealityPointerEventData eventData)
    {
        TryBlocking(eventData, PointerEvent.Drag);

        if (eventData.Pointer == activeDragPointer)
        {
            UpdateDragging();

            // Mark the pointer data as used to prevent other behaviors from handling input events
            eventData.Use();
        }
    }

    public void OnPointerClicked(MixedRealityPointerEventData eventData)
    {
        TryBlocking(eventData, PointerEvent.Click);
    }

    public void OnPointerUp(MixedRealityPointerEventData eventData)
    {
        TryBlocking(eventData, PointerEvent.Up);

        if (eventData.Pointer == activeDragPointer)
        {
            EndDragging();
        }
    }
    #endregion IMixedRealityPointerHandler Functions

    #region IMixedRealityTouchHandler Functions
    public void OnTouchStarted(HandTrackingInputEventData eventData)
    {
        if (!IsDragging() &&
            (activeTouch == Handedness.None) &&
            (eventData.Handedness == Handedness.Left || eventData.Handedness == Handedness.Right))
        {
            activeTouch = eventData.Handedness;
        }
    }

    public void OnTouchUpdated(HandTrackingInputEventData eventData)
    {
        if (activeTouch == eventData.Handedness)
        {
            var touchPointer = GetTouchPointer(eventData.Handedness);
            bool canDrag = false;

            if (touchPointer != null && this.dragCollider != null && HitValidDragCollider(touchPointer))
            {
                Vector3 startPosition = touchPointer.Position + (this.dragCollider.transform.forward * this.touchPointerStartOffset);
                RaycastHit hit = default(RaycastHit);
                canDrag = this.dragCollider.Raycast(new Ray(
                    startPosition,
                    this.dragCollider.transform.forward),
                    out hit,
                    this.touchPointerDistance);
            }

            if (canDrag)
            {
                StartDragging(touchPointer);
            }
            else
            {
                EndDragging();
            }
        }

        if (activeTouch == eventData.Handedness)
        {
            UpdateDragging();
        }
    }

    public void OnTouchCompleted(HandTrackingInputEventData eventData)
    {
        if (activeTouch == eventData.Handedness)
        {
            EndTouchAndDragging();
        }
    }
    #endregion IMixedRealityTouchHandler Functions

    #region Public Enums
    [Serializable]
    public enum DragAxisValue
    {
        XAxis = 0,
        YAxis,
        ZAxis
    }
    #endregion Public Enums

    #region Private Enums
    private enum PointerEvent
    {
        Down,
        Drag,
        Click,
        Up
    }
    #endregion Private Enums
}
