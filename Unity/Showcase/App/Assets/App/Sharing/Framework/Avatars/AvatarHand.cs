// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// A class holding player's head and joint poses.
    /// </summary>
    public class AvatarHand
    {
        /// <summary>
        /// Get the current joint poses.
        /// </summary>
        public Dictionary<TrackedHandJoint, Pose> JointPoses { get; } = new Dictionary<TrackedHandJoint, Pose>
        {
            { AvatarHandDescription.Primary.Joint, Pose.identity },
            { TrackedHandJoint.ThumbMetacarpalJoint, Pose.identity },
            { TrackedHandJoint.ThumbProximalJoint, Pose.identity },
            { TrackedHandJoint.ThumbDistalJoint, Pose.identity },
            { TrackedHandJoint.ThumbTip, Pose.identity },
            { TrackedHandJoint.IndexMetacarpal, Pose.identity },
            { TrackedHandJoint.IndexKnuckle, Pose.identity },
            { TrackedHandJoint.IndexMiddleJoint, Pose.identity },
            { TrackedHandJoint.IndexDistalJoint, Pose.identity },
            { TrackedHandJoint.IndexTip, Pose.identity },
            { TrackedHandJoint.MiddleMetacarpal, Pose.identity },
            { TrackedHandJoint.MiddleKnuckle, Pose.identity },
            { TrackedHandJoint.MiddleMiddleJoint, Pose.identity },
            { TrackedHandJoint.MiddleDistalJoint, Pose.identity },
            { TrackedHandJoint.MiddleTip, Pose.identity },
            { TrackedHandJoint.RingMetacarpal, Pose.identity },
            { TrackedHandJoint.RingKnuckle, Pose.identity },
            { TrackedHandJoint.RingMiddleJoint, Pose.identity },
            { TrackedHandJoint.RingDistalJoint, Pose.identity },
            { TrackedHandJoint.RingTip, Pose.identity },
            { TrackedHandJoint.PinkyMetacarpal, Pose.identity },
            { TrackedHandJoint.PinkyKnuckle, Pose.identity },
            { TrackedHandJoint.PinkyMiddleJoint, Pose.identity },
            { TrackedHandJoint.PinkyDistalJoint, Pose.identity },
            { TrackedHandJoint.PinkyTip, Pose.identity }
        };

        /// <summary>
        /// Get flags that represent which joint poses are valid.
        /// </summary>
        public AvatarPoseFlag Flags { get; private set; }

        /// <summary>
        /// Reset the change flags.
        /// </summary>
        public void Reset()
        {
            Flags = AvatarPoseFlag.None;
        }

        /// <summary>
        /// Get the change flag for the given joint.
        /// </summary>
        public AvatarPoseFlag GetFlag(TrackedHandJoint joint)
        {
            return AvatarHandDescription.GetFlag(joint);
        }

        /// <summary>
        /// Get if the joint has changed.
        /// </summary>
        public bool HasJointFlag(TrackedHandJoint joint)
        {
            return HasFlag(GetFlag(joint));
        }

        /// <summary>
        /// Get if the joint flag is set, indicated a joint change.
        /// </summary>
        private bool HasFlag(AvatarPoseFlag flag)
        {
            if (flag == AvatarPoseFlag.None)
            {
                return false;
            }
            else
            {
                return Flags.HasFlag(flag);
            }
        }

        /// <summary>
        /// Mark the joint has changed, passing in the joint pose. If the joint pose is invalid, the flag is not set.
        /// </summary>
        public void SetJointFlag(ref Pose pose, TrackedHandJoint joint)
        {
            SetFlag(ref pose, GetFlag(joint));
        }

        /// <summary>
        /// Mark the joint has changed, passing in the joint rotation. If the joint rotation is invalid, the flag is not set.
        /// </summary>
        public void SetJointFlag(ref Quaternion rotation, TrackedHandJoint joint)
        {
            SetFlag(ref rotation, GetFlag(joint));
        }

        /// <summary>
        /// Mark the joint has changed, passing in the joint pose. If the joint pose is invalid, the flag is not set.
        /// </summary>
        private void SetFlag(ref Pose pose, AvatarPoseFlag flag)
        {
            if (pose.position.IsValidVector() && pose.rotation.IsValidRotation())
            {
                Flags |= flag;
            }
            else if (AvatarPoseFlag.None != flag)
            {
                Flags &= ~flag;
            }
        }

        /// <summary>
        /// Mark the joint has changed, passing in the joint rotation. If the joint rotation is invalid, the flag is not set.
        /// </summary>
        private void SetFlag(ref Quaternion rotation, AvatarPoseFlag flag)
        {
            if (rotation.IsValidRotation())
            {
                Flags |= flag;
            }
            else if (AvatarPoseFlag.None != flag)
            {
                Flags &= ~flag;
            }
        }
    }
}
