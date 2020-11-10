// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.UI;
using System;
using UnityEngine;
using UnityEngine.Events;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// A behavior which wraps components the can manipulate a transform, and exposes events that are raised when the
    /// Movable 4133transform is moving or is moved.
    /// </summary>
    public class MovableObject : MonoBehaviour
    {
        private int _manipulationCounts = 0;

        #region Serialized Fields
        [Header("Movable Parts")]

        [SerializeField]
        [Tooltip("A child transform that is being moved.null This must be a child of this transform.")]
        private Transform movable;

        /// <summary>
        /// A child transform that is being moved.null This must be a child of this transform.
        /// </summary>
        public Transform Movable
        {
            get => movable;

            set
            {
                if (movable != value)
                {
                    movable = value;
                    ValidateMovable();
                }
            }
        }

        [Header("Transforms Changers")]

        [SerializeField]
        [Tooltip("The manipulation handler that can change this object's position and rotation..")]
        private ManipulationHandler manipulation;

        /// <summary>
        /// The manipulation handler that will change this object's position and rotation.
        /// </summary>
        public ManipulationHandler Manipulation
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
        [Tooltip("Event fired when this anchor is being moved.")]
        private UnityEvent moving = new UnityEvent();

        /// <summary>
        /// Event fired when this anchor is being moved
        /// </summary>
        public UnityEvent Moving => moving;

        [SerializeField]
        [Tooltip("Event fired when this anchor has changed position.")]
        private UnityEvent moved = new UnityEvent();

        /// <summary>
        /// Event fired when this anchor has changed position."
        /// </summary>
        public UnityEvent Moved => moved;
        #endregion Serialized Fields

        #region Public Properties
        /// <summary>
        /// Get if the object is being moved.
        /// </summary>
        public bool IsMoving => _manipulationCounts > 0;
        #endregion Public Properties

        #region MonoBehaviour Functions
        /// <summary>
        /// In editor validate the field changes.
        /// </summary>
        private void OnValidate()
        {
            ValidateMovable();
        }

        /// <summary>
        /// On start validate the movable target, and create a new AppAnchor is needed.
        /// </summary>
        protected virtual void Awake()
        {
            if (manipulation == null)
            {
                manipulation = GetComponent<ManipulationHandler>();
            }

            if (placement == null)
            {
                placement = GetComponent<ObjectPlacement>();
            }

            if (onTarget == null)
            {
                onTarget = GetComponent<PlaceOnTarget>();
            }

            ValidateMovable();

            if (manipulation != null)
            {
                manipulation.HostTransform = movable ?? transform;
                manipulation.OnManipulationStarted.AddListener(OnManipulationStarted);
                manipulation.OnManipulationEnded.AddListener(OnManipulationEnded);
            }

            if (placement != null)
            {
                placement.Target = movable ?? transform;
                placement.OnPlacing.AddListener(OnManipulationStarted);
                placement.OnPlaced.AddListener(OnManipulationEnded);
            }

            if (onTarget != null)
            {
                onTarget.OnPlaced.AddListener(OnManipulationEnded);
            }
        }

        protected virtual void OnDestroy()
        {
            if (manipulation != null)
            {
                manipulation.OnManipulationEnded.RemoveListener(OnManipulationEnded);
            }

            if (placement != null)
            {
                placement.OnPlaced.RemoveListener(OnManipulationEnded);
            }

            if (onTarget != null)
            {
                onTarget.OnPlaced.RemoveListener(OnManipulationEnded);
            }
        }
        #endregion MonoBehaviour Functions

        #region Protected Functions
        /// <summary>
        /// Extending classes can handle ending of manipulation here, before Moved event fired.
        /// </summary>
        protected virtual void HandleOnManipulationEnding()
        {
        }

        /// <summary>
        /// Extending classes can handle ending of manipulation here, after Moved event fired.
        /// </summary>
        protected virtual void HandleOnManipulationEnded()
        {
        }

        /// <summary>
        /// Extending classes can invoke this to notify the base that this object has moved.
        /// </summary>
        protected void NotifyMoved()
        {
            FireMoved();
        }
        #endregion Protected Functions

        #region Private Functions
        /// <summary>
        /// Validate that movable is a child of this.
        /// </summary>
        private void ValidateMovable()
        {
            if (movable != null && !movable.IsChildOf(transform))
            {
                Debug.LogError($"The transform '{movable.name}' is not a child of '{name}'");
                movable = null;
            }
        }

        /// <summary>
        ///  Invoked when the object has starting to be moved.
        /// </summary>
        private void OnManipulationStarted(ManipulationEventData args)
        {
            OnManipulationStarted();
        }

        /// <summary>
        ///  Invoked when the object has starting to be moved.
        /// </summary>
        private void OnManipulationStarted()
        {
            if (_manipulationCounts++ == 0)
            {
                moving?.Invoke();
            }
        }

        /// <summary>
        /// Invoked when the object has stopped being moved.
        /// </summary>
        private void OnManipulationEnded(ManipulationEventData args)
        {
            OnManipulationEnded();
        }

        /// <summary>
        ///  Invoked when the object has stopped being moved.
        /// </summary>
        private void OnManipulationEnded()
        {
            HandleOnManipulationEnding();
            _manipulationCounts = Math.Max(--_manipulationCounts, 0);
            FireMoved();
            HandleOnManipulationEnded();
        }

        /// <summary>
        /// Only fire moved when not manipulating. When manipulation is complete, this will be invoked.
        /// </summary>
        private void FireMoved()
        {
            if (_manipulationCounts <= 0)
            {
                moved?.Invoke();
            }
        }
        #endregion Private Functions
    }
}
