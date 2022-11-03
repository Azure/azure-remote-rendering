// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    public class AvatarHandMovement : MonoBehaviour
    {
        private Quaternion _reorientation;
        private AvatarJoint[] _joints = null;
        private Matrix4x4 _originToWorld;

        #region Serialized Fields
        [Header("Joint Transforms")]

        [SerializeField]
        [Tooltip("The hand joints transforms")]
        private AvatarHandTransforms transforms = null;

        /// <summary>
        /// The hand joint transforms
        /// </summary>
        public AvatarHandTransforms Transforms
        {
            get => transforms;
            set
            {
                if (transforms != value)
                {
                    RemoveTransformsHandlers();
                    transforms = value;
                    InitializeJoints();
                    AddTransformsHandlers();
                }
            }
        }

        [Header("Joint Serialization Settings")]

        [SerializeField]
        [Tooltip("The settings for what and how joints should be serialized.")]
        private AvatarSerialization avatarSerialization;

        /// <summary>
        /// The settings for what and how joints should be serialized
        /// </summary>
        public AvatarSerialization AvatarSerialization
        {
            get => avatarSerialization;
            set
            {
                if (avatarSerialization != value)
                {
                    RemoveTransformsHandlers();
                    avatarSerialization = value;
                    InitializeJoints();
                    AddTransformsHandlers();
                }
            }
        }

        [Header("Joint Smoothing")]

        [SerializeField]
        [Tooltip("The speed of the joint interpolation")]
        private float interpolateLerpSpeed = 1.0f;

        /// <summary>
        /// The speed of the joint interpolation.
        /// </summary>
        public float InterpolateLerpSpeed
        {
            get => interpolateLerpSpeed;
            set => interpolateLerpSpeed = value;
        }

        [Header("Advance Model Adjustments")]

        [SerializeField]
        [Tooltip("If non-zero, this vector and the modelPalmFacing vector " +
        "will be used to re-orient the Transform bones in the hand rig, to " +
        "compensate for bone axis discrepancies between tracked bones and model " +
        "bones.")]
        private Vector3 modelFingerPointing = new Vector3(0, 0, 0);

        /// <summary>
        /// If non-zero, this vector and the modelPalmFacing vector
        /// will be used to re-orient the Transform bones in the hand rig, to 
        /// compensate for bone axis discrepancies between tracked bones and model  
        /// bones.
        /// </summary>
        public Vector3 ModelFingerPointing
        {
            get => modelFingerPointing;
            set
            {
                modelFingerPointing = value;
                UpdateReorientation();
            }
        }

        [SerializeField]
        [Tooltip("If non-zero, this vector and the modelFingerPointing vector " +
          "will be used to re-orient the Transform bones in the hand rig, to " +
          "compensate for bone axis discrepancies between tracked bones and model " +
          "bones.")]
        private Vector3 modelPalmFacing = new Vector3(0, 0, 0);

        /// <summary>
        /// If non-zero, this vector and the modelFingerPointing vector
        /// will be used to re-orient the Transform bones in the hand rig, to
        /// compensate for bone axis discrepancies between tracked bones and model 
        /// bones.
        /// </summary>
        public Vector3 ModelPalmFacing
        {
            get => modelPalmFacing;
            set
            {
                modelPalmFacing = value;
                UpdateReorientation();
            }
        }
        #endregion Serialized Fields

        #region Public Properties
        public Transform Primary
        {
            get
            {
                if (transforms == null || transforms.Transforms == null)
                {
                    return null;
                }
                else
                {
                    transforms.Transforms.TryGetValue(
                        AvatarHandDescription.Primary.Joint,
                        out Transform primary);

                    return primary;
                }    
            }
        }

        /// <summary>
        /// The handedness.
        /// </summary>
        public Handedness Handedness
        {
            get => transforms?.Handedness ?? Handedness.Right;
        }
        #endregion Public Properties

        #region MonoBehaviours Functions
        private void OnValidate()
        {
            if (Application.isPlaying && Application.isEditor)
            {
                UpdateReorientation();
            }
        }

        private void Start()
        {
            AddTransformsHandlers();
            UpdateReorientation();
            InitializeJoints();
        }

        private void LateUpdate()
        {
            float interpolatationTime = Mathf.Clamp01(
                Time.deltaTime * interpolateLerpSpeed);

            for (int i = 0; i < _joints.Length; i++)
            {
                UpdateJoint(i, interpolatationTime);
            }
        }

        private void OnDestroy()
        {
            RemoveTransformsHandlers();
        }
        #endregion MonoBehavior Functions

        #region Public Functions
        public void SetPose(AvatarPose pose, Matrix4x4 originToWorld)
        {
            if (!isActiveAndEnabled || _joints == null)
            {
                return;
            }

            _originToWorld = originToWorld;
            for (int i = 0; i < _joints.Length; i++)
            {
                UpdateJointGoal(pose, i);
            }
        }

        public Transform GetTransform(TrackedHandJoint joint)
        {
            Transforms.Transforms.TryGetValue(joint, out Transform result);
            return result;
        }
        #endregion Public Functions

        #region Private Functions
        /// <summary>
        /// Add transforms handlers
        /// </summary>
        private void AddTransformsHandlers()
        {
            if (transforms != null)
            {
                transforms.TransformsChanged += OnTransformsChanged;
            }
        }

        /// <summary>
        /// Remove transforms handlers
        /// </summary>
        private void RemoveTransformsHandlers()
        {
            if (transforms != null)
            {
                transforms.TransformsChanged -= OnTransformsChanged;
            }
        }

        /// <summary>
        /// Initialize joint dictionary with their corresponding joint transforms
        /// </summary>
        private void InitializeJoints()
        {
            if (Transforms == null || AvatarSerialization == null)
            {
                return;
            }

            // find joint transforms
            var transforms = Transforms.Transforms;
            if (transforms == null)
            {
                return;
            }

            // find the hand description
            var handDescription = AvatarSerialization.HandDescription;
            if (handDescription == null)
            {
                return;
            }

            // create serializable joint data
            int index = 0;
            _joints = new AvatarJoint[handDescription.SerializableJoints.Length];
            foreach (var joint in handDescription.SerializableJoints)
            {
                if (transforms.TryGetValue(joint.Joint, out Transform jointTransform))
                {
                    var avatarJoint = AvatarJoint.Create(joint, jointTransform);
                    _joints[index++] = avatarJoint;
                }
            }

            if (index != _joints.Length)
            {
                Array.Resize(ref _joints, index);
            }
        }

        private void UpdateJointGoal(AvatarPose pose, int index)
        {
            AvatarJoint joint = _joints[index];
            if (joint.hasPose)
            {
                if (pose.TryGetJoint(Handedness, joint.joint, out Pose jointPose))
                {
                    jointPose.position = ToWorld(jointPose.position);
                    jointPose.rotation = ToWorld(jointPose.rotation) * _reorientation;
                    joint.Goal(jointPose);
                }
                else
                {
                    joint.Reset();
                }
            }
            else if (pose.TryGetJoint(Handedness, joint.joint, out Quaternion rotation))
            {
                joint.Goal(ToWorld(rotation) * _reorientation);
            }
            else
            {
                joint.Reset();
            }
        }

        private void UpdateJoint(int index, float interpolatationTime)
        {
            _joints[index].Update(interpolatationTime);
        }

        private Vector3 ToWorld(Vector3 position)
        {
            return _originToWorld.MultiplyPoint(position);
        }

        private Quaternion ToWorld(Quaternion rotation)
        {
            return _originToWorld.rotation * rotation;
        }
        private void UpdateReorientation()
        {
            if (modelFingerPointing == Vector3.zero || modelPalmFacing == Vector3.zero)
            {
                _reorientation = Quaternion.identity;
            }
            else
            {
                _reorientation = Quaternion.Inverse(Quaternion.LookRotation(modelFingerPointing, -modelPalmFacing));
            }
        }

        /// <summary>
        /// Handle transform changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="transforms"></param>
        private void OnTransformsChanged(AvatarHandTransforms sender, IReadOnlyDictionary<TrackedHandJoint, Transform> transforms)
        {
            InitializeJoints();
        }
        #endregion Private Functions

        #region Private Classes
        private class AvatarJoint
        {
            public readonly TrackedHandJoint joint;
            public readonly bool isHand;
            public readonly bool hasPose;
            public readonly bool isSerialized;
            public readonly Transform transform;
            public readonly Pose defaultLocalPose;

            private bool targetPoseLocal;
            private Pose targetPose;

            private AvatarJoint(TrackedHandJoint joint, bool isSerialized, bool isHand, bool hasPose, Transform transform)
            {
                this.joint = joint;
                this.isHand = isHand;
                this.hasPose = hasPose;
                this.transform = transform;
                this.isSerialized = isSerialized;
                this.targetPose = new Pose(Vector3.negativeInfinity, Quaternion.identity);
                this.defaultLocalPose = transform == null ?
                    Pose.identity :
                    new Pose(transform.localPosition, transform.localRotation);

            }

            public static AvatarJoint Create(AvatarJointDescription jointDescription, Transform transform)
            {
                return new AvatarJoint(
                    joint: jointDescription.Joint,
                    isSerialized: true,
                    isHand: false,
                    hasPose: jointDescription.HasPose,
                    transform: transform);
            }

            public void Goal(Quaternion rotation)
            {
                targetPoseLocal = false;
                targetPose.rotation = rotation;
            }

            public void Goal(Pose pose)
            {
                targetPoseLocal = false;
                targetPose = pose;
            }

            public void Reset()
            {
                targetPoseLocal = true;
                targetPose.position = defaultLocalPose.position;
                targetPose.rotation = defaultLocalPose.rotation;            
            }

            public void Update(float interpolatationTime)
            {
                if (transform == null || !targetPose.position.IsValidVector())
                {
                    return;
                }


                if (targetPoseLocal)
                {
                    UpdateLocal(interpolatationTime);
                }
                else
                {
                    UpdateGlobal(interpolatationTime);
                }
            }

            public void UpdateGlobal(float interpolatationTime)
            {
                Quaternion rotation = Quaternion.Lerp(
                    transform.rotation,
                    targetPose.rotation,
                    interpolatationTime);

                if (hasPose)
                {
                    Vector3 position = Vector3.Lerp(
                        transform.position,
                        targetPose.position,
                        interpolatationTime);

                    transform.SetPositionAndRotation(position, rotation);
                }
                else
                {
                    transform.rotation = rotation;
                }
            }

            public void UpdateLocal(float interpolatationTime)
            {
                transform.localRotation = Quaternion.Lerp(
                    transform.localRotation,
                    targetPose.rotation,
                    interpolatationTime);

                if (hasPose)
                {
                    transform.localPosition = Vector3.Lerp(
                        transform.localPosition,
                        targetPose.position,
                        interpolatationTime);
                }
            }
        }
        #endregion Private Classes
    }
}

