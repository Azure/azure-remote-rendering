// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;
using UnityEngine.Events;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// A MovableObject which wraps an IAppAnchor. If MovableObject.Movable, a child transform, moves far enough from 
    /// the anchor, the anchor is moved to the child's location.
    /// </summary>
    public class MovableAnchor : MovableObject
    {
        private IAppAnchor _anchor = null;
        private bool _started = false;
        private float _maxAnchorDistanceSquared;

        #region Serialized Fields
        [Header("Anchor Settings")]

        [SerializeField]
        [Tooltip("The distance to movable transform can be from the anchor before the anchor is moved.")]
        private float maxAnchorDistance = 5.0f;

        /// <summary>
        /// The distance to movable transform can be
        /// from the anchor before the anchor is moved.
        /// </summary>
        public float MaxAnchorDistance
        {
            get => maxAnchorDistance;

            set
            {
                if (maxAnchorDistance != value)
                {
                    maxAnchorDistance = value;
                    _maxAnchorDistanceSquared = value * value;
                }
            }
        }

        [SerializeField]
        [Tooltip("Should a cloud anchor be created for this 'movable' anchor.")]
        private bool createCloudAnchor = true;

        /// <summary>
        /// Should a cloud anchor be created for this 'movable' anchor.
        /// </summary>
        public bool CreateCloudAnchor
        {
            get => createCloudAnchor;
            set => createCloudAnchor = value;
        }

        [Header("Anchor Fallbacks")]

        [SerializeField]
        [Tooltip("If a cloud anchor id is supplied and not found, the 'Fallback Origin', 'Fallback Position', and 'Fallback Rotation' are used to place this transform.")]
        private Transform fallbackOrigin = null;

        /// <summary>
        /// If a cloud anchor id is supplied and not found, the 'Fallback Origin', 'Fallback Position', and 'Fallback Rotation' are used to place this transform.
        /// </summary>
        public Transform FallbackOrigin
        {
            get => fallbackOrigin;
            set => fallbackOrigin = value;
        }

        [Tooltip("If a cloud anchor id is supplied and not found, the 'Fallback Origin', 'Fallback Position', and 'Fallback Rotation' are used to place this transform.")]
        private Vector3 fallbackPosition = Vector3.positiveInfinity;

        /// <summary>
        /// If a cloud anchor id is supplied and not found, the 'Fallback Origin', 'Fallback Position', and 'Fallback Rotation' are used to place this transform.
        /// </summary>
        public Vector3 FallbackPosition
        {
            get => fallbackPosition;
        }

        [Tooltip("If a cloud anchor id is supplied and not found, the 'Fallback Origin', 'Fallback Position', and 'Fallback Rotation' are used to place this transform.")]
        private Quaternion fallbackRotation = QuaternionStatics.PositiveInfinity;

        /// <summary>
        /// If a cloud anchor id is supplied and not found, the 'Fallback Origin', 'Fallback Position', and 'Fallback Rotation' are used to place this transform.
        /// </summary>
        public Quaternion FallbackRotation
        {
            get => fallbackRotation;
        }

        [Header("Anchor Events")]

        [SerializeField]
        [Tooltip("Event fired when the anchor id has changed.")]
        private AnchorIdChangedEvent anchorIdChanged = new AnchorIdChangedEvent();

        /// <summary>
        /// Event fired when the anchor id has changed.
        /// </summary>
        public AnchorIdChangedEvent AnchorIdChanged => anchorIdChanged;
        #endregion Serialized Fields

        #region Public Properties
        /// <summary>
        /// Get the id that represent an anchor with no cloud id.
        /// </summary>
        public static string EmptyAnchorId => AppAnchor.EmptyAnchorId;

        /// <summary>
        /// Get the current cloud anchor id.
        /// </summary>
        public string AnchorId => _anchor?.AnchorId;
        #endregion Public Properties

        #region MonoBehaviour Functions
        /// <summary>
        /// On start validate the movable target, and create a new AppAnchor is needed.
        /// </summary>
        protected void Start()
        {
            _started = true;
            _maxAnchorDistanceSquared = maxAnchorDistance * maxAnchorDistance;

            if (_anchor == null)
            {
                ApplyNativeAnchor();
            }
            else
            {
                ApplyCurrentAnchor();
            }
        }
        
        /// <summary>
        /// Every frame about the position of this transform to the anchor's position.
        /// </summary>
        private void Update()
        {
            if (_anchor != null && _anchor.IsLocated &&  _anchor.Transform != null)
            {
                transform.position = _anchor.Transform.position;
                transform.rotation = _anchor.Transform.rotation;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            ReleaseOldAnchor();
        }

        private void OnApplicationQuit()
        {
            // avoid deleting of cloud anchor during app exit
            _anchor = null;
        }
        #endregion MonoBehaviour Functions

        #region Public Functions
        /// <summary>
        /// Move anchor to the last known fallback transform, plus the additional movement performed on the Movable child transform.
        /// </summary>
        public void ResetAnchor()
        {
            if (fallbackPosition.IsValidVector() && fallbackRotation.IsValidRotation() && fallbackOrigin != null)
            {
                transform.localPosition = fallbackPosition;
                transform.localRotation = fallbackRotation;
            }

            if (Movable != null)
            {
                transform.position = Movable.position;
                transform.rotation = Movable.rotation;
            }

            MoveAnchor(transform.position, transform.rotation);
        }

        /// <summary>
        /// Move the inner anchor via local postion and rotation values.
        /// </summary>
        public void LocalMoveAnchor(Vector3 localPosition, Quaternion localRotation)
        {
            transform.localPosition = localPosition;
            transform.localRotation = localRotation;

            if (Movable != null)
            {
                Movable.localPosition = Vector3.zero;
                Movable.localRotation = Quaternion.identity;
            }

            TrySavingFallback();
            _anchor?.Move(transform);
            NotifyMoved();
        }

        /// <summary>
        /// Move the inner anchor via world position and rotation values.
        /// </summary>
        public void MoveAnchor(Vector3 worldPosition, Quaternion worldRotation)
        {
            transform.position = worldPosition;
            transform.rotation = worldRotation;

            if (Movable != null)
            {
                Movable.localPosition = Vector3.zero;
                Movable.localRotation = Quaternion.identity;
            }

            TrySavingFallback();
            _anchor?.Move(transform);
            NotifyMoved();
        }

        /// <summary>
        /// Apply the given cloud anchor to this object. Note this will delete the old anchor that's currently being used.
        /// </summary>
        public void ApplyCloudAnchor(string anchorId, Vector3 fallbackPosition, Quaternion fallbackRotation)
        {
            this.fallbackPosition = fallbackPosition;
            this.fallbackRotation = fallbackRotation;
            TryUsingFallback();

            if ((_anchor == null || _anchor.AnchorId != anchorId) &&
                (!string.IsNullOrEmpty(anchorId)))
            {
                SetAnchor(new AppAnchor(anchorId, allowNewCloudAnchors: createCloudAnchor));
            }
        }

        /// <summary>
        /// Create and apply a new native anchor to this object. Note this will delete the old that's currently being used.
        /// </summary>
        public void ApplyNativeAnchor(Vector3 fallbackPosition, Quaternion fallbackRotation)
        {
            this.fallbackPosition = fallbackPosition;
            this.fallbackRotation = fallbackRotation;
            TryUsingFallback();

            if (transform != null)
            {
                SetAnchor(new AppAnchor(transform, createCloudAnchor));
            }
        }

        /// <summary>
        /// Create and apply a new native anchor to this object. Note this will delete the old that's currently being used.
        /// </summary>
        public void ApplyNativeAnchor()
        {
            TrySavingFallback();

            if (transform != null)
            {
                SetAnchor(new AppAnchor(transform, createCloudAnchor));
            }
        }

        /// <summary>
        /// Create and apply an empty anchor to this object. Note this will delete the old that's currently being used.
        /// An empty won't have a cloud or native anchor attached.
        /// </summary>
        public void ApplyEmptyAnchor()
        {
            TrySavingFallback();
            SetAnchor(new AppAnchor(createCloudAnchor));
        }
        #endregion Public Functions

        #region Protected Functions
        /// <summary>
        /// Invoked when the object has stopped being moved. If the anchor's movable part moves too far from the anchor's origin,
        /// reset the anchor so it's at the movable part's location.
        /// </summary>
        protected override void HandleOnManipulationEnded()
        {
            bool movableTransformFarFromAnchor = (Movable != null) && ((transform.position - Movable.position).sqrMagnitude >= _maxAnchorDistanceSquared);
            bool resetExistingAnchor = _anchor == null || movableTransformFarFromAnchor;

            // Only reset if the current platform supports anchors.
            if (resetExistingAnchor && AppAnchor.AnchorsSupported())
            {
                MoveAnchor(Movable.position, Movable.rotation);
            }
        }
        #endregion Protected Functions

        #region Private Functions
        /// <summary>
        /// Apply the current anchor that may have been set before Start() was called.
        /// </summary>
        private void ApplyCurrentAnchor()
        {
            HandleNewAnchor();
        }

        /// <summary>
        /// If the fallback origin is null, find it.
        /// </summary>
        private bool EnsureFallbackOrigin()
        {
            if (fallbackOrigin == null)
            {
                fallbackOrigin = FindFallbackOrigin();
            }

            return fallbackOrigin != null;
        }

        /// <summary>
        /// Find the topmost MovableAnchor, and use it as the fallback origin.
        /// </summary>
        private Transform FindFallbackOrigin()
        {
            MovableAnchor result = transform.GetComponentInParent<MovableAnchor>();
            while (result.transform.parent != null)
            {
                var next = result.transform.parent.GetComponentInParent<MovableAnchor>();
                if (next == null)
                {
                    break;
                }
                result = next;
            }

            if (result.transform == transform)
            {
                return null;
            }
            else
            {
                return result.transform;
            }
        }

        /// <summary>
        /// Attempt to move to fallback position and rotation. This is called when the fallback's are changed. If
        /// an anchor originated locally, this request is ignored. 
        /// </summary>
        private void TryUsingFallback()
        {
            EnsureFallbackOrigin();
            if (fallbackOrigin == null || !fallbackPosition.IsValidVector() || !fallbackRotation.IsValidRotation())
            {
                return;
            }

            transform.position = fallbackOrigin.TransformPoint(fallbackPosition);
            transform.rotation = fallbackOrigin.rotation * fallbackRotation;
        }

        /// <summary>
        /// Attempt to save the current position and rotation as the fallback positions and rotations.
        /// </summary>
        /// <returns>
        /// True if the fallback values have changed.
        /// </returns>
        private void TrySavingFallback()
        {
            EnsureFallbackOrigin();
            if (fallbackOrigin == null)
            {
                return;
            }

            fallbackPosition = fallbackOrigin.InverseTransformPoint(transform.position);
            fallbackRotation = transform.rotation * Quaternion.Inverse(fallbackOrigin.rotation);
        }

        /// <summary>
        /// Set a new IAppAnchor value.
        /// </summary>
        private void SetAnchor(IAppAnchor value)
        {
            bool possibleChangedId = (_anchor == null || value == null || _anchor.AnchorId != value.AnchorId);
            if (_anchor != value && possibleChangedId)
            {
                ReleaseOldAnchor();
                _anchor = value;
                HandleNewAnchor();
            }
        }

        /// <summary>
        /// Initialize anchor handlers
        /// </summary>
        private void HandleNewAnchor()
        {
            if (!_started)
            {
                return;
            }

            if (_anchor != null)
            {
                _anchor.AnchorIdChanged += OnAnchorIdChanged;
            }

            FireAnchorChanged();
        }

        /// <summary>
        /// Release anchor handlers
        /// </summary>
        private void ReleaseOldAnchor()
        {
            if (_anchor == null)
            {
                return;
            }

            _anchor.AnchorIdChanged -= OnAnchorIdChanged;
            _anchor.Delete();
            _anchor = null;
        }

        /// <summary>
        /// Handle anchor id changes.
        /// </summary>
        private void OnAnchorIdChanged(IAppAnchor sender, string newId)
        {
            FireAnchorChanged();
        }

        /// <summary>
        /// Fire an event indicating that the anchor id has changed.
        /// </summary>
        private void FireAnchorChanged()
        {
            AnchorIdChanged?.Invoke(new AnchorIdChangedEventArgs()
            {
                anchorId = AnchorId,
                fallbackPosition = fallbackPosition,
                fallbackRotation = fallbackRotation
            });
        }
        #endregion Private Functions

        #region Public Classes
        /// <summary>
        /// An event raised when the anchor id changes.
        /// </summary>
        [Serializable]
        public class AnchorIdChangedEvent : UnityEvent<AnchorIdChangedEventArgs>
        {
        }

        /// <summary>
        /// Event data used when an event is raised during anchor id changes.
        /// </summary>
        [Serializable]
        public struct AnchorIdChangedEventArgs
        {
            public string anchorId;
            public Vector3 fallbackPosition;
            public Quaternion fallbackRotation;
        }
        #endregion Public Classes
    }
}
