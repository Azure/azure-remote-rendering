// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// A struct holding player's head and joint poses.
    /// </summary>
    public class AvatarPose
    {
        private static Pose _invalidPose = new Pose(Vector3.negativeInfinity, Quaternion.identity);
        private Pose _headPose = _invalidPose;
        private AvatarHand _leftHand = new AvatarHand();
        private AvatarHand _rightHand = new AvatarHand();
        private AvatarJointDescription[] _serializeJoints = new AvatarJointDescription[0];

        /// <summary>
        /// Get the head pose, regardless of being valid.
        /// </summary>
        public Pose Head => _headPose;

        /// <summary>
        /// Get or set the hand joints that should be serialized with this avatar pose.
        /// </summary>
        public AvatarJointDescription[] SerializeJoints
        {
            get => _serializeJoints;

            set
            {
                _serializeJoints = value;

                // joints need to be serialized in order of the flag value.
                Array.Sort(_serializeJoints, (AvatarJointDescription a, AvatarJointDescription b) =>
                {
                    if (a.Flag == b.Flag)
                    {
                        return 0;
                    }
                    else if ((int)a.Flag > (int)b.Flag)
                    {
                        return 1;
                    }
                    else
                    {
                        return -1;
                    }
                });
            }
        }

        #region Public Functions
        /// <summary>
        /// Reset state so no data is stored.
        /// </summary>
        public void Reset()
        {
            _headPose = _invalidPose;
            _leftHand.Reset();
            _rightHand.Reset();
        }

        /// <summary>
        /// Try to get the head pose
        /// </summary>
        public bool TryGetHead(out Pose pose)
        {
            if (!_headPose.position.IsValidVector())
            {
                pose = Pose.identity;
                return false;
            }
            else
            {
                pose = _headPose;
                return true;
            }
        }

        /// <summary>
        /// Set head pose
        /// </summary>
        public void SetHead(Pose pose)
        {
            _headPose = pose;
        }

        /// <summary>
        /// Get the hand flags
        /// </summary>
        /// <param name="handedness"></param>
        /// <returns></returns>
        public AvatarPoseFlag HandFlags(Handedness handedness)
        {
            var hand = GetHand(handedness);
            if (hand == null)
            {
                return AvatarPoseFlag.None;
            }
            return hand.Flags;
        }

        /// <summary>
        /// Try to get the shared hand 
        /// </summary>
        public bool TryGetJoint(Handedness handedness, TrackedHandJoint joint, out Pose pose)
        {          
            var hand = GetHand(handedness);
            if (hand == null)
            {
                pose = Pose.identity;
                return false;
            }

            if (!hand.HasJointFlag(joint))
            {
                pose = Pose.identity;
                return false;
            }

            pose = hand.JointPoses[joint];
            return true;
        }

        /// <summary>
        /// Try to get the shared hand 
        /// </summary>
        public bool TryGetJoint(Handedness handedness, TrackedHandJoint joint, out Quaternion rotation)
        {
            var hand = GetHand(handedness);
            if (hand == null)
            {
                rotation = Quaternion.identity;
                return false;
            }

            if (!hand.HasJointFlag(joint))
            {
                rotation = Quaternion.identity;
                return false;
            }

            rotation = hand.JointPoses[joint].rotation;
            return true;
        }

        /// <summary>
        /// Set the shared hand joint position
        /// </summary>
        public void SetJoint(Handedness handedness, TrackedHandJoint joint, Quaternion rotation)
        {
            var hand = GetHand(handedness);
            if (hand != null)
            {
                hand.JointPoses[joint] = new Pose(Vector3.negativeInfinity, rotation);
                hand.SetJointFlag(ref rotation, joint);
            }
        }

        /// <summary>
        /// Set the shared hand joint position
        /// </summary>
        public void SetJoint(Handedness handedness, TrackedHandJoint joint, Pose pose)
        {
            var hand = GetHand(handedness);
            if (hand != null)
            {
                hand.JointPoses[joint] = pose;
                hand.SetJointFlag(ref pose, joint);
            }
        }

        /// <summary>
        /// Has a joint position
        /// </summary>
        public bool HasJoint(Handedness handedness, TrackedHandJoint joint)
        {
            var hand = GetHand(handedness);
            if (hand != null)
            {
                return GetHand(handedness).HasJointFlag(joint);
            }
            else
            {
                return false;
            }
        }
        #endregion Public Functions

        #region Private Functions
        private AvatarHand GetHand(Handedness handedness)
        {
            if (Handedness.Left == handedness)
            {
                return _leftHand;
            }
            else if (Handedness.Right == handedness)
            {
                return _rightHand;
            }
            else
            {
                return null;
            }
        }
        #endregion Private Functions
    }

    /// <summary>
    /// The sharing service player pose cache entry.
    /// </summary>
    public class AvatarPoseCacheEntry : AvatarPose, IObjectPoolEntry<AvatarPoseCacheEntry>
    {
        public event Action<AvatarPoseCacheEntry> OnDisposed;

        public void Dispose()
        {
            OnDisposed?.Invoke(this);
        }

        /// <summary>
        /// Invoke on check out
        /// </summary>
        public void OnCheckOut()
        {
            Reset();
        }

        /// <summary>
        /// Invoked on check in.
        /// </summary>
        public void OnCheckIn()
        {
        }
    }

    /// <summary>
    /// The sharing service player pose cache.
    /// </summary>
    public class AvatarPoseCache : ObjectPool<AvatarPoseCacheEntry>
    {
        public AvatarPoseCache(int size) : base(size)
        {
        }
    }

    /// <summary>
    /// Serialize a SharingServicePlayerPose object
    /// </summary>
    public class AvatarPoseSerializer : ISharingServiceSerializer
    {
        ISharingServiceBasicSerializer _serializer;
        AvatarPoseCache _cache = new AvatarPoseCache(120);

        public AvatarPoseSerializer(ISharingServiceBasicSerializer byteSerializer)
        {
            _serializer = byteSerializer;
        }

        /// <summary>
        /// Get the number of bytes needed to encode the given value.
        /// </summary>
        public int GetByteSize(object value)
        {
            if (!(value is AvatarPose))
            {
                return 0;
            }

            AvatarPose player = (AvatarPose)value;
            int bytes = _serializer.GetByteSize<Pose>();
            bytes += GetByteSizeForHand(player, Handedness.Left);
            bytes += GetByteSizeForHand(player, Handedness.Right);
            return bytes;
        }

        /// <summary>
        /// Get the number of bytes needed to encode the given hand information.
        /// </summary>
        private int GetByteSizeForHand(AvatarPose player, Handedness handedness)
        {
            // Bytes for change flag
            int bytes = _serializer.GetByteSize<int>();

            // Bytes for joints
            var serializeJoints = player.SerializeJoints;
            var serializeJointsCount = serializeJoints?.Length ?? 0;
            for (int i = 0; i < serializeJointsCount; i++)
            {
                var joint = serializeJoints[i];
                if (player.HasJoint(handedness, joint.Joint))
                {
                    if (joint.HasPose)
                    {
                        bytes += _serializer.GetByteSize<Pose>();
                    }
                    else
                    {
                        bytes += _serializer.GetByteSize<Quaternion>();
                    }
                }
            }

            return bytes;
        }

        /// <summary>
        /// Serialize the given value to the byte array
        /// </summary>
        public void Serialize(object value, byte[] target, ref int offset)
        {
            if (!(value is AvatarPose))
            {
                return;
            }

            AvatarPose player = (AvatarPose)value;

            var head = player.Head;
            _serializer.Serialize(ref head, target, ref offset);
            SerializeHand(player, Handedness.Left, target, ref offset);
            SerializeHand(player, Handedness.Right, target, ref offset);
        }

        /// <summary>
        /// Serialize the given value to the byte array
        /// </summary>
        private void SerializeHand(AvatarPose player, Handedness handedness, byte[] target, ref int offset)
        {
            // Serialize change flag
            _serializer.Serialize((int)player.HandFlags(handedness), target, ref offset);

            // Serialize joints
            var serializeJoints = player.SerializeJoints;
            var serializeJointsCount = serializeJoints?.Length ?? 0;
            for (int i = 0; i < serializeJointsCount; i++)
            {
                var joint = serializeJoints[i];
                if (joint.HasPose)
                {
                    if (player.TryGetJoint(handedness, joint.Joint, out Pose pose))
                    {
                        _serializer.Serialize(ref pose, target, ref offset);
                    }
                }
                else if (player.TryGetJoint(handedness, joint.Joint, out Quaternion rotation))
                {
                    _serializer.Serialize(ref rotation, target, ref offset);
                }
            }
        }

        /// <summary>
        /// Deserialize a value from a byte array
        /// </summary>
        public void Deserialize(out object value, byte[] source, ref int offset)
        {
            var player = _cache.CheckOut();

            try
            {
                Pose head;
                _serializer.Deserialize(out head, source, ref offset);
                player.SetHead(head);
                DeserializeHand(player, Handedness.Left, source, ref offset);
                DeserializeHand(player, Handedness.Right, source, ref offset);

                value = player;
            }
            catch (Exception ex)
            {
                player.Dispose();
                throw ex;
            }
        }

        /// <summary>
        /// Deserialize a hand from the byte array
        /// </summary>
        private void DeserializeHand(AvatarPose player, Handedness handedness, byte[] source, ref int offset)
        {
            // Deserialize the change change
            _serializer.Deserialize(out int rawFlags, source, ref offset);
            AvatarPoseFlag flags = (AvatarPoseFlag)rawFlags;

            // Deserialize hand joints
            foreach (AvatarPoseFlag flag in AvatarPoseFlagHelper.GetEnumerable())
            {
                if (flags.HasFlag(flag))
                {
                    var joint = AvatarHandDescription.GetJointDescription(flag);
                    if (joint.HasPose)
                    {
                        _serializer.Deserialize(out Pose pose, source, ref offset);
                        player.SetJoint(handedness, joint.Joint, pose);
                    }
                    else
                    {
                        _serializer.Deserialize(out Quaternion rotation, source, ref offset);
                        player.SetJoint(handedness, joint.Joint, rotation);
                    }
                }
            }
        }

        /// <summary>
        /// Convert object to string
        /// </summary>
        public string ToString(object value)
        {
            throw new InvalidOperationException("ToString is not supported");
        }

        /// <summary>
        /// Convert string to object
        /// </summary>
        public bool FromString(string value, out object result)
        {
            throw new InvalidOperationException("FromString is not supported");
        }
    }
}
