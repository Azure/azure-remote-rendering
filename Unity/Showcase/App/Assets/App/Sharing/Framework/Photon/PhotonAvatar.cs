// Copyright(c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    public class PhotonAvatar : PhotonAvatarBase
    {
        Matrix4x4 _originToWorld = Matrix4x4.identity;
        Pose _headTarget = new Pose(Vector3.negativeInfinity, Quaternion.identity);
        Quaternion _rootRotation = Quaternion.identity;

        #region Serialized Fields

        [Header("Body and Head Transforms")]

        [SerializeField]
        [Tooltip("The root or body transform.")]
        private Transform root = null;

        /// <summary>
        /// The root or body transform.
        /// </summary>
        public Transform Root
        {
            get => root;
            set => root = value;
        }

        [SerializeField]
        [Tooltip("The head transform.")]
        private Transform head = null;

        /// <summary>
        /// The head transform.
        /// </summary>
        public Transform Head
        {
            get => head;
            set => head = value;
        }

        [Header("Body and Head Smoothing")]

        [SerializeField]
        [Tooltip("The speed of the body/root rotation interpolation")]
        private float bodyRotationInterpolateLerpSpeed = 1.0f;

        /// <summary>
        /// The speed of the body/root rotation interpolation.
        /// </summary>
        public float BodyRotationInterpolateLerpSpeed
        {
            get => bodyRotationInterpolateLerpSpeed;
            set => bodyRotationInterpolateLerpSpeed = value;
        }

        [SerializeField]
        [Tooltip("The speed of the body/root position interpolation")]
        private float bodyPositionInterpolateLerpSpeed = 1.0f;

        /// <summary>
        /// The speed of the body/root position interpolation.
        /// </summary>
        public float BodyPositionInterpolateLerpSpeed
        {
            get => bodyPositionInterpolateLerpSpeed;
            set => bodyPositionInterpolateLerpSpeed = value;
        }

        [SerializeField]
        [Tooltip("The speed of the head rotation interpolation")]
        private float headRotationInterpolateLerpSpeed = 1.0f;

        /// <summary>
        /// The speed of the head rotation interpolation.
        /// </summary>
        public float HeadRotationInterpolateLerpSpeed
        {
            get => headRotationInterpolateLerpSpeed;
            set => headRotationInterpolateLerpSpeed = value;
        }

        [SerializeField]
        [Tooltip("The speed of the head position interpolation")]
        private float headPositionInterpolateLerpSpeed = 1.0f;

        /// <summary>
        /// The speed of the head position interpolation.
        /// </summary>
        public float HeadPositionInterpolateLerpSpeed
        {
            get => headPositionInterpolateLerpSpeed;
            set => headPositionInterpolateLerpSpeed = value;
        }

        [Header("Hand Components")]

        [SerializeField]
        [Tooltip("The left hand.")]
        private AvatarHandMovement leftHand = null;

        /// <summary>
        /// The left hand.
        /// </summary>
        public AvatarHandMovement LeftHand
        {
            get => leftHand;
            set => leftHand = value;
        }

        [SerializeField]
        [Tooltip("The right hand.")]
        private AvatarHandMovement rightHand = null;

        /// <summary>
        /// The right hand.
        /// </summary>
        public AvatarHandMovement RightHand
        {
            get => rightHand;
            set => rightHand = value;
        }

        [Header("Events")]

        [SerializeField]
        [Tooltip("Event raised when the sharing origin has moved.")]
        private UnityEvent originMoved = new UnityEvent();

        /// <summary>
        /// Event raised when the sharing origin has moved.
        /// </summary>
        public UnityEvent OriginMoved => originMoved;
        #endregion Serialized Fields

        #region MonoBehaviour Functions
        private void LateUpdate()
        {
            UpdateHeadAndBody();
        }
         
        private void OnDestroy()
        {
            PhotonAvatarCache.Remove(PhotonHelpers.UserIdFromString(Player.PlayerId));
        }
        #endregion MonoBehavior Functions

        #region Public Functions
        public void SetPose(AvatarPose pose, Matrix4x4 originToWorld)
        {
            if (root == null || !isActiveAndEnabled) 
            {
                return;
            }

            bool hasOriginChanged = originToWorld != _originToWorld;
            _originToWorld = originToWorld;

            bool success = pose.TryGetHead(out _headTarget);
            root.gameObject.SetActive(success);

            // put position and rotations into world space, from sharing origin
            _headTarget = new Pose(
                originToWorld.MultiplyPoint(_headTarget.position),
                originToWorld.rotation * _headTarget.rotation);

            // only rotate body around y-axis
            _rootRotation = Quaternion.LookRotation(
                new Vector3(_headTarget.forward.x, 0, _headTarget.forward.z), Vector3.up);

            // Set left hand position
            if (leftHand != null)
            {
                leftHand.SetPose(pose, originToWorld);
            }

            // Set right hand position
            if (rightHand != null)
            {
                rightHand.SetPose(pose, originToWorld);
            }

            // Notify listeners that origin has moved
            if (hasOriginChanged)
            {
                originMoved.Invoke();
            }
        }
        #endregion Public Functions

        #region Protected Methods
        protected override void Initialized()
        {
            PhotonAvatarCache.Add(PhotonHelpers.UserIdFromString(Player.PlayerId), this);
            UpdateMetadata();
        }
        #endregion Protected Methods

        #region Private Functions
        private void UpdateHeadAndBody()
        {
            if (head == null || !_headTarget.position.IsValidVector())
            {
                return;
            }

            Vector3 startHeadPosition = head.position;
            Quaternion startHeadRotation = head.rotation;

            // A root (aka body) transform is not required
            if (root != null)
            {
                Vector3 bodyPosition = Vector3.Lerp(
                    root.position,
                    _headTarget.position,
                    Mathf.Clamp01(Time.deltaTime * bodyPositionInterpolateLerpSpeed));

                Quaternion bodyRotation = Quaternion.Lerp(
                    root.rotation,
                    _rootRotation,
                    Mathf.Clamp01(Time.deltaTime * bodyRotationInterpolateLerpSpeed));

                // Only rotate body around y-axis
                root.SetPositionAndRotation(
                    bodyPosition,
                    bodyRotation);
            }

            Vector3 headPosition = Vector3.Lerp(
                startHeadPosition,
                _headTarget.position,
                    Mathf.Clamp01(Time.deltaTime * headPositionInterpolateLerpSpeed));

            Quaternion headRotation = Quaternion.Lerp(
                startHeadRotation,
                _headTarget.rotation,
                Mathf.Clamp01(Time.deltaTime * headRotationInterpolateLerpSpeed));

            // Set head position
            head.SetPositionAndRotation(headPosition, headRotation);
        }

        private void UpdateMetadata()
        {
            var metadata = GetComponent<AvatarComponentCollection>();
            if (metadata != null)
            {
                metadata.Initialize(Player);
            }
        }
        #endregion Private Functions
    }

    /// <summary>
    /// A cache so the PhotonAvatars component can quickly find avatars based on player id.
    /// </summary>
    public static class PhotonAvatarCache
    {
        private static Dictionary<int, PhotonAvatar> _cache = new Dictionary<int, PhotonAvatar>();

        public static void Add(int playerId, PhotonAvatar avatar)
        {
            lock (_cache)
            {
                _cache[playerId] = avatar;
            }
        }

        public static bool TryGet(int playerId, out PhotonAvatar avatar)
        {
            lock (_cache)
            {
                return _cache.TryGetValue(playerId, out avatar);
            }
        }

        public static bool Remove(int playerId)
        {
            lock (_cache)
            {
                return _cache.Remove(playerId);
            }
        }
    }

#if PHOTON_INSTALLED
    /// <summary>
    /// A class used to wrap behaviors that require Photon to be installed.
    /// </summary>
    public abstract class PhotonAvatarBase : MonoBehaviour, global::Photon.Pun.IPunInstantiateMagicCallback
    {
        ISharingService _service = null;
        string _pendingPlayerId = null;

        #region Public Properties
        public SharingServicePlayerData Player { get; private set; }
        #endregion Public Properties

        #region MonoBehaviour Functions
        private void OnDestroy()
        {
            RemoveListeners();
        }
        #endregion MonoBehaviour Functions

        #region IPunInstantiateMagicCallback 
        public void OnPhotonInstantiate(global::Photon.Pun.PhotonMessageInfo info)
        {
            _pendingPlayerId = PhotonHelpers.UserIdToString(info.photonView.CreatorActorNr);
            if (!TryInitializing())
            {
                AddListeners();
            }
        }
        #endregion IPunInstantiateMagicCallback

        #region Protected Methods
        protected abstract void Initialized();
        #endregion Protected Methods

        #region Private Methods
        private bool TryInitializing()
        {
            bool initialized = false;
            var players = AppServices.SharingService.Players;
            if (players != null)
            {
                foreach (var player in players)
                {
                    if (TryInitializing(player))
                    {
                        initialized = true;
                        break;
                    }
                }
            }

            return initialized;
        }

        private bool TryInitializing(ISharingServicePlayer player)
        {
            if (player.Data.PlayerId == _pendingPlayerId)
            {
                Initialize(player.Data);
                return true;
            }
            else
            {
                return false;
            }
        }

        private void Initialize(SharingServicePlayerData player)
        {
            Player = player;
            RemoveListeners();
            Initialized();
        }

        private void AddListeners()
        {
            if (_service != null)
            {
                return;
            }

            _service = AppServices.SharingService;
            if (_service != null)
            {
                _service.PlayerAdded += OnPlayerAdded;
            }
        }

        private void RemoveListeners()
        {
            if (_service != null)
            {
                _service.PlayerAdded -= OnPlayerAdded;
                _service = null;
            }
        }

        private void OnPlayerAdded(ISharingService sender, ISharingServicePlayer player)
        {
            TryInitializing(player);
        }
        #endregion Private Methods
    }
#else
    public abstract class PhotonAvatarBase : MonoBehaviour
    {
        #region Public Properties
        public SharingServicePlayerData Player { get; } = default;
        #endregion Public Properties

        #region Protected Methods
        protected abstract void Initialized();
        #endregion Protected Methods
    }
#endif
}
