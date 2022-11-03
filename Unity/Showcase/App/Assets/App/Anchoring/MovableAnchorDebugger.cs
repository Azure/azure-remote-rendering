// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using TMPro;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// This helper will render debugging objects at the MovableAnchor and native anchor poses. This helper will also 
    /// draw a connecting line from the MovableAnchor object to the native anchor the MovableAnchor is consuming.
    /// </summary>
    public class MovableAnchorDebugger : MonoBehaviour
    {
        GameObject _anchorObject;
        GameObject _movableObject;
        LineRenderer _lineRenderer;
        TextMeshPro _anchorText;
        TextMeshPro _movableText;
        Vector3 _lastUsedMovablePosition = Vector3.negativeInfinity;
        Vector3 _lastUsedAnchorPosition = Vector3.negativeInfinity;
        string _lastUsedName;
        string _lastUsedAnchorId;
        bool _lastIsAnchorLocated;

        #region Serialized Fields
        [SerializeField]
        [Tooltip("The movable anchor to debug")]
        private MovableAnchor movableAnchor;

        /// <summary>
        /// The movable anchor to debug
        /// </summary>
        public MovableAnchor MovableAnchor
        {
            get => movableAnchor;
            set => movableAnchor = value;
        }

        [SerializeField]
        [Tooltip("The prefab to create and place at the anchor's pose")]
        private GameObject anchorPrefab = null;

        /// <summary>
        /// The prefab to create and place at the anchor's pose
        /// </summary>
        public GameObject AnchorPrefab
        {
            get => anchorPrefab;
            set => anchorPrefab = value;
        }

        [SerializeField]
        [Tooltip("The prefab to create and place at the movable's pose")]
        private GameObject movablePrefab = null;

        /// <summary>
        /// The prefab to create and place at the movable's pose
        /// </summary>
        public GameObject MovablePrefab
        {
            get => movablePrefab;
            set => movablePrefab = value;
        }

        [SerializeField]
        [Tooltip("The color to apply to the line connecting the MovableAnchor to it's current anchor, when the anchor is located.")]
        private Color anchorLocatedLineColor = Color.green;

        /// <summary>
        /// The color to apply to the line connecting the MovableAnchor to it's current anchor, when the anchor is located.
        /// </summary>
        public Color AnchorLocatedLineColor
        {
            get => anchorLocatedLineColor;
            set => anchorLocatedLineColor = value;
        }

        [SerializeField]
        [Tooltip("The color to apply to the line connecting the MovableAnchor to it's current anchor, when the anchor is not located.")]
        private Color anchorNotLocatedLineColor = Color.yellow;

        /// <summary>
        /// The color to apply to the line connecting the MovableAnchor to it's current anchor, when the anchor is not located.
        /// </summary>
        public Color AnchorNotLocatedLineColor
        {
            get => anchorLocatedLineColor;
            set => anchorLocatedLineColor = value;
        }

        [SerializeField]
        [Tooltip("The color to apply to the line connecting the MovableAnchor to it's old anchor, when there is no more anchor")]
        private Color noAnchorLineColor = Color.red;

        /// <summary>
        /// The color to apply to the line connecting the MovableAnchor to it's current anchor, when the anchor is not located.
        /// </summary>
        public Color NoAnchorLineColor
        {
            get => noAnchorLineColor;
            set => noAnchorLineColor = value;
        }
        #endregion Serialized Fields

        #region MonoBehavior Functions
        private void OnEnable()
        {
            if (movableAnchor == null)
            {
                movableAnchor = GetComponent<MovableAnchor>();
            }

            CreateDebugObject();
        }

        private void LateUpdate()
        {
            if (movableAnchor != null)
            {
                UpdatePoses();
                UpdateAnchorText();
                UpdateLine();
            }
        }

        private void OnDisable()
        {
            DestroyDebugObject();
        }
        #endregion MonoBehavior Functions

        #region Private Functions
        private void CreateDebugObject()
        {
            if (anchorPrefab == null || movablePrefab == null || movableAnchor == null)
            {
                return;
            }

            if (_anchorObject == null)
            {
                _anchorObject = Instantiate(anchorPrefab);
                _anchorObject.name = $"{movableAnchor.name} (anchor)";
                _anchorText = _anchorObject.GetComponentInChildren<TextMeshPro>();
                _lineRenderer = _anchorObject.EnsureComponent<LineRenderer>();
                _lineRenderer.useWorldSpace = true;
                _lineRenderer.positionCount = 0;
            }

            if (_movableObject == null && movableAnchor.Movable != null)
            {
                _movableObject = Instantiate(movablePrefab);
                _movableObject.name = $"{movableAnchor.name} (movable)";
                _movableText = _movableObject.GetComponentInChildren<TextMeshPro>();
            }

            UpdateAnchorText(forceUpdate: true);
        }

        private void DestroyDebugObject()
        {
            if (_anchorObject != null)
            {
                Destroy(_anchorObject);
                _anchorObject = null;
                _lineRenderer = null;
                _anchorText = null;
            }

            if (_movableObject != null)
            {
                Destroy(_movableObject);
                _movableObject = null;
                _movableText = null;
            }
        }

        private void UpdatePoses()
        {
            if (_anchorObject != null && movableAnchor.AnchorTransform != null)
            {
                _anchorObject.transform.position = movableAnchor.AnchorTransform.position;
                _anchorObject.transform.rotation = movableAnchor.AnchorTransform.rotation;
            }

            if (_movableObject != null && movableAnchor.Movable != null)
            {
                _movableObject.transform.position = movableAnchor.Movable.position;
                _movableObject.transform.rotation = movableAnchor.Movable.rotation;
            }
        }

        private void UpdateAnchorText(bool forceUpdate = false)
        {
            bool nameChanged = movableAnchor.name != _lastUsedName;
            bool anchorIdChanged = movableAnchor.AnchorId != _lastUsedAnchorId;
            bool isAnchorLocatedChanged = movableAnchor.IsAnchorLocated != _lastIsAnchorLocated;
            bool isMovablePositionChanged = movableAnchor.Movable != null && movableAnchor.Movable.position != _lastUsedMovablePosition;
            bool isAnchorPositionChanged = movableAnchor.AnchorTransform != null && movableAnchor.AnchorTransform.position != _lastUsedAnchorPosition;
            bool anchorTextChanged = (nameChanged || isAnchorPositionChanged || anchorIdChanged || isAnchorLocatedChanged || forceUpdate);
            bool movableTextChanged = (nameChanged || isMovablePositionChanged || forceUpdate);

            _lastUsedName = movableAnchor.name;
            _lastUsedAnchorId = movableAnchor.AnchorId;
            _lastIsAnchorLocated = movableAnchor.IsAnchorLocated;
            _lastUsedMovablePosition = movableAnchor.Movable != null ? movableAnchor.Movable.position : Vector3.negativeInfinity;
            _lastUsedAnchorPosition = movableAnchor.AnchorTransform != null ? movableAnchor.AnchorTransform.position : Vector3.negativeInfinity;

            if (_anchorText != null && anchorTextChanged)
            {
                string anchorIdText = _lastUsedAnchorId;
                if (string.IsNullOrEmpty(anchorIdText))
                {
                    anchorIdText = "no cloud anchor";
                }

                string anchorLocatedText;
                if (movableAnchor.IsAnchorLocated)
                {
                    anchorLocatedText = "located";
                }
                else
                {
                    anchorLocatedText = "not located";
                }

                _anchorText.text = $"{movableAnchor.name} ({anchorLocatedText}) @ {GetVector3String(ref _lastUsedAnchorPosition)}\n{anchorIdText}";
            }

            if (_movableText != null && movableTextChanged)
            {
                _movableText.text = $"{movableAnchor.name} (movable part) @ {GetVector3String(ref _lastUsedMovablePosition)}";
            }
        }

        private static string GetVector3String(ref Vector3 vector)
        {
            return vector.IsValidVector() ? vector.ToString() : "Unkown";
        }
        
        private void UpdateLine()
        {
            if (_lineRenderer != null)
            {
                UpdateLinePose();
                UpdateLineColor();
            }
        }

        private void UpdateLinePose()
        {
            Vector3 start = movableAnchor.transform.position;
            Vector3 end = start;

            if (movableAnchor.Movable != null)
            {
                end = movableAnchor.Movable.position;
            }

            float distanceSqr = (end - start).sqrMagnitude;

            if (distanceSqr == 0)
            {
                _lineRenderer.positionCount = 0;
            }
            else
            {
                _lineRenderer.positionCount = 2;
                _lineRenderer.SetPosition(0, start);
                _lineRenderer.SetPosition(1, end);
            }
        }

        private void UpdateLineColor()
        {
            Color color;
            if (!movableAnchor.HasAnchor)
            {
                color = noAnchorLineColor;
            }
            else if (!movableAnchor.IsAnchorLocated)
            {
                color = anchorNotLocatedLineColor;
            }
            else
            {
                color = anchorLocatedLineColor;
            }

            _lineRenderer.startColor = color;
            _lineRenderer.endColor = color;
        }
        #endregion Private Functions
    }
}
