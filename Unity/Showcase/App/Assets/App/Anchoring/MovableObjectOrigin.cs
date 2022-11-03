// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// An origin transform of the application's movable objects.
    /// </summary>
    public class MovableObjectOrigin : MonoBehaviour
    {
        private MovableAnchor _anchor = null;

        #region MonoBehaviour Methods
        private void Awake()
        {
            _anchor = GetComponent<MovableAnchor>();
        }
        #endregion MonoBehaviour Methods

        #region Public Methods
        /// <summary>
        /// Convert the given global position and rotation so it's relative to this origin transform.
        /// </summary>
        public (Vector3 originPosition, Quaternion originRotation) WorldToLocal(Vector3 worldPosition, Quaternion worldRotation)
        {
            ForceUpdateAnchorPose();
            return (transform.InverseTransformPoint(worldPosition), Quaternion.Inverse(transform.rotation) * worldRotation);
        }

        /// <summary>
        /// Transform the given origin position and rotation so it's relative to the game's world space.
        /// </summary>
        public (Vector3 worldPosition, Quaternion worldRotation) LocalToWorld(Vector3 originPosition, Quaternion originRotation)
        {
            ForceUpdateAnchorPose();
            return (transform.TransformPoint(originPosition), transform.rotation * originRotation);
        }
        #endregion Public Methods

        #region Private Methods
        /// <summary>
        /// Ensure the origin has the most up-to-date pose, before calculate a pose relative to it.
        /// </summary>
        private void ForceUpdateAnchorPose()
        {
            if (_anchor != null)
            {
                _anchor.ForceUpdate();
            }
        }
        #endregion Private Methods
    }
}
