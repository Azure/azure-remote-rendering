// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Calls events based on Palm states.
/// </summary>
public class PalmAngleEvents : InputSystemGlobalHandlerListener, IMixedRealityPointerHandler
{
    private float _minPalmUpAngle = 150f;
    private float _maxPalmUpAngle = 210f;
    private bool _wasPalmUp = false;
    private Handedness _pointerDown = Handedness.None;

    #region Serialized Fields
    [Header("General Settings")]

    [SerializeField]
    [Tooltip("The hand to start tracking.")]
    private Handedness hand = Handedness.Both;

    /// <summary>
    /// The hand to start tracking.
    /// </summary>
    public Handedness Hand
    {
        get => hand;
        set => hand = value;
    }

    [Header("Events")]

    [SerializeField]
    [Tooltip("The event raised when the palm is put into the up position.")]
    private UnityEvent onPalmUp = new UnityEvent();

    /// <summary>
    /// The event raised when the palm is put into the up position.
    /// </summary>
    public UnityEvent OnPalmUp => onPalmUp;

    [SerializeField]
    [Tooltip("The event raised when the palm is put into the down position.")]
    private UnityEvent onPalmDown = new UnityEvent();

    /// <summary>
    /// The event raised when the palm is put into the down position.
    /// </summary>
    public UnityEvent OnPalmDown => onPalmDown;
    #endregion Serialized Fields

    #region MonoBehavior Functions
    /// <summary>
    /// Fire palm events when needed.
    /// </summary>
    private void Update()
    {
        if (_pointerDown != Handedness.None &&
            !IsActiveHand(_pointerDown))
        {
            _pointerDown = Handedness.None;
        }

        if (_pointerDown != Handedness.None)
        {
            return;
        }

        Handedness upHand;
        if (TryGetPalm(out upHand))
        {
            if (!_wasPalmUp && onPalmUp != null)
            {
                onPalmUp.Invoke();
            }
            _wasPalmUp = true;
        }
        else
        {
            if (_wasPalmUp && onPalmDown != null)
            {
                onPalmDown.Invoke();
            }
            _wasPalmUp = false;
        }
    }
    #endregion MonoBehavior Functions

    #region Public Functions
    public bool TryGetPalm(out Handedness upHand)
    { 
        upHand = Handedness.None;
        if (hand == Handedness.Both || hand == Handedness.Any)
        {
            if (LeftOrRightPalmUp(Handedness.Left))
            {
                upHand = Handedness.Left;
            }
            else if (LeftOrRightPalmUp(Handedness.Right))
            {
                upHand = Handedness.Right;
            }
        }
        else if (LeftOrRightPalmUp(hand))
        {
            upHand = hand;
        }
        return upHand != Handedness.None;
    }

    public bool TryGetPalmRotation(Handedness hand, out Vector3 eulerAngles)
    {
        bool leftOrRight = hand == Handedness.Left || hand == Handedness.Right;
        MixedRealityPose palmPose;

        if (leftOrRight &&
            HandJointUtils.TryGetJointPose(TrackedHandJoint.Palm, hand, out palmPose))
        {
            eulerAngles = palmPose.Rotation.eulerAngles;
            return true;
        }
        else
        {
            eulerAngles = Vector3.zero;
            return false;
        }
    }
    #endregion Public Functions

    #region IMixedRealityPointerHandler Functions
    public void OnPointerDown(MixedRealityPointerEventData eventData)
    {
        if ((eventData.Handedness & Handedness.Both) == 0 ||
            !(eventData.Pointer?.Controller is IMixedRealityHand))
        {
            return;
        }

        _pointerDown |= eventData.Handedness;
    }

    public void OnPointerDragged(MixedRealityPointerEventData eventData)
    {
    }

    public void OnPointerUp(MixedRealityPointerEventData eventData)
    {
        if ((eventData.Handedness & Handedness.Both) == 0 ||
            !(eventData.Pointer?.Controller is IMixedRealityHand))
        {
            return;
        }

        _pointerDown &= (~eventData.Handedness);
    }

    public void OnPointerClicked(MixedRealityPointerEventData eventData)
    {
    }
    #endregion IMixedRealityPointerHandler Functions

    #region InputSystemGlobalHandlerListener Functions
    protected override void RegisterHandlers()
    {
        if (CoreServices.InputSystem != null)
        {
            CoreServices.InputSystem.RegisterHandler<IMixedRealityPointerHandler>(this);
        }
    }

    protected override void UnregisterHandlers()
    {
        if (CoreServices.InputSystem != null)
        {
            CoreServices.InputSystem.UnregisterHandler<IMixedRealityPointerHandler>(this);
        }
    }
    #endregion InputSystemGlobalHandlerListener Functions

    #region Private Functions
    private bool LeftOrRightPalmUp(Handedness hand)
    {
        if (hand != Handedness.Left && hand != Handedness.Right)
        {
            return false;
        }

        var handObject = HandJointUtils.FindHand(hand);
        MixedRealityPose handJoint;
        bool isPalmUp = false;

        if (handObject != null &&
            HandJointUtils.TryGetJointPose(TrackedHandJoint.Palm, hand, out handJoint) &&
            handJoint.Rotation.eulerAngles.z >= _minPalmUpAngle &&
            handJoint.Rotation.eulerAngles.z <= _maxPalmUpAngle)
        {
            isPalmUp = true;
        }

        return isPalmUp;
    }

    private bool IsActiveHand(Handedness hand)
    {
        return (HandJointUtils.FindHand(hand) != null);
    }
    #endregion Private Functions
}
