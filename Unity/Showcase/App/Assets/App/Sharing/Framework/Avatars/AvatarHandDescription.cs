// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// This describes what hand data is serialized and send to other clients.
    /// </summary>
    public class AvatarHandDescription
    {
        private FingerSerializationType _fingerSerialization = FingerSerializationType.None;

        /// <summary>
        /// A cache that stores the all finger joints.
        /// </summary>
        private static AvatarJointDescription[] _allJoints = null;

        /// <summary>
        /// A cache that stores the serialized finger joints.
        /// </summary>
        private AvatarJointDescription[] _serializableJoints = null;

        /// <summary>
        /// Describes how to change a joint to its corresponding flag.
        /// </summary>
        private static Dictionary<TrackedHandJoint, AvatarPoseFlag> _jointToFlag = new Dictionary<TrackedHandJoint, AvatarPoseFlag>()
        {
            { TrackedHandJoint.Wrist, AvatarPoseFlag.Hand },
            { TrackedHandJoint.Palm, AvatarPoseFlag.Hand },
            { TrackedHandJoint.ThumbMetacarpalJoint, AvatarPoseFlag.None },
            { TrackedHandJoint.ThumbProximalJoint, AvatarPoseFlag.ThumbProximal },
            { TrackedHandJoint.ThumbDistalJoint, AvatarPoseFlag.ThumbDistal },
            { TrackedHandJoint.ThumbTip, AvatarPoseFlag.ThumbTip },
            { TrackedHandJoint.IndexMetacarpal, AvatarPoseFlag.None },
            { TrackedHandJoint.IndexKnuckle, AvatarPoseFlag.IndexKnuckle },
            { TrackedHandJoint.IndexMiddleJoint, AvatarPoseFlag.IndexMiddle },
            { TrackedHandJoint.IndexDistalJoint, AvatarPoseFlag.IndexDistal },
            { TrackedHandJoint.IndexTip, AvatarPoseFlag.IndexTip },
            { TrackedHandJoint.MiddleMetacarpal, AvatarPoseFlag.None },
            { TrackedHandJoint.MiddleKnuckle, AvatarPoseFlag.MiddleKnuckle },
            { TrackedHandJoint.MiddleMiddleJoint, AvatarPoseFlag.MiddleMiddle },
            { TrackedHandJoint.MiddleDistalJoint, AvatarPoseFlag.MiddleDistal },
            { TrackedHandJoint.MiddleTip, AvatarPoseFlag.MiddleTip },
            { TrackedHandJoint.RingMetacarpal, AvatarPoseFlag.None },
            { TrackedHandJoint.RingKnuckle, AvatarPoseFlag.RingKnuckle },
            { TrackedHandJoint.RingMiddleJoint, AvatarPoseFlag.RingMiddle },
            { TrackedHandJoint.RingDistalJoint, AvatarPoseFlag.RingDistal },
            { TrackedHandJoint.RingTip, AvatarPoseFlag.RingTip },
            { TrackedHandJoint.PinkyMetacarpal, AvatarPoseFlag.None },
            { TrackedHandJoint.PinkyKnuckle, AvatarPoseFlag.PinkyKnuckle },
            { TrackedHandJoint.PinkyMiddleJoint, AvatarPoseFlag.PinkyMiddle },
            { TrackedHandJoint.PinkyDistalJoint, AvatarPoseFlag.PinkyDistal },
            { TrackedHandJoint.PinkyTip, AvatarPoseFlag.PinkyTip }
        };

        /// <summary>
        /// Describes how to change a flag to its corresponding join.
        /// </summary>
        private static Dictionary<AvatarPoseFlag, TrackedHandJoint> _flagToJoint = new Dictionary<AvatarPoseFlag, TrackedHandJoint>();

        /// <summary>
        /// Describes how to change a joint enum to a description.
        /// </summary>
        private static Dictionary<TrackedHandJoint, AvatarJointDescription> _jointToDescription = new Dictionary<TrackedHandJoint, AvatarJointDescription>();

        /// <summary>
        /// Describes the Unity bone type of left hand joints.
        /// </summary>
        private static Dictionary<TrackedHandJoint, HumanBodyBones> _leftBones = new Dictionary<TrackedHandJoint, HumanBodyBones>()
        {
            { TrackedHandJoint.Wrist, HumanBodyBones.LeftHand },
            { TrackedHandJoint.Palm, HumanBodyBones.LeftHand },
            { TrackedHandJoint.ThumbMetacarpalJoint, HumanBodyBones.LeftThumbProximal },
            { TrackedHandJoint.ThumbProximalJoint, HumanBodyBones.LeftThumbIntermediate },
            { TrackedHandJoint.ThumbDistalJoint, HumanBodyBones.LeftThumbDistal },
            { TrackedHandJoint.IndexKnuckle, HumanBodyBones.LeftIndexProximal },
            { TrackedHandJoint.IndexMiddleJoint, HumanBodyBones.LeftIndexIntermediate },
            { TrackedHandJoint.IndexDistalJoint, HumanBodyBones.LeftIndexDistal },
            { TrackedHandJoint.MiddleKnuckle, HumanBodyBones.LeftMiddleProximal },
            { TrackedHandJoint.MiddleMiddleJoint, HumanBodyBones.LeftMiddleIntermediate },
            { TrackedHandJoint.MiddleDistalJoint, HumanBodyBones.LeftMiddleDistal },
            { TrackedHandJoint.RingKnuckle, HumanBodyBones.LeftRingProximal },
            { TrackedHandJoint.RingMiddleJoint, HumanBodyBones.LeftRingIntermediate },
            { TrackedHandJoint.RingDistalJoint, HumanBodyBones.LeftRingDistal },
            { TrackedHandJoint.PinkyKnuckle, HumanBodyBones.LeftLittleProximal },
            { TrackedHandJoint.PinkyMiddleJoint, HumanBodyBones.LeftLittleIntermediate },
            { TrackedHandJoint.PinkyDistalJoint, HumanBodyBones.LeftLittleDistal }
        };

        /// <summary>
        /// Describes the Unity bone type of left hand joints.
        /// </summary>
        private static Dictionary<TrackedHandJoint, HumanBodyBones> _rightBones = new Dictionary<TrackedHandJoint, HumanBodyBones>()
        {
            { TrackedHandJoint.Wrist, HumanBodyBones.RightHand },
            { TrackedHandJoint.Palm, HumanBodyBones.RightHand },
            { TrackedHandJoint.ThumbMetacarpalJoint, HumanBodyBones.RightThumbProximal },
            { TrackedHandJoint.ThumbProximalJoint, HumanBodyBones.RightThumbIntermediate },
            { TrackedHandJoint.ThumbDistalJoint, HumanBodyBones.RightThumbDistal },
            { TrackedHandJoint.IndexKnuckle, HumanBodyBones.RightIndexProximal },
            { TrackedHandJoint.IndexMiddleJoint, HumanBodyBones.RightIndexIntermediate },
            { TrackedHandJoint.IndexDistalJoint, HumanBodyBones.RightIndexDistal },
            { TrackedHandJoint.MiddleKnuckle, HumanBodyBones.RightMiddleProximal },
            { TrackedHandJoint.MiddleMiddleJoint, HumanBodyBones.RightMiddleIntermediate },
            { TrackedHandJoint.MiddleDistalJoint, HumanBodyBones.RightMiddleDistal },
            { TrackedHandJoint.RingKnuckle, HumanBodyBones.RightRingProximal },
            { TrackedHandJoint.RingMiddleJoint, HumanBodyBones.RightRingIntermediate },
            { TrackedHandJoint.RingDistalJoint, HumanBodyBones.RightRingDistal },
            { TrackedHandJoint.PinkyKnuckle, HumanBodyBones.RightLittleProximal },
            { TrackedHandJoint.PinkyMiddleJoint, HumanBodyBones.RightLittleIntermediate },
            { TrackedHandJoint.PinkyDistalJoint, HumanBodyBones.RightLittleDistal }
        };

        /// <summary>
        /// The finger tips of a hand
        /// </summary>
        private static HashSet<TrackedHandJoint> _fingerTips = new HashSet<TrackedHandJoint>()
        {
            TrackedHandJoint.ThumbTip,
            TrackedHandJoint.IndexTip,
            TrackedHandJoint.MiddleTip,
            TrackedHandJoint.RingTip,
            TrackedHandJoint.PinkyTip,
        };

        /// <summary>
        /// The finger joints of a hand
        /// </summary>
        private static Dictionary<AvatarFinger, TrackedHandJoint[]> _fingerJoints = new Dictionary<AvatarFinger, TrackedHandJoint[]>()
        {
            { AvatarFinger.Thumb, new TrackedHandJoint[] { TrackedHandJoint.ThumbMetacarpalJoint, TrackedHandJoint.ThumbProximalJoint, TrackedHandJoint.ThumbDistalJoint, TrackedHandJoint.ThumbTip } },
            { AvatarFinger.Index, new TrackedHandJoint[] { TrackedHandJoint.IndexKnuckle, TrackedHandJoint.IndexMiddleJoint, TrackedHandJoint.IndexDistalJoint, TrackedHandJoint.IndexTip } },
            { AvatarFinger.Middle, new TrackedHandJoint[] { TrackedHandJoint.MiddleKnuckle, TrackedHandJoint.MiddleMiddleJoint, TrackedHandJoint.MiddleDistalJoint, TrackedHandJoint.MiddleTip } },
            { AvatarFinger.Ring, new TrackedHandJoint[] { TrackedHandJoint.RingKnuckle, TrackedHandJoint.RingMiddleJoint, TrackedHandJoint.RingDistalJoint, TrackedHandJoint.RingTip } },
            { AvatarFinger.Pinky, new TrackedHandJoint[] { TrackedHandJoint.PinkyKnuckle, TrackedHandJoint.PinkyMiddleJoint, TrackedHandJoint.PinkyDistalJoint, TrackedHandJoint.PinkyTip } },
        };

        static AvatarHandDescription()
        {
            foreach (var entry in _jointToFlag)
            {
                if (!_flagToJoint.ContainsKey(entry.Value))
                {
                    _flagToJoint[entry.Value] = entry.Key;
                }
            }
        }

        /// <summary>
        /// Get or set if finger tips should be serialized
        /// </summary>
        public FingerSerializationType FingerSerializationType
        {
            get => _fingerSerialization;

            set
            {
                if (_fingerSerialization != value)
                {
                    _fingerSerialization = value;
                    _serializableJoints = null;
                }
            }
        }

        /// <summary>
        /// Get all hand joints, even those that aren't serialized
        /// </summary>
        public static AvatarJointDescription[] AllJoints
        {
            get
            {
                if (_allJoints == null)
                {
                    var jointToDescription = new Dictionary<TrackedHandJoint, AvatarJointDescription>(_jointToFlag.Count);

                    // Add hand joint first
                    jointToDescription[Primary.Joint] = Primary;

                    foreach (var entry in _jointToFlag)
                    {
                        var joint = entry.Key;
                        var flag = entry.Value;

                        // Can't use joints with a None flag, and the Hand joint was already added
                        if (flag == AvatarPoseFlag.Hand)
                        {
                            continue;
                        }

                        AvatarJointDescription description;
                        if (_fingerTips.Contains(joint))
                        {
                            description = new AvatarJointDescription(
                                joint,
                                flag,
                                hasPose: true);
                        }
                        else
                        {
                            if (_leftBones.TryGetValue(joint, out var leftBone) &&
                                _rightBones.TryGetValue(joint, out var rightBone))
                            {
                                description = new AvatarJointDescription(
                                    joint,
                                    flag,
                                    leftBone,
                                    rightBone,
                                    hasPose: false);
                            }
                            else
                            {
                                description = new AvatarJointDescription(
                                    joint,
                                    flag,
                                    hasPose: false);
                            }
                        }

                        jointToDescription[description.Joint] = description;
                    }

                    _allJoints = jointToDescription.Values.ToArray();
                    _jointToDescription = jointToDescription;

                }

                return _allJoints;
            }
        }

        /// <summary>
        /// Get the joint description of the primary hand joint that is serialized and sent to other clients.
        /// </summary>
        /// <remarks>
        /// If this is changed, you'll likely have to update your avatar model.
        /// </remarks>
        public static AvatarJointDescription Primary { get; } = new AvatarJointDescription(
            TrackedHandJoint.Wrist,
            _jointToFlag[TrackedHandJoint.Wrist],
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightHand,
            hasPose: true);

        /// <summary>
        /// Get the joint description of the primary hand joint that is serialized and sent to other clients.
        /// </summary>
        /// <remarks>
        /// If this is changed, you'll likely have to update your avatar model.
        /// </remarks>
        public AvatarJointDescription SerializableJoint => Primary;

        /// <summary>
        /// Get the descriptions of the joints that are serialized and sent to other clients.
        /// </summary>
        public AvatarJointDescription[] SerializableJoints
        {
            get
            {
                if (_serializableJoints == null)
                {
                    var allJoints = AllJoints;
                    var serializableJoints = new List<AvatarJointDescription>(allJoints.Length);
                    foreach (var entry in allJoints)
                    {
                        // Can't serialize something without a change flag
                        if (entry.Flag == AvatarPoseFlag.None)
                        {
                            continue;
                        }

                        bool fingerTip = _fingerTips.Contains(entry.Joint);
                        if ((entry.IsHand) ||
                            (_fingerSerialization == FingerSerializationType.FingerTips && fingerTip) || 
                            (_fingerSerialization == FingerSerializationType.JointRotations && !fingerTip))
                        {
                            serializableJoints.Add(entry);
                        }
                    }
                    _serializableJoints = serializableJoints.ToArray();
                }
                return _serializableJoints;
            }
        }

        /// <summary>
        /// Get the joint's change flag.
        /// </summary>
        public static AvatarPoseFlag GetFlag(TrackedHandJoint joint)
        {
            return _jointToFlag[joint];
        }

        /// <summary>
        /// Get the flag's joint description.
        /// </summary>
        public static AvatarJointDescription GetJointDescription(AvatarPoseFlag flag)
        {
            return _jointToDescription[_flagToJoint[flag]];
        }

        /// <summary>
        /// Get the finger joints.
        /// </summary>
        public static TrackedHandJoint[] GetJoints(AvatarFinger finger)
        {
            return _fingerJoints[finger];
        }
    }

    /// <summary>
    /// Describes the finger serialization.
    /// </summary>
    public enum FingerSerializationType
    {
        [Tooltip("Serialization is not defined.")]
        Unknown = 0,

        [Tooltip("No finger data is serialized.")]
        None = 1,

        [Tooltip("Only the finger tips will be serialized.")]
        FingerTips = 2,

        [Tooltip("Only the finger joints will be serialized.")]
        JointRotations = 3
    }
}
