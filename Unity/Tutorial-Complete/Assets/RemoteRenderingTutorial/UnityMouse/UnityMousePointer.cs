// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities;
using Microsoft.MixedReality.Toolkit.Physics;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Input
{
    /// <summary>
    /// Internal Touch Pointer Implementation.
    /// </summary>
    public class UnityMousePointer : BaseControllerPointer, IMixedRealityMousePointer
    {
        #region Serialized Fields
        [SerializeField]
        [Tooltip("When should the system cursor be shown.")]
        private SystemCursorVisibilityChanges systemCursorVisibilityChanges = SystemCursorVisibilityChanges.Always;

        [SerializeField]
        [Tooltip("Should the mouse cursor be hidden when no active input is received?")]
        private bool hideCursorWhenInactive = true;

        [SerializeField]
        [Range(0.01f, 1f)]
        [Tooltip("What is the movement threshold to reach before un-hiding mouse cursor?")]
        private float movementThresholdToUnHide = 0.1f;

        [SerializeField]
        [Range(0f, 10f)]
        [Tooltip("How long should it take before the mouse cursor is hidden?")]
        private float hideTimeout = 3.0f;
        #endregion Serialized Fields

        #region Private Fields
        private float timeoutTimer = 0.0f;
        private bool isInteractionEnabled = false;
        private bool cursorWasDisabledOnDown = false;
        private Vector3 lastPosition;
        private IMixedRealityController controller;

        /// <summary>
        /// The last mouse screen position
        /// </summary>
        private Vector3 screenPosition = Vector3.zero;

        /// <summary>
        /// The ray start position
        /// </summary>
        private Vector3 rayStart = Vector3.zero;

        /// <summary>
        /// The ray direction
        /// </summary>
        private Vector3 rayDirection = Vector3.forward;

        /// <summary>
        /// Is the mouse pointer currently disabled because of being inactive
        /// </summary>
        private bool isDisabled = true;
        #endregion Private Fields

        #region Properties
        /// <summary>
        /// Should the mouse cursor be hidden when no active input is received?
        /// </summary>
        public bool HideCursorWhenInactive
        {
            get { return hideCursorWhenInactive; }
            set { hideCursorWhenInactive = value; }
        }

        /// <summary>
        /// What is the movement threshold to reach before un-hiding mouse cursor?
        /// </summary>
        public float MovementThresholdToUnHide
        {
            get { return movementThresholdToUnHide; }
            set { movementThresholdToUnHide = value; }
        }

        /// <summary>
        /// How long should it take before the mouse cursor is hidden?
        /// </summary>
        public float HideTimeout
        {
            get { return hideTimeout; }
            set { hideTimeout = value; }
        }

        /// <summary>
        /// Is the mouse disable because it was inactive
        /// </summary>
        public bool IsInactiveAndDisabled => hideCursorWhenInactive && isDisabled;

        /// <inheritdoc />
        bool IMixedRealityMousePointer.HideCursorWhenInactive => hideCursorWhenInactive;

        /// <inheritdoc />
        float IMixedRealityMousePointer.MovementThresholdToUnHide => movementThresholdToUnHide;

        /// <inheritdoc />
        float IMixedRealityMousePointer.HideTimeout => hideTimeout;
        #endregion Properties

        #region Unity Functions
        protected override void Start()
        {
            base.Start();

            // Disable as needed
            isDisabled = DisableCursorOnStart;

            // Initialize Game Object
            if (gameObject != null)
            {
                gameObject.name = "Spatial Mouse Pointer";
            }
        }
        #endregion Unity Functions

        #region IMixedRealityPointer Implementation
        /// <inheritdoc />
        public override bool IsInteractionEnabled => isInteractionEnabled;

        /// <inheritdoc />
        public override IMixedRealityController Controller
        {
            get { return controller; }
            set
            {
                controller = value;
                if (controller != null)
                {
                    InputSourceParent = controller.InputSource;
                    isInteractionEnabled = true;
                }
                else
                {
                    isInteractionEnabled = false;
                }
            }
        }

        /// <summary>
        /// Get the position of the visible pointer
        /// </summary>
        public override Vector3 Position => lastPosition;

        /// <inheritdoc />
        public override void OnPreSceneQuery()
        {
            TryDisablingBasedOnTimer();
            bool visible = IsInteractionEnabled && !IsInactiveAndDisabled;
            BaseCursor?.SetVisibility(visible);
            UpdateSystemCursor(visible);

            Ray ray = new Ray(transform.position, transform.rotation * Vector3.forward);
            Rays[0].CopyRay(ray, PointerExtent);

            if (MixedRealityRaycaster.DebugEnabled)
            {
                Debug.DrawRay(ray.origin, ray.direction * PointerExtent, Color.red);
            }

            base.OnPreSceneQuery();
        }

        public override void OnPostSceneQuery()
        {
            if (this.Result.CurrentPointerTarget != null)
            {
                lastPosition = transform.position + (transform.forward * this.Result.Details.RayDistance);
            }
            else
            {
                lastPosition = transform.position + (transform.forward * DefaultPointerExtent);
            }

            base.OnPostSceneQuery();
        }
        #endregion IMixedRealityPointer Implementation

        #region IMixedRealitySourcePoseHandler Implementation
        /// <inheritdoc />
        public override void OnSourceDetected(SourceStateEventData eventData)
        {
            base.OnSourceDetected(eventData);
            if (eventData.SourceId == Controller?.InputSource?.SourceId)
            {
                isInteractionEnabled = true;
            }
        }

        /// <inheritdoc />
        public override void OnSourceLost(SourceStateEventData eventData)
        {
            base.OnSourceLost(eventData);
            if (eventData.SourceId == Controller?.InputSource?.SourceId)
            {
                isInteractionEnabled = false;
            }
        }
        #endregion IMixedRealitySourcePoseHandler Implementation

        #region IMixedRealityInputHandler Implementation
        /// <inheritdoc />
        public override void OnInputDown(InputEventData eventData)
        {
            cursorWasDisabledOnDown = IsInactiveAndDisabled;

            if (cursorWasDisabledOnDown)
            {
                BaseCursor?.SetVisibility(true);
                transform.rotation = CameraCache.Main.transform.rotation;
            }
            else
            {
                base.OnInputDown(eventData);
            }
        }

        /// <inheritdoc />
        public override void OnInputUp(InputEventData eventData)
        {
            if (!IsInactiveAndDisabled && !cursorWasDisabledOnDown)
            {
                base.OnInputUp(eventData);
            }
        }

        /// <inheritdoc />
        public override void OnInputChanged(InputEventData<Vector2> eventData)
        {
            base.OnInputChanged(eventData);
            if (eventData.SourceId == Controller?.InputSource.SourceId)
            {
                TryEnablingBaseOnMovement(eventData.InputData.x, eventData.InputData.y);
            }
        }
        #endregion IMixedRealityInputHandler Implementation

        #region Private Functions
        private void TryDisablingBasedOnTimer()
        {
            timeoutTimer += Time.unscaledDeltaTime;
            if (timeoutTimer >= hideTimeout)
            {
                timeoutTimer = 0.0f;
                BaseCursor?.SetVisibility(false);
                isDisabled = true;
            }
        }

        private void TryEnablingBaseOnMovement(float mouseX, float mouseY)
        {
            if (Mathf.Abs(mouseX) >= movementThresholdToUnHide ||
                Mathf.Abs(mouseY) >= movementThresholdToUnHide)
            {
                if (isDisabled)
                {
                    BaseCursor?.SetVisibility(true);    
                    transform.rotation = CameraCache.Main.transform.rotation;
                    isDisabled = false;
                }
            }

            if (!isDisabled)
            {
                timeoutTimer = 0.0f;
            }
        }

        private void UpdateSystemCursor(bool visible)
        {
            if ((systemCursorVisibilityChanges == SystemCursorVisibilityChanges.Never) ||
                (systemCursorVisibilityChanges == SystemCursorVisibilityChanges.PlayerOnly && Application.isEditor))
            {
                return;
            }

            Cursor.visible = visible;
        }
        #endregion Private Functions

        #region Class Enums
        /// <summary>
        /// When can the cursor's visibility change.
        /// </summary>
        public enum SystemCursorVisibilityChanges
        {
            Always,
            Never,
            PlayerOnly,
        }
        #endregion Class Enums
    }
}
