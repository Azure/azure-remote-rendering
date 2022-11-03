// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Calls events based on Palm states.
/// </summary>
public class PalmAngleEvents : InputSystemGlobalHandlerListener, IMixedRealityPointerHandler
{
    private float _minPalmUpAngle = 150f;
    private float _maxPalmUpAngle = 210f;
    private Handedness _upHand = Handedness.None;
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
    private HandEvent onPalmUp = new HandEvent();

    /// <summary>
    /// The event raised when the palm is put into the up position.
    /// </summary>
    public HandEvent OnPalmUp => onPalmUp;

    [SerializeField]
    [Tooltip("The event raised when the palm is put into the down position.")]
    private HandEvent onPalmDown = new HandEvent();

    /// <summary>
    /// The event raised when the palm is put into the down position.
    /// </summary>
    public HandEvent OnPalmDown => onPalmDown;
    #endregion Serialized Fields

    #region Public Properties
    /// <summary>
    /// Get the hand that are up.
    /// </summary>
    public Handedness UpHand
    {
        get => _upHand;

        private set
        {
            if (_upHand != value)
            {
                var oldHand = _upHand;
                _upHand = value;

                if (oldHand != Handedness.None)
                {
                    onPalmDown?.Invoke(oldHand);
                }

                if (_upHand != Handedness.None)
                {
                    onPalmUp?.Invoke(_upHand);
                }
            }
        }
    }
    #endregion Public Properties

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

        if (_pointerDown == Handedness.None)
        {
            Handedness upHands;
            TryGetPalms(out upHands);

            // Perfer existing up hand, if still up
            if ((UpHand == Handedness.None) ||
                (upHands & UpHand) != UpHand)
            {
                if ((upHands & Handedness.Left) == Handedness.Left)
                {
                    UpHand = Handedness.Left;
                }
                else if ((upHands & Handedness.Right) == Handedness.Right)
                {
                    UpHand = Handedness.Right;
                }
                else
                {
                    UpHand = Handedness.None;
                }
            }
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        UpHand = Handedness.None;
    }
    #endregion MonoBehavior Functions

    #region Public Functions
    public bool TryGetPalms(out Handedness upHand)
    { 
        upHand = Handedness.None;
        if (hand == Handedness.Both || hand == Handedness.Any)
        {
            if (LeftOrRightPalmUp(Handedness.Left))
            {
                upHand |= Handedness.Left;
            }

            if (LeftOrRightPalmUp(Handedness.Right))
            {
                upHand |= Handedness.Right;
            }
        }
        else if (LeftOrRightPalmUp(hand))
        {
            upHand = hand;
        }
        return upHand != Handedness.None;
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

        MixedRealityPose handJoint;
        bool isPalmUp = false;

        if (HandJointUtils.TryGetJointPose(TrackedHandJoint.Palm, hand, out handJoint) &&
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

[Serializable]
public class HandEvent : UnityEvent<Handedness>
{
}
