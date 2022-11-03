// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.UI;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// A behavior which wraps components the can manipulate a transform, and exposes events that are raised when the
    /// Movable transform is moving or is moved.
    /// </summary>
    public class MovableObject : MonoBehaviour
    {
        private int _manipulationCounts = 0;
        private float _fireMovingEndedDelayInSeconds = 1f;
        private LogHelper<MovableObject> _log = new LogHelper<MovableObject>();
        private MovableObjectOrigin _origin = null;

        #region Serialized Fields
        [Header("Movable Parts")]

        [SerializeField]
        [Tooltip("A child transform that can be moved. This must be a child of this transform. If null, this transform is moved directly.")]
        private Transform movable;

        /// <summary>
        /// A child transform that can be moved. This must be a child of this transform. If null, this transform is moved directly.
        /// </summary>
        public Transform Movable
        {
            get => movable;

            set
            {
                if (movable != value)
                {
                    movable = value;
                    ValidateMovableChild();
                }
            }
        }

        [Header("Transforms Changers")]

        [SerializeField]
        [Tooltip("The object manipulator that can change this object's position and rotation..")]
        private ObjectManipulator manipulation;

        /// <summary>
        /// The manipulation handler that will change this object's position and rotation.
        /// </summary>
        public ObjectManipulator Manipulation
        {
            get => manipulation;
            set => manipulation = value;
        }

        [SerializeField]
        [Tooltip("The object placer that can change this object's position and rotation.")]
        private ObjectPlacement placement;

        /// <summary>
        /// The object placer that can change this object's position and rotation.
        /// </summary>
        public ObjectPlacement Placement
        {
            get => placement;
            set => placement = value;
        }

        [SerializeField]
        [Tooltip("The 'place on target' object that can change this object's position and rotation.")]
        private PlaceOnTarget onTarget;

        /// <summary>
        /// The 'place on target' object that can change this object's position and rotation.
        /// </summary>
        public PlaceOnTarget OnTarget
        {
            get => onTarget;
            set => onTarget = value;
        }

        [Header("Movable Events")]

        [SerializeField]
        [Tooltip("Event fired when this object has started to move.")]
        private UnityEvent moving = new UnityEvent();

        /// <summary>
        /// Event fired when this object has started to move.
        /// </summary>
        public UnityEvent Moving => moving;

        [SerializeField]
        [Tooltip("Event fired when this object is about to finish moving.")]
        private UnityEvent movingEnding = new UnityEvent();

        /// <summary>
        /// Event fired when this object is about to finish moving.
        /// </summary>
        public UnityEvent MovingEnding => movingEnding;

        [SerializeField]
        [Tooltip("Event fired when this object has finished moving.")]
        private UnityEvent moved = new UnityEvent();

        /// <summary>
        /// Event fired when this object has finished moving.
        /// </summary>
        public UnityEvent Moved => moved;
        #endregion Serialized Fields

        #region Public Properties
        /// <summary>
        /// Check if the movable component is a child of this transform
        /// </summary>
        public bool HasMovableChild => movable != null && movable != transform;

        /// <summary>
        /// Get if the object is being moved.
        /// </summary>
        public bool IsMoving => _manipulationCounts > 0;

        /// <summary>
        /// Get if this transform is the origin.
        /// </summary>
        public bool IsOrigin { get; private set; }
        #endregion Public Properties

        #region MonoBehaviour Functions
        /// <summary>
        /// In editor validate the field changes.
        /// </summary>
        private void OnValidate()
        {
            ValidateMovableChild();
        }

        /// <summary>
        /// On start validate the movable target, and create a new AppAnchor is needed.
        /// </summary>
        protected virtual void Awake()
        {
            IsOrigin = GetComponent<MovableObjectOrigin>() != null;

            if (manipulation == null)
            {
                manipulation = GetComponent<ObjectManipulator>();
            }

            if (placement == null)
            {
                placement = GetComponent<ObjectPlacement>();
            }

            if (onTarget == null)
            {
                onTarget = GetComponent<PlaceOnTarget>();
            }

            ValidateMovableChild();

            if (manipulation != null)
            {
                manipulation.HostTransform = movable ?? transform;
                manipulation.OnManipulationStarted.AddListener(OnManipulationStarted);
                manipulation.OnManipulationEnded.AddListener(OnManipulationEnded);
            }

            if (placement != null)
            {
                placement.Target = movable ?? transform;
                placement.OnPlacing.AddListener(OnMovingStarted);
                placement.OnPlaced.AddListener(OnMovingEnded);
            }

            if (onTarget != null)
            {
                onTarget.OnPlaced.AddListener(OnMovingStartedAndEnded);
            }
        }

        protected virtual void OnDestroy()
        {
            if (manipulation != null)
            {
                manipulation.OnManipulationStarted.RemoveListener(OnManipulationStarted);
                manipulation.OnManipulationEnded.RemoveListener(OnManipulationEnded);
            }

            if (placement != null)
            {
                placement.OnPlacing.RemoveListener(OnMovingStarted);
                placement.OnPlaced.RemoveListener(OnMovingEnded);
            }

            if (onTarget != null)
            {
                onTarget.OnPlaced.RemoveListener(OnMovingStartedAndEnded);
            }
        }

        private void OnDisable()
        {
            // Handle pending events now, so they are not lost
            OnMovingEnded(delay: false, forceEndingAllMoves: true);
        }
        #endregion MonoBehaviour Functions

        #region Public Functions
        /// <summary>
        /// Convert the given global position and rotation so it's relative to this origin transform.
        /// </summary>
        public (Vector3 originPosition, Quaternion originRotation) WorldToOrigin(Vector3 worldPosition, Quaternion worldRotation)
        {
            return FindOrigin() ?
                _origin.WorldToLocal(worldPosition, worldRotation) :
                (worldPosition, worldRotation);
        }

        /// <summary>
        /// Transform the given origin position and rotation so it's relative to the game's world space.
        /// </summary>
        public (Vector3 worldPosition, Quaternion worldRotation) OriginToWorld(Vector3 originPosition, Quaternion originRotation)
        {
            return FindOrigin() ?
                _origin.LocalToWorld(originPosition, originRotation) :
                (originPosition, originRotation);
        }

        /// <summary>
        /// Move the Movable transform to the given origin location.
        /// </summary>
        public void MoveOrigin(Vector3 originPosition, Quaternion originRotation)
        { 
            var pose = OriginToWorld(originPosition, originRotation);
            Move(pose.worldPosition, pose.worldRotation);
        }

        /// <summary>
        /// Move the Movable transform to the given world location.
        /// </summary>
        public void Move(Vector3 worldPosition, Quaternion worldRotation)
        {
            _log.LogVerbose($"Move() ENTER (name: {name}) (worldPosition: {worldPosition}) (worldRotation: {worldRotation})");
            bool moved = false;

            if (transform.position != worldPosition)
            {
                moved = true;
                transform.position = worldPosition;
            }

            if (transform.rotation != worldRotation)
            {
                moved = true;
                transform.rotation = worldRotation;
            }

            if (HasMovableChild)
            {
                if (Movable.localPosition != Vector3.zero)
                {
                    moved = true;
                    Movable.localPosition = Vector3.zero;
                }

                if (Movable.localRotation != Quaternion.identity)
                {
                    moved = true;
                    Movable.localRotation = Quaternion.identity;
                }
            }

            if (moved)
            {
                OnMovingStartedAndEnded();
            }

            _log.LogVerbose($"Move() EXIT (name: {name}) (worldPosition: {worldPosition}) (worldRotation: {worldRotation})");
        }
        #endregion Public Functions

        #region Protected Functions
        /// <summary>
        /// Extending classes can handle ending of manipulation here, before Moved event fired.
        /// </summary>
        protected virtual void HandleOnMovingEnding()
        {
        }

        /// <summary>
        /// Extending classes can handle ending of manipulation here, after Moved event fired.
        /// </summary>
        protected virtual void HandleOnMovingEnded()
        {
        }
        #endregion Protected Functions

        #region Private Functions
        private bool FindOrigin()
        {
            if (_origin == null)
            {
                _origin = GetComponentInParent<MovableObjectOrigin>();
            }

            return _origin != null;
        }
        /// <summary>
        /// Validate that movable is a child of this.
        /// </summary>
        private void ValidateMovableChild()
        {
            if (movable != null && movable != transform && !movable.IsChildOf(transform))
            {
                Debug.LogError($"The transform '{movable.name}' is not a child of '{name}'");
                movable = null;
            }
        }

        /// <summary>
        /// Invoked when the object has starting to be moved by a MRTK manipulation component
        /// </summary>
        private void OnManipulationStarted(ManipulationEventData args)
        {
            OnMovingStarted();
        }

        /// <summary>
        /// Invoked when the object has starting to be moved.
        /// </summary>
        private void OnMovingStarted()
        {
            if (_manipulationCounts++ == 0)
            {
                try
                {
                    moving?.Invoke();
                }
                catch (Exception ex)
                {
                    _log.LogError("Exception occurred while raising moving event. Exception: {0}", ex);
                }
            }
        }

        /// <summary>
        /// Invoked when the object has stopped being moved by a MRTK manipulation component
        /// </summary>
        private void OnManipulationEnded(ManipulationEventData args)
        {
            // Manipulation ends have a higher chance to being immediately followed by a manipulation started
            OnMovingEnded(delay: true, forceEndingAllMoves: false);
        }

        /// <summary>
        /// Combine moving starting and ending into a single routine.
        /// </summary>
        private void OnMovingStartedAndEnded()
        {
            OnMovingStarted();
            OnMovingEnded();
        }

        /// <summary>
        /// Invoked when the object has stopped being moved.
        /// </summary>
        private void OnMovingEnded()
        {
            OnMovingEnded(delay: false, forceEndingAllMoves: false);
        }

        /// <summary>
        /// Invoked when the object has stopped being moved.
        /// </summary>
        private void OnMovingEnded(bool delay, bool forceEndingAllMoves)
        {
            if (_manipulationCounts > 0)
            {
                if (delay)
                {
                    StartCoroutine(OnMovingEndedDelayed(forceEndingAllMoves));
                }
                else
                {
                    if (forceEndingAllMoves)
                    {
                        _manipulationCounts = 1;
                    }

                    // Don't reduce count yet, as handling ending may trigger an additional move
                    if (_manipulationCounts == 1)
                    {
                        HandleOnMovingEnding();
                    }

                    FireMovingEnding();

                    if (_manipulationCounts > 0)
                    {
                        _manipulationCounts--;
                    }

                    FireMoved();

                    // During moved event, something could have started another move.
                    if (_manipulationCounts == 0)
                    {
                        HandleOnMovingEnded();
                    }
                }
            }
        }

        /// <summary>
        /// Fire manipulation ended events after a short delay.
        /// </summary>
        private IEnumerator OnMovingEndedDelayed(bool force)
        {
            yield return new WaitForSecondsRealtime(_fireMovingEndedDelayInSeconds);
            OnMovingEnded(delay: false, force);
        }

        /// <summary>
        /// Only fire movingEnding when about to stop moving.
        /// </summary>
        private void FireMovingEnding()
        {
            if (_manipulationCounts == 1)
            {
                try
                {
                    movingEnding?.Invoke();
                }
                catch (Exception ex)
                {
                    _log.LogError("Exception occurred while raising movingEnding event. Exception: {0}", ex);
                }
            }
        }

        /// <summary>
        /// Only fire moved when not moving.
        /// </summary>
        private void FireMoved()
        {
            if (_manipulationCounts == 0)
            {
                try
                {
                    moved?.Invoke();
                }
                catch (Exception ex)
                {
                    _log.LogError("Exception occurred while raising moved event. Exception: {0}", ex);
                }
            }
        }
        #endregion Private Functions
    }
}
