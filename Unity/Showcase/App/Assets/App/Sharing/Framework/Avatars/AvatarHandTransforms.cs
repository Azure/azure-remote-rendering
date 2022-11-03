// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    public class AvatarHandTransforms : MonoBehaviour
    {
        private Matrix4x4 _sourceToWorld = Matrix4x4.identity;
        private Dictionary<Transform, Transform> _ownedTransforms = null;
        private Dictionary<TrackedHandJoint, Transform> _pendingTransforms = null;
        private Dictionary<TrackedHandJoint, Transform> _transforms = 
            new Dictionary<TrackedHandJoint, Transform>();

        #region Serialized Fields
        [Header("General")]

        [SerializeField]
        [Tooltip("The handedness")]
        private Handedness handedness = Handedness.Left;

        /// <summary>
        /// The hand renderer.
        /// </summary>
        public Handedness Handedness
        {
            get => handedness;
            set => handedness = value;
        }

        [SerializeField]
        [Tooltip("Create copies of transforms")]
        private bool copyTransforms = false;

        /// <summary>
        /// Create copies of transforms
        /// </summary>
        public bool CopyTransforms
        {
            get => copyTransforms;
            set => copyTransforms = value;
        }

        [Header("Head Transform")]

        [SerializeField]
        [Tooltip("The head. This is only used during copy operation, so that the copied wrist is placed relative to the head. If null, parent transform is used.")]
        private Transform head = null;

        /// <summary>
        /// The head. This is only used during copy operation, so that the copied wrist is placed relative to the head. If null, parent transform is used.
        /// </summary>
        public Transform Head
        {
            get => head;
            set => head = value;
        }

        [Header("Joint Transforms")]

        [SerializeField]
        [Tooltip("The wrist root.")]
        private Transform wrist = null;

        /// <summary>
        /// Get or set the wrist root.
        /// </summary>
        public Transform Wrist
        {
            get => wrist;
            set => wrist = value;
        }

        [SerializeField]
        [Tooltip("The thumb root, at the metacarpal.")]
        private Transform thumbMetacarpal = null;

        /// <summary>
        /// The thumb root, at the metacarpal.
        /// </summary>
        public Transform ThumbMetacarpal
        {
            get => thumbMetacarpal;
            set => thumbMetacarpal = value;
        }

        [SerializeField]
        [Tooltip("The index finger root, at the knuckle.")]
        private Transform indexKnuckle = null;

        /// <summary>
        /// The index finger root, at the knuckle.
        /// </summary>
        public Transform IndexKnuckle
        {
            get => indexKnuckle;
            set => indexKnuckle = value;
        }

        [SerializeField]
        [Tooltip("The middle finger root, at the knuckle.")]
        private Transform middleKnuckle = null;

        /// <summary>
        /// The middle finger root, at the knuckle.
        /// </summary>
        public Transform MiddleKnuckle
        {
            get => middleKnuckle;
            set => middleKnuckle = value;
        }

        [SerializeField]
        [Tooltip("The ring finger root, at the knuckle.")]
        private Transform ringKnuckle = null;

        /// <summary>
        /// The ring finger root, at the knuckle.
        /// </summary>
        public Transform RingKnuckle
        {
            get => ringKnuckle;
            set => ringKnuckle = value;
        }

        [SerializeField]
        [Tooltip("The pinky finger root, at the knuckle.")]
        private Transform pinkyKnuckle = null;

        /// <summary>
        /// The pinky finger root, at the knuckle.
        /// </summary>
        public Transform PinkyKnuckle
        {
            get => pinkyKnuckle;
            set => pinkyKnuckle = value;
        }

        [Header("Joint Animator")]

        [SerializeField]
        [Tooltip("The animator used to extract joints from. If null the hand hierarchy is used instead.")]
        private Animator animator = null;

        /// <summary>
        /// The animator used to extract joint transforms from. If null the hand hierarchy is used instead.
        /// </summary>
        public Animator Animator
        {
            get => animator;
            set
            {
                if (animator != value)
                {
                    animator = value;
                    Initialize();
                }
            }
        }
        #endregion Serialized Fields

        #region Public Properties 
        public IReadOnlyDictionary<TrackedHandJoint, Transform> Transforms => _transforms;
        #endregion Public Properties

        #region Public Events
        public event Action<AvatarHandTransforms, IReadOnlyDictionary<TrackedHandJoint, Transform>> TransformsChanged;
        #endregion Public Events

        #region MonoBehavior Functions
        private void Start()
        {
            Initialize();
        }
        #endregion MonoBehavior Functions

        #region Public Functions
        public void FindTransforms()
        {
            Initialize();
        }
        #endregion Public Functions 

        #region Private Functions
        /// <summary>
        /// Initialize the transforms
        /// </summary>
        private void Initialize()
        {
            _pendingTransforms = new Dictionary<TrackedHandJoint, Transform>();
            if (animator != null)
            {
                InitializeJointTransformsFromAnimator();
            }
            else
            {
                InitializeJointTransformsFromHierarchy();
            }

            DestroyCopies();
            if (copyTransforms)
            {
                _pendingTransforms = CreateCopy(_pendingTransforms);
            }

            _transforms = _pendingTransforms;
            TransformsChanged?.Invoke(this, _transforms);

        }

        /// <summary>
        /// Initialize the joint transforms from hierarchy
        /// </summary>
        private void InitializeJointTransformsFromHierarchy()
        {
            // if wrist is copied, we need to know the relative root position so to place it.
            // we assume this parent is the destination head.
            if (transform.parent == null || head == null)
            {
                _sourceToWorld = Matrix4x4.identity;
            }
            else
            {
                _sourceToWorld = transform.parent.localToWorldMatrix * head.worldToLocalMatrix;
            }

            // primary hand transform
            InitializeJointTransform(AvatarHandDescription.Primary.Joint, wrist);

            // thumb transforms
            InitializeJointTransform(TrackedHandJoint.ThumbMetacarpalJoint, thumbMetacarpal);
            InitializeJointTransform(TrackedHandJoint.ThumbProximalJoint, RetrieveChildTransform(TrackedHandJoint.ThumbMetacarpalJoint));
            InitializeJointTransform(TrackedHandJoint.ThumbDistalJoint, RetrieveChildTransform(TrackedHandJoint.ThumbProximalJoint));
            InitializeJointTransform(TrackedHandJoint.ThumbTip, RetrieveChildTransform(TrackedHandJoint.ThumbDistalJoint));

            // index finger transforms
            InitializeJointTransform(TrackedHandJoint.IndexKnuckle, indexKnuckle);
            InitializeJointTransform(TrackedHandJoint.IndexMiddleJoint, RetrieveChildTransform(TrackedHandJoint.IndexKnuckle));
            InitializeJointTransform(TrackedHandJoint.IndexDistalJoint, RetrieveChildTransform(TrackedHandJoint.IndexMiddleJoint));
            InitializeJointTransform(TrackedHandJoint.IndexTip, RetrieveChildTransform(TrackedHandJoint.IndexDistalJoint));

            // middle finger transforms
            InitializeJointTransform(TrackedHandJoint.MiddleKnuckle, middleKnuckle);
            InitializeJointTransform(TrackedHandJoint.MiddleMiddleJoint, RetrieveChildTransform(TrackedHandJoint.MiddleKnuckle));
            InitializeJointTransform(TrackedHandJoint.MiddleDistalJoint, RetrieveChildTransform(TrackedHandJoint.MiddleMiddleJoint));
            InitializeJointTransform(TrackedHandJoint.MiddleTip, RetrieveChildTransform(TrackedHandJoint.MiddleDistalJoint));

            // ring finger transforms
            InitializeJointTransform(TrackedHandJoint.RingKnuckle, ringKnuckle);
            InitializeJointTransform(TrackedHandJoint.RingMiddleJoint, RetrieveChildTransform(TrackedHandJoint.RingKnuckle));
            InitializeJointTransform(TrackedHandJoint.RingDistalJoint, RetrieveChildTransform(TrackedHandJoint.RingMiddleJoint));
            InitializeJointTransform(TrackedHandJoint.RingTip, RetrieveChildTransform(TrackedHandJoint.RingDistalJoint));

            // pinky transforms
            InitializeJointTransform(TrackedHandJoint.PinkyKnuckle, pinkyKnuckle);
            InitializeJointTransform(TrackedHandJoint.PinkyMiddleJoint, RetrieveChildTransform(TrackedHandJoint.PinkyKnuckle));
            InitializeJointTransform(TrackedHandJoint.PinkyDistalJoint, RetrieveChildTransform(TrackedHandJoint.PinkyMiddleJoint));
            InitializeJointTransform(TrackedHandJoint.PinkyTip, RetrieveChildTransform(TrackedHandJoint.PinkyDistalJoint));
        }

        /// <summary>
        /// Initialize the joint transforms from a given animator
        /// </summary>
        private void InitializeJointTransformsFromAnimator()
        {
            if (animator == null)
            {
                return;
            }

            // if wrist is copied, we need to know the relative root position so to place it.
            // we assume this parent is the destination head, and the source head is defined in the animator
            var sourceRoot = animator.GetBoneTransform(HumanBodyBones.Head);
            if (sourceRoot == null || transform.parent == null)
            {
                _sourceToWorld = Matrix4x4.identity;
            }
            else
            {
                _sourceToWorld = transform.parent.localToWorldMatrix * sourceRoot.worldToLocalMatrix;
            }

            foreach (var joint in AvatarHandDescription.AllJoints)
            {
                if (joint.HasBone)
                {
                    Transform jointTransform = animator.GetBoneTransform(joint.Bone(handedness));
                    if (jointTransform != null)
                    {
                        InitializeJointTransform(joint.Joint, jointTransform);
                    }
                }
            }

            // The finger tips don't have bones, so search the hierarchy for these
            InitializeJointTransform(TrackedHandJoint.ThumbTip, RetrieveChildTransform(TrackedHandJoint.ThumbDistalJoint));
            InitializeJointTransform(TrackedHandJoint.IndexTip, RetrieveChildTransform(TrackedHandJoint.IndexDistalJoint));
            InitializeJointTransform(TrackedHandJoint.MiddleTip, RetrieveChildTransform(TrackedHandJoint.MiddleDistalJoint));
            InitializeJointTransform(TrackedHandJoint.RingTip, RetrieveChildTransform(TrackedHandJoint.RingDistalJoint));
            InitializeJointTransform(TrackedHandJoint.PinkyTip, RetrieveChildTransform(TrackedHandJoint.PinkyDistalJoint));
        }

        /// <summary>
        /// Add joint transform to the joint dictionary
        /// </summary>
        private void InitializeJointTransform(TrackedHandJoint joint, Transform jointTransform)
        {
            if (jointTransform != null)
            {
                _pendingTransforms[joint] = jointTransform;
            }
        }

        private void DestroyCopies()
        {
            if (_ownedTransforms != null)
            {
                foreach (var entry in _ownedTransforms)
                {
                    if (entry.Value != null)
                    {
                        Destroy(entry.Value.gameObject);
                    }
                }
                _ownedTransforms = null;
            }
        }

        private Dictionary<TrackedHandJoint, Transform> CreateCopy(Dictionary<TrackedHandJoint, Transform> originalTransforms)
        {
            _ownedTransforms = new Dictionary<Transform, Transform>();
            var result = new Dictionary<TrackedHandJoint, Transform>(originalTransforms.Count);
            foreach (var entry in originalTransforms)
            {
                result[entry.Key] = CreateCopy(entry.Value);
            }
            return result;
        }

        private Transform CreateCopy(Transform jointTransform)
        {
            Transform parentCopy;
            if (jointTransform.parent == null ||
                !_ownedTransforms.TryGetValue(jointTransform.parent, out parentCopy))
            {
                parentCopy = transform;
            }
            var copy = new GameObject($"{jointTransform.name} (copy)");
            copy.transform.SetParent(parentCopy);

            var pose = _sourceToWorld * jointTransform.localToWorldMatrix;
            copy.transform.SetPositionAndRotation(
                new Vector3(pose[0, 3], pose[1, 3], pose[2, 3]),
                pose.rotation);

            _ownedTransforms[jointTransform] = copy.transform;
            return copy.transform;
        }

        private Transform RetrieveChildTransform(TrackedHandJoint parentJoint)
        {
            _pendingTransforms.TryGetValue(parentJoint, out Transform jointTransform);

            if (jointTransform != null && jointTransform.childCount > 0)
            {
                return jointTransform.GetChild(0);
            }
            return null;
        }
        #endregion Private Functions
    }
}
