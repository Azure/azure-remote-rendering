// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// A MovableObject which wraps an IAppAnchor. If MovableObject.Movable, a child transform, moves far enough from 
    /// the anchor, the anchor is moved to the child's location.
    /// </summary>
    public class MovableAnchor : MovableObject
    {
        private IAppAnchor _anchor = null;
        private float _maxAnchorDistanceSquared;
        private LogHelper<MovableAnchor> _log = new LogHelper<MovableAnchor>();

        #region Serialized Fields
        [Header("Anchor Settings")]

        [SerializeField]
        [Tooltip("The distance the movable transform can be from the anchor before a new anchor is created. If negative, new anchors are not created after a move.")]
        private float maxAnchorDistance = 5.0f;

        /// <summary>
        /// The distance the movable transform can be from the anchor before a new anchor is created. If negative, new anchors are not created after a move.
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
        #endregion Serialized Fields

        #region Public Properties
        /// <summary>
        /// Is object's anchor located
        /// </summary>
        public bool IsAnchorLocated => _anchor?.IsLocated ?? false;

        /// <summary>
        /// Is there an anchor
        /// </summary>
        public bool HasAnchor => _anchor != null;

        /// <summary>
        /// Get the anchor's cloud id
        /// </summary>
        public string AnchorId => _anchor != null ? _anchor.AnchorId : null;

        /// <summary>
        /// Get the inner anchor position
        /// </summary>
        public Transform AnchorTransform => _anchor == null ? null : _anchor.Transform;
        #endregion Public Properties

        #region MonoBehaviour Functions
        /// <summary>
        /// On start validate the movable target, and create a new AppAnchor is needed.
        /// </summary>
        protected void Start()
        {
            _log.LogVerbose($"Start() ENTER (name: {name})");
            _maxAnchorDistanceSquared = maxAnchorDistance * maxAnchorDistance;
            CreateAnchorIfEmpty();
        }
        
        /// <summary>
        /// Every frame about the position of this transform to the anchor's position.
        /// </summary>
        private void Update()
        {
            ForceUpdate();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            ReleaseOldAnchor(preventDelete: true);
        }

        private void OnApplicationQuit()
        {
            // avoid deleting of cloud anchor during app exit
            _anchor = null;
        }
        #endregion MonoBehaviour Functions

        #region Public Functions
        /// <summary>
        /// Force updating pose to the current anchor pose.
        /// </summary>
        public void ForceUpdate()
        {
            if (_anchor != null && _anchor.IsLocated)
            {
                transform.position = _anchor.Position;
                transform.rotation = _anchor.Rotation;
            }
        }

        /// <summary>
        /// Apply a copy of the give app anchor
        /// </summary>
        public void ApplyAnchor(IAppAnchor anchor)
        {
            if (anchor != null)
            {
                SetAnchor(anchor);
            }
        }
        #endregion Public Functions

        #region Protected Functions
        /// <summary>
        /// Invoked when the object has stopped being moved. If the anchor's movable part moves too far from the anchor's origin,
        /// reset the anchor so it's at the movable part's location.
        /// </summary>
        protected override void HandleOnMovingEnding()
        {
            if (HasMovableChild && _anchor != null)
            {
                Vector3 movedDistance = _anchor.Position - Movable.position;
                bool resetExistingAnchor = maxAnchorDistance >= 0 && movedDistance.sqrMagnitude >= _maxAnchorDistanceSquared;

                if (resetExistingAnchor)
                {
                    _log.LogVerbose("Moving anchor, as object moved farther than the max distance of {0} m (name: {1}) (anchor: {2})", maxAnchorDistance, name, _anchor?.AnchorId);
                    MoveAnchorDuringMoveEnding(Movable.position, Movable.rotation);
                }
                else
                {
                    _log.LogVerbose("Not moving anchor, as object did not moved farther than the max distance of {0} m (name: {1}) (anchor: {2})", maxAnchorDistance, name, _anchor?.AnchorId);
                }
            }
        }
        #endregion Protected Functions

        #region Private Functions
        /// <summary>
        /// Create a new anchor if current anchor is null.
        /// </summary>
        private async void CreateAnchorIfEmpty()
        {
            IAppAnchor newAnchor = null;
            if (_anchor == null)
            {
                try
                {
                    newAnchor = await AppAnchor.Create(name, transform);
                }
                catch (Exception ex)
                {
                    _log.LogError("Failed to create app anchor. {0}", ex);
                }
            }

            if (newAnchor != null)
            {
                if (_anchor == null)
                {
                    SetAnchor(newAnchor);
                }
                else
                {
                    newAnchor.Dispose();
                }
            }
        }

        /// <summary>
        /// Set a new IAppAnchor value.
        /// </summary>
        private void SetAnchor(IAppAnchor value)
        {
            bool possibleChangedId = (_anchor == null || value == null || _anchor.AnchorId != value.AnchorId || !value.FromCloud);
            if (_anchor != value && possibleChangedId)
            {
                ReleaseOldAnchor(preventDelete: true);
                _anchor = value;
            }
        }

        /// <summary>
        /// Move both the anchor and the Movable transform to the given global location
        /// </summary>
        private async void MoveAnchorDuringMoveEnding(Vector3 globalPosition, Quaternion globalRotation)
        {
            _log.LogVerbose("MoveAnchor() ENTER (name: {0})", name);

            transform.position = globalPosition;
            transform.rotation = globalRotation;
            Movable.localPosition = Vector3.zero;
            Movable.localRotation = Quaternion.identity;

            if (_anchor.Position != globalPosition ||
                _anchor.Rotation != globalRotation)
            {
                _log.LogVerbose("MoveAnchor() START ASYNC (name: {0})", name);
                try
                {
                    await _anchor.Move(transform);
                }
                catch (Exception ex)
                {
                    _log.LogError("MoveAnchor() Failed to move anchor, object won't be positioned correctly (name: {0}): {1}", name, ex);
                }
                _log.LogVerbose("MoveAnchor() STOP ASYNC (name: {0})", name);
            }

            _log.LogVerbose("MoveAnchor() EXIT (name: {0})", name);
        }

        /// <summary>
        /// Release anchor handlers
        /// </summary>
        private void ReleaseOldAnchor(bool preventDelete = false)
        {
            if (_anchor == null)
            {
                return;
            }

            if (!preventDelete)
            {
                _anchor.Delete();
            }

            _anchor = null;
        }
        #endregion Private Functions
    }
}
