// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// A helper that searches for joint transforms
    /// </summary>
    public class AvatarTransforms : MonoBehaviour
    {
        #region Serialized Fields
        [SerializeField]
        [Tooltip("The left hand joints")]
        private AvatarHandTransforms leftHand = null;

        /// <summary>
        /// The left hand joints
        /// </summary>
        public AvatarHandTransforms LeftHand
        {
            get => leftHand;
            set => leftHand = value;
        }

        [SerializeField]
        [Tooltip("The right hand joints")]
        private AvatarHandTransforms rightHand = null;

        /// <summary>
        /// The right hand joints
        /// </summary>
        public AvatarHandTransforms RightHand
        {
            get => rightHand;
            set => rightHand = value;
        }
        #endregion Serialized Fields
    }
}
