// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    public class AvatarJointDescription
    {
        HumanBodyBones _leftBone;
        HumanBodyBones _rightBone;

        /// <summary>
        /// Get the joint value
        /// </summary>
        public TrackedHandJoint Joint { get; }

        /// <summary>
        /// Get the joint change flag
        /// </summary>
        public AvatarPoseFlag Flag { get; }

        /// <summary>
        /// Is this the primary hand pose
        /// </summary>
        public bool IsHand => Flag == AvatarPoseFlag.Hand;

        /// <summary>
        /// Get if the pose should be serialized for this joint
        /// </summary>
        public bool HasPose { get; }

        /// <summary>
        /// Get if joint rotates a bone
        /// </summary>
        public bool HasBone { get; }

        public AvatarJointDescription(
            TrackedHandJoint joint, 
            AvatarPoseFlag flag, 
            HumanBodyBones leftBone,
            HumanBodyBones rightBone,
            bool hasPose)
        {
            Joint = joint;
            Flag = flag;
            HasPose = hasPose;
            HasBone = true;

            _leftBone = leftBone;
            _rightBone = rightBone;
        }

        public AvatarJointDescription(
            TrackedHandJoint joint,
            AvatarPoseFlag flag,
            bool hasPose)
        {
            Joint = joint;
            Flag = flag;
            HasPose = hasPose;
            HasBone = false;
        }

        public HumanBodyBones Bone(Handedness hand)
        {
            return hand == Handedness.Left ? _leftBone : _rightBone;
        }
    }
}
