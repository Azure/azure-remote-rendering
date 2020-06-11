// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// A class for sharing the state of an anchor and a child transform.
/// </summary>
public class SharableObjectTransform : MonoBehaviour
{
    private SharingServiceTransform _transform = SharingServiceTransform.Create();
    private bool _pendingSendTransform = false;
    private Transform _shareTransform = null;
    private Vector3 _receivedSentPosition;
    private Quaternion _receivedSentRotation;
    private Vector3 _receivedSentScale;
    private Coroutine _sendingLiveMovements;
    private MovableAnchor _anchor;
    private string _anchorId;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("If true, move events are sent as this object is being manipulated so that the other players can see the manipulation live.")]
    private bool enableLiveMovement = true;

    /// <summary>
    /// If true, move events are sent as this object is being manipulated so that the other players can see the manipulation live.
    /// </summary>
    public bool EnableLiveMovement
    {
        get => enableLiveMovement;
        set => enableLiveMovement = value;
    }

    [SerializeField]
    [Tooltip("The sharing target used to send properties updates too. If null at Start(), the nearest parent target will be used.")]
    private SharingTarget target;

    /// <summary>
    /// The sharing target used to send properties updates too. If null at Start(), the nearest parent target will be used.
    /// </summary>
    public SharingTarget Target
    {
        get => target;
        set => target = value;
    }

    [SerializeField]
    [Tooltip("This is the movable object whose transform is shared with the other players. If this is a MovableAnchor, the anchor id is also shared.")]
    private MovableObject movable;

    /// <summary>
    /// This is the movable object whose transform is shared with the other players. If this is a MovableAnchor, the anchor id is also shared.
    /// </summary>
    public MovableObject Movable
    {
        get => movable;
        set => movable = value;
    }
    #endregion Serialized Fields

    #region Private Properties
    /// <summary>
    /// Get the last shared fallback position
    /// </summary>
    public Vector3 FallbackPosition
    {
        get
        {
            Pose fallback;
            if (target != null && target.TryGetProperty(SharableStrings.AnchorFallbackPose, out fallback))
            {
                return fallback.position;
            }
            else
            {
                return Vector3.negativeInfinity;
            }
        }
    }

    /// <summary>
    /// Get the last shared fallback rotation
    /// </summary>
    public Quaternion FallbackRotation
    {
        get
        {
            Pose fallback;
            if (target != null && target.TryGetProperty(SharableStrings.AnchorFallbackPose, out fallback))
            {
                return fallback.rotation;
            }
            else
            {
                return QuaternionStatics.PositiveInfinity;
            }
        }
    }
    #endregion Private Properties

    #region MonoBehaviour Functions
    private void Awake()
    {
        if (target == null)
        {
            target = GetComponentInChildren<SharingTarget>();
        }

        if (movable == null)
        {
            movable = GetComponentInChildren<MovableObject>();
        }

        // Check if there is an anchor
        _anchor = movable as MovableAnchor;

        // Initialize the anchor id before the anchor object starts to create a native anchor.
        InitializeAnchor();
    }

    private void Start()
    {
        if (movable != null)
        {
            movable.Moving.AddListener(HandleObjectMoving);
            movable.Moved.AddListener(HandleObjectMoved);
            if (movable.IsMoving)
            {
                HandleObjectMoving();
            }
        }

        if (_anchor != null)
        {
            _anchor.AnchorIdChanged.AddListener(SendAnchorId);
            SendAnchorId(_anchor);
        }

        if (movable == null || movable.Movable == null)
        {
            _shareTransform = transform;
        }
        else
        {
            _shareTransform = movable.Movable;
        }

        if (target != null)
        {
            target.PropertyChanged += HandlePropertyChanged;
            target.TransformMessageReceived += HandleTransformMessage;
            target.ConnectionChanged += TargetConnectionChanged;
        }

        // If SendTransform() was called before start, the request is blocked; so call this function now.
        if (_pendingSendTransform)
        {
            SendTransform(true);
        }
    }

    private void OnDestroy()
    {
        _sendingLiveMovements = null;

        if (target != null)
        {
            target.PropertyChanged -= HandlePropertyChanged;
            target.TransformMessageReceived -= HandleTransformMessage;
            target.ConnectionChanged -= TargetConnectionChanged;
            target = null;
        }

        if (movable != null)
        {
            movable.Moving.RemoveListener(HandleObjectMoving);
            movable.Moved.RemoveListener(HandleObjectMoved);
        }

        if (_anchor != null)
        {
            _anchor.AnchorIdChanged.RemoveListener(SendAnchorId);
        }
    }
    #endregion MonoBehaviour Functions

    #region Public Functions
    /// <summary>
    /// Force the object's transform to be sent to the other clients.
    /// </summary>
    public void SendTransform()
    {
        SendTransform(true);
    }

    /// <summary>
    /// Get the server transform for this target.
    /// </summary>
    public SharingServiceTransform GetTransform()
    {
        SharingServiceTransform result;
        if (target == null || !target.TryGetProperty(SharableStrings.ObjectTransform, out result))
        {
            result = SharingServiceTransform.Create();
        }
        return result;
    }

    /// <summary>
    /// Force the transform to use whatever is currently stored on the server.
    /// </summary>
    public void SetTransform(SharingServiceTransform serverTransform)
    {
        ReceiveTransform(serverTransform);
        SendTransform();
    }
    #endregion Public Functions

    #region Private Functions
    /// <summary>
    /// Initialize the object's anchor to the cloud id currently stored on the server.
    /// </summary>
    private void InitializeAnchor()
    {
        if (target == null)
        {
            return;
        }

        if (!target.IsConnected)
        {
            // If not connected to the sharing service, force an empty anchor to avoid created an unneeded azure anchor.
            _anchorId = null;
            _anchor?.ApplyEmptyAnchor();
        }
        else
        {
            string anchorId;
            target.TryGetProperty(SharableStrings.AnchorId, out anchorId);

            // If there is no shared anchor id (anchorId == null), create a native anchor and shared it. Otherwise,
            // use the anchor id to find and consume the associated azure anchor.
            if (string.IsNullOrEmpty(anchorId))
            {
                CreateAndSendAnchor();
            }
            else
            { 
                ReceiveAnchorId(anchorId);
            }
        }
    }

    /// <summary>
    /// Handle a connection change, and reinitialize anchor if connected
    /// </summary>
    private void TargetConnectionChanged(bool isConnected)
    {
        if (isConnected)
        {
            InitializeAnchor();
        }
    }

    /// <summary>
    /// Handle property changes received from the server.
    /// </summary>
    private void HandlePropertyChanged(string property, object input)
    {
        switch (input)
        {
            case SharingServiceTransform value when property == SharableStrings.ObjectTransform:
                ReceiveTransform(value);
                break;

            case string value when property == SharableStrings.AnchorId:
                ReceiveAnchorId(value);
                break;
        }
    }

    /// <summary>
    /// Handle transform messages received from the server. These are special messages that represent the object's
    /// transform.  
    /// </summary>
    private void HandleTransformMessage(SharingServiceTransform transform)
    {
        ReceiveTransform(transform);
    }

    /// <summary>
    /// Invoked when the local user starts moving this object. When called, start notifying other players of object movements. 
    /// </summary>
    private void HandleObjectMoving()
    {
        if (_sendingLiveMovements == null && enableLiveMovement)
        {
            _sendingLiveMovements = StartCoroutine(SendObjectMovements());
        }
    }

    /// <summary>
    /// Continuously send movement events to the other users.
    /// </summary>
    private IEnumerator SendObjectMovements()
    {
        do
        {
            SendTransform(setProperty: false);
            yield return 0;
        } while (_sendingLiveMovements != null);

        yield break;
    }

    /// <summary>
    /// Invoked when the local user stops moving this object. When called, stop notifying other players of object movements. 
    /// </summary>
    private void HandleObjectMoved()
    {
        _sendingLiveMovements = null;
        SendTransform(setProperty: true);
    }
    
    /// <summary>
    /// Send the object anchor id to the other users. This also sends the anchor's transform relative to the object's 
    /// root anchor.
    /// </summary>
    private void SendAnchorId(MovableAnchor anchor)
    {
        SendAnchorId(anchor.AnchorId, anchor.FallbackPosition, anchor.FallbackRotation);
    }

    /// <summary>
    /// Send the object anchor id to the other users. This also sends the anchor's transform relative to the object's 
    /// root anchor.
    /// </summary>
    private void SendAnchorId(MovableAnchor.AnchorIdChangedEventArgs args)
    {
        SendAnchorId(args.anchorId, args.fallbackPosition, args.fallbackRotation);
    }

    /// <summary>
    /// Send the object anchor id to the other users. This also sends the anchor's transform relative to the object's 
    /// root anchor.
    /// </summary>
    private void SendAnchorId(string anchorId, Vector3 fallbackPosition, Quaternion fallbackRotation)
    {
        // Only send if anchor id has changed from the last received anchor, or the
        // anchor id is empty
        if (target != null && (anchorId != _anchorId || _anchorId == MovableAnchor.EmptyAnchorId))
        {
            target.SetProperties(
                SharableStrings.AnchorId,
                anchorId,
                SharableStrings.AnchorFallbackPose,
                new Pose(fallbackPosition, fallbackRotation));
            _anchorId = anchorId;
        }
    }

    /// <summary>
    /// Create a new cloud anchor, then share it.
    /// </summary>
    private void CreateAndSendAnchor()
    {
        // Applying a native anchor will automatically save it to the cloud. This class will then handle the _anchor's anchor id changed event, and send the new id.
        _anchorId = null;
        _anchor?.ApplyNativeAnchor(FallbackPosition, FallbackRotation);
    }

    /// <summary>
    /// Receive the object anchor id from the server. This also sets the anchor's fallback transform relative to the object's 
    /// root anchor. The fallback transform is used when the platform doesn't support anchors or the anchor is not located.
    /// </summary>
    private void ReceiveAnchorId(string anchorId)
    {
        _anchorId = anchorId;
        if (_anchor != null && !string.IsNullOrEmpty(anchorId))
        {
            _anchor.ApplyCloudAnchor(anchorId, FallbackPosition, FallbackRotation);
        }
    }

    /// <summary>
    /// Notify the other users of the object's transform changes. This will send a one time event. If 'setProperty' is
    /// true, a property change will also be sent to the server.
    /// </summary>
    private void SendTransform(bool setProperty)
    {
        if (target != null && _shareTransform != null)
        {
            _pendingSendTransform = false;
            if (_transform.Set(_shareTransform))
            {
                target.SendTransformMessage(_transform);
            }

            if (setProperty)
            {
                target.SetProperty(SharableStrings.ObjectTransform, _transform);
            }
        }
        else
        {
            _pendingSendTransform = true;
        }
    }

    /// <summary>
    /// Receive transform changes from the server. This transform will be applied to the object's movable transform.
    /// </summary>
    private void ReceiveTransform(SharingServiceTransform source)
    {
        if (_transform.Position == source.Position &&
            _transform.Rotation == source.Rotation &&
            _transform.Scale == source.Scale)
        {
            return;
        }

        _transform = source;
        _shareTransform.localPosition = _transform.Position;
        _shareTransform.localRotation = _transform.Rotation;
        _shareTransform.localScale = _transform.Scale;
    }
    #endregion Private Functions
}
