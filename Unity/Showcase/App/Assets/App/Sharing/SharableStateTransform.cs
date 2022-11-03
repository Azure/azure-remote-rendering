// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// A class for sharing the state of an anchor and a child transform.
/// </summary>
public class SharableStateTransform : MonoBehaviour
{
    private SharingServiceTransform _transform = SharingServiceTransform.Create();
    private bool _pendingSendTransform = false;
    private Transform _sharedTransformSource = null;
    private Coroutine _sendingLiveMovements;

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
    [Tooltip("If true, local poses are shared instead of origin poses.")]
    private bool useLocal = false;

    /// <summary>
    /// If true, local poses are shared instead of origin poses.
    /// </summary>
    public bool UseLocal
    {
        get => useLocal;
        set => useLocal = value;
    }

    [SerializeField]
    [FormerlySerializedAs("target")]
    [Tooltip("The sharing object used to send properties updates too. If null at Start(), the nearest parent target will be used.")]
    private SharingObjectBase sharingObject;

    /// <summary>
    /// The sharing object used to send properties updates too. If null at Start(), the nearest parent target will be used.
    /// </summary>
    public SharingObjectBase SharingObject
    {
        get => sharingObject;
        set => sharingObject = value;
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

    [SerializeField]
    [Tooltip("The number of time per second to sending pose events when the object is moving.")]
    private float updateFrequency = 30.0f;

    /// <summary>
    /// The number of time per second to sending pose events when the object is moving.
    /// </summary>
    public float UpdateFrequency
    {
        get => updateFrequency;
        set => updateFrequency = value;
    }
    #endregion Serialized Fields

    #region MonoBehaviour Functions
    private void Awake()
    {
        if (sharingObject == null)
        {
            sharingObject = GetComponentInChildren<SharingObjectBase>();
        }

        if (movable == null)
        {
            movable = GetComponentInChildren<MovableObject>();
        }

        if (movable != null)
        {
            movable.Moving.AddListener(HandleObjectMoving);
            movable.Moved.AddListener(HandleObjectMoved);
            if (movable.IsMoving)
            {
                HandleObjectMoving();
            }
        }
    }

    private void Start()
    {
        if (movable == null)
        {
            _sharedTransformSource = transform;
        }
        else if (movable.HasMovableChild)
        {
            _sharedTransformSource = movable.Movable;
        }
        else
        {
            _sharedTransformSource = movable.transform;
        }

        // If SendTransform() was called before start, the request is blocked.
        // Call this function now.
        if (_pendingSendTransform)
        {
            SendTransform(true);
        }

        if (sharingObject != null)
        {
            sharingObject.PropertyChanged += HandlePropertyChanged;
            sharingObject.TransformMessageReceived += HandleTransformMessage;
        }
    }

    private void OnDestroy()
    {
        StopSendingObjectMovements();

        if (sharingObject != null)
        {
            sharingObject.PropertyChanged -= HandlePropertyChanged;
            sharingObject.TransformMessageReceived -= HandleTransformMessage;
            sharingObject = null;
        }

        if (movable != null)
        {
            movable.Moving.RemoveListener(HandleObjectMoving);
            movable.Moved.RemoveListener(HandleObjectMoved);
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
    /// Save the server transform for this target, so it can be re-applied later
    /// </summary>
    public ITransformHistory SaveTransform()
    {
        return new TransformHistory(GetTransform(), (SharingServiceTransform load) =>
        {
            ReceiveTransform(load);
            SendTransform();
        });
    }
    #endregion Public Functions

    #region Private Functions
    /// <summary>
    /// Handle property changes received from the server.
    /// </summary>
    private void HandlePropertyChanged(ISharingServiceObject sender, string property, object input)
    {
        switch (input)
        {
            case SharingServiceTransform transform when property == SharableStrings.ObjectTransform:
                ReceiveTransform(transform);
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
        if (_sendingLiveMovements == null && enableLiveMovement && updateFrequency > 0)
        {
            _sendingLiveMovements = StartCoroutine(SendObjectMovements());
        }
    }

    /// <summary>
    /// Continuously send movement events to the other users.
    /// </summary>
    private IEnumerator SendObjectMovements()
    {
        float delay = 1.0f / updateFrequency;
        do
        {
            SendTransform(setProperty: false);
            yield return new WaitForSeconds(delay);
        } while (_sendingLiveMovements != null);

        yield break;
    }

    /// <summary>
    /// Stop sending object pose changes
    /// </summary>
    private void StopSendingObjectMovements()
    {
        if (_sendingLiveMovements != null)
        {
            StopCoroutine(_sendingLiveMovements);
            _sendingLiveMovements = null;
        }
    }

    /// <summary>
    /// Invoked when the local user stops moving this object. When called, stop notifying other players of object movements. 
    /// </summary>
    private void HandleObjectMoved()
    {
        StopSendingObjectMovements();
        SendTransform(setProperty: true);
    }

    /// <summary>
    /// Get the latest sharing service transform
    /// </summary>
    private SharingServiceTransform GetTransform()
    {
        SharingServiceTransform result;
        if (sharingObject == null || !sharingObject.TryGetProperty(SharableStrings.ObjectTransform, out result))
        {
            result = SharingServiceTransform.Create();
        }
        return result;
    }

    /// <summary>
    /// Notify the other users of the object's transform changes. This will send a one time event. If 'setProperty' is
    /// true, a property change will also be sent to the server.
    /// </summary>
    private void SendTransform(bool setProperty)
    {
        if (sharingObject != null && _sharedTransformSource != null)
        {
            _pendingSendTransform = false;            
            if (UpdateSharingServiceTransform())
            {
                sharingObject.SendTransformMessage(_transform);
            }

            if (setProperty)
            {
                sharingObject.SetProperty(SharableStrings.ObjectTransform, _transform);
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
        if (movable != null && movable.IsMoving)
        {
            return;
        }

        if (_transform.Position == source.Position &&
            _transform.Rotation == source.Rotation &&
            _transform.Scale == source.Scale)
        {
            return;
        }

        _transform = source;
        UpdateSharedTransformSource();
    }

    /// <summary>
    /// Update the sharing transform with _sharedTransformSource's world pose, and local scale
    /// </summary>
    private bool UpdateSharingServiceTransform()
    {
        if (_sharedTransformSource == null)
        {
            return false;
        }

        bool changed = _transform.SetScale(ref _sharedTransformSource);

        // The position and rotation can't be shared without a stage/collaboration origin
        if (!IsOrigin())
        {
            if (HasMovable() && !useLocal)
            {
                var originPose = WorldToOrigin(_sharedTransformSource.position, _sharedTransformSource.rotation);
                changed |= _transform.Set(ref originPose.originPosition, ref originPose.originRotation);
            }
            else
            {
                changed |= _transform.SetLocal(ref _sharedTransformSource);
            }
        }

        return changed;  
    }

    /// <summary>
    /// Update the _sharedTransformSource's world pose, and local scale with the latest sharing transform
    /// </summary>
    private void UpdateSharedTransformSource()
    {
        if (_sharedTransformSource == null)
        {
            return;
        }

        // The position and rotation can't be shared without a stage/collaboration origin
        if (!IsOrigin())
        {
            if (HasMovable() && !useLocal)
            {
                var worldPose = OriginToWorld(_transform.Position, _transform.Rotation);
                _sharedTransformSource.SetPositionAndRotation(worldPose.worldPosition, worldPose.worldRotation);
            }
            else
            {
                _sharedTransformSource.localPosition = _transform.Position;
                _sharedTransformSource.localRotation = _transform.Rotation;
            }
        }

        _sharedTransformSource.localScale = _transform.Scale;
    }

    /// <summary>
    /// Is this at the root of the shared scene
    /// </summary>
    private bool IsOrigin()
    {
        return HasMovable() && movable.IsOrigin;
    }

    /// <summary>
    /// Is this object anchored.
    /// </summary>
    private bool HasMovable()
    {
        return movable != null;
    }

    /// <summary>
    /// Transform the given global position and rotation so it's relative to the model's stage/collaboration origin
    /// </summary>
    private (Vector3 originPosition, Quaternion originRotation) WorldToOrigin(Vector3 worldPosition, Quaternion worldRotation)
    {
        return movable.WorldToOrigin(worldPosition, worldRotation);
    }

    /// <summary>
    /// Transform the given stage/collaboration position and rotation so it's relative to the game's world space.
    /// </summary>
    private (Vector3 worldPosition, Quaternion worldRotation) OriginToWorld(Vector3 originPosition, Quaternion originRotation)
    {
        return movable.OriginToWorld(originPosition, originRotation);
    }
    #endregion Private Functions

    #region Interfaces and Classes
    /// <summary>
    /// Stores historic transform data that can be re-applied and shared.
    /// </summary>
    public interface ITransformHistory
    {
        /// <summary>
        /// Re-apply the saved state
        /// </summary>
        void Apply();
    }

    /// <summary>
    /// Stores historic transform data that can be re-applied and shared.
    /// </summary>
    private class TransformHistory : ITransformHistory
    {
        Action<SharingServiceTransform> _callback;
        SharingServiceTransform _transform;

        public TransformHistory(SharingServiceTransform save, Action<SharingServiceTransform> load)
        {
            _transform = save;
            _callback = load;
        }

        /// <summary>
        /// Re-apply the saved state
        /// </summary>
        public void Apply()
        {
            _callback?.Invoke(_transform);
        }
    }
    #endregion Interfaces and Classes
}
