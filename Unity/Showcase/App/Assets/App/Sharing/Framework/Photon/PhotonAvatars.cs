// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if PHOTON_INSTALLED
using ExitGames.Client.Photon;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    public class PhotonAvatars :
        IOnEventCallback,
        IDisposable
    {
        private Transform _origin;
        private Transform _head;
        private Matrix4x4 _worldToOrigin = Matrix4x4.identity;
        private Matrix4x4 _originToWorld = Matrix4x4.identity;
        private PhotonParticipants _participants = null;
        private AvatarPose _playerPose = new AvatarPose();
        private LogHelper<PhotonMessages> _logger = new LogHelper<PhotonMessages>();
        private ConcurrentQueue<Operation> _operations = new ConcurrentQueue<Operation>();
        private AvatarJointDescription[] _serializableJoints = null;
        private SharingServiceProfile _profileSettings = null;
        private PhotonAudioDetector _audioDetector = null;
        private PhotonPlayerColor _playerColor = null;
        private float _nextPlayerUpdate = 0;

        #region Constructors
        private PhotonAvatars(
            SharingServiceProfile profileSettings,
            PhotonComponents components,
            PhotonParticipants participants, 
            PhotonProperties properties,
            Transform origin)
        {
            _profileSettings = profileSettings;
            _origin = origin;
            _head = CameraCache.Main.transform;
            _playerPose.SerializeJoints = _serializableJoints = AvatarSerialization.ExtractSerializableJoints(_profileSettings.PhotonAvatarPrefab);
            _logger.Verbose = profileSettings.VerboseLogging ? LogHelperState.Always : LogHelperState.Default;
            _participants = participants ?? throw new ArgumentNullException("Participants can't be null");
            _participants.PlayerRemoved += OnPlayerRemoved;
            _audioDetector = new PhotonAudioDetector(components, properties)
            {
                VerboseLogging = profileSettings.VerboseLogging
            };
            _playerColor = new PhotonPlayerColor(participants.LocalParticipant, properties)
            {
                PrimaryColors = profileSettings.PhotonPlayerColors
            };

            CreateAvatar();
            PhotonNetwork.AddCallbackTarget(this);
        }
        #endregion Constructors

        #region Public Properties
        /// <summary>
        /// The time, in seconds, between player updates
        /// </summary>
        public float UpdatePlayerDelay { get; set; } = 1.0f / 10.0f;

        /// <summary>
        /// The origin of all avatars
        /// </summary>
        public Transform Origin
        {
            get => _origin;

            set
            {
                if (_origin != value)
                {
                    _origin = value;

                    if (_origin == null)
                    {
                        _worldToOrigin = Matrix4x4.identity;
                        _originToWorld = Matrix4x4.identity;
                    }
                    else
                    {
                        _worldToOrigin = value.worldToLocalMatrix;
                        _originToWorld = value.localToWorldMatrix;
                    }
                }
            }
        }
        #endregion Public Properties

        #region Public Functions
        /// <summary>
        /// Initialize data transport.
        /// </summary>
        public static PhotonAvatars CreateFromParticipants(
            SharingServiceProfile profileSettings,
            PhotonComponents components,
            PhotonParticipants participants,
            PhotonProperties properties,
            Transform origin)
        {
            var result = new PhotonAvatars(profileSettings, components, participants, properties, origin);
            return result;
        }

        /// <summary>
        /// Release resources.
        /// </summary>
        public void Dispose()
        { 
            PhotonNetwork.RemoveCallbackTarget(this);
            _participants.PlayerRemoved -= OnPlayerRemoved;

            if (_operations.TryDequeue(out Operation operation))
            {
                operation.Dispose();
            }
        }

        /// <summary>
        /// Update with avatars
        /// </summary>
        public void Update()
        {
            while (_operations.TryDequeue(out Operation operation))
            {
                try
                {
                    switch (operation.type)
                    {
                        case OperationType.Update:
                            UpdateAvatar(operation.player, operation.pose);
                            break;
                    }
                }
                finally
                {
                    operation.Dispose();
                }
            }

            _audioDetector.Update();
        }

        /// <summary>
        /// Send users pose
        /// </summary>
        public void LateUpdate()
        {
            if (Time.time >= _nextPlayerUpdate)
            {
                // The origin can be anchored and updating every frame
                if (_origin != null)
                {
                    _worldToOrigin = _origin.worldToLocalMatrix;
                    _originToWorld = _origin.localToWorldMatrix;
                }

                UpdatePlayer();
                SendPlayer();

                _nextPlayerUpdate = Time.time + UpdatePlayerDelay;
            }
        }
        #endregion Public Functions

        #region IOnEventCallback
        /// <summary>
        /// Called for any incoming events.
        /// </summary>
        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code == (byte)PhotonEventTypes.PlayerPoseEvent)
            {
                if (_participants.TryFind(photonEvent.Sender, out PhotonParticipant player) &&
                    photonEvent.CustomData is AvatarPose)
                {
                    _operations.Enqueue(new Operation()
                    {
                        type = OperationType.Update,
                        player = player,
                        pose = (AvatarPose)(photonEvent.CustomData)
                    });
                }
            }
        }
        #endregion

        #region Private Methods  
        /// <summary>
        /// Create avatar for self
        /// </summary>
        private void CreateAvatar()
        {
            if (_profileSettings.PhotonAvatarPrefab != null)
            {
                PhotonNetwork.Instantiate(
                    _profileSettings.PhotonAvatarPrefab.name,
                    Vector3.zero,
                    Quaternion.identity);
            }
        }

        /// <summary>
        /// Try deleting the avatar.
        /// </summary>
        private void DeleteAvatar(PhotonParticipant player)
        {
            if (PhotonAvatarCache.TryGet(player.Inner.ActorNumber, out PhotonAvatar avatar) && avatar != null)
            {
                PhotonViewAction.Create(avatar.gameObject, _participants.LocalParticipant, (view) =>
                {
                    PhotonNetwork.Destroy(view);
                });
            }
        }

        /// <summary>
        /// Update the avatar pose.
        /// </summary>
        private void UpdateAvatar(PhotonParticipant player, AvatarPose pose)
        {
            UpdateAvatar(player.ActorNumber, pose);
        }

        /// <summary>
        /// Update the avatar pose.
        /// </summary>
        private void UpdateAvatar(int actorNumber, AvatarPose pose)
        {
            if (PhotonAvatarCache.TryGet(actorNumber, out PhotonAvatar avatar) && avatar != null)
            {
                avatar.SetPose(pose, _originToWorld);
            }
        }

        /// <summary>
        /// Send a new message with current player updates
        /// </summary>
        private bool SendPlayer()
        {
            UpdateAvatar(_participants.LocalActorNumber, _playerPose);

            var options = RaiseEventOptions.Default;
            return PhotonNetwork.RaiseEvent(
                (byte)PhotonEventTypes.PlayerPoseEvent,
                _playerPose, 
                options,
                SendOptions.SendUnreliable);
        }

        /// <summary>
        /// Update the currnet player poses
        /// </summary>
        private void UpdatePlayer()
        {
            _playerPose.Reset();

            if (_head != null)
            {
                _playerPose.SetHead(ToPose(ToOrigin(_head.localToWorldMatrix)));
                UpdatePlayerHand(Handedness.Left);
                UpdatePlayerHand(Handedness.Right);
            }
        }

        private void UpdatePlayerHand(Handedness handedness)
        {            
            IMixedRealityHand hand = HandJointUtils.FindHand<IMixedRealityHand>(handedness);
            if (hand != null)
            {
                int length = _serializableJoints.Length;
                for (int i = 0; i < length; i++)
                {
                    var joint = _serializableJoints[i];

                    // break out of the loop if primary hand joint is not found.
                    if (!UpdatePlayerJoint(hand, joint) &&  joint.IsHand)
                    {
                        break;
                    }
                }
            }
        }

        private bool UpdatePlayerJoint(IMixedRealityHand hand, AvatarJointDescription joint)
        {
            if (hand.TryGetJoint(joint.Joint, out MixedRealityPose jointPose))
            {
                if (joint.HasPose)
                {
                    _playerPose.SetJoint(hand.ControllerHandedness, joint.Joint, ToPose(ToOrigin(ToMatrix(jointPose))));
                }
                else
                {
                    _playerPose.SetJoint(hand.ControllerHandedness, joint.Joint, ToOrigin(jointPose.Rotation));
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Convert aMixedRealityPose transform to a Matrix4x4 transform.
        /// The resulting matrix has the position and rotation of the input, and unit scale.
        /// </summary>
        public static Matrix4x4 ToMatrix(MixedRealityPose pose)
        {
            return Matrix4x4.TRS(pose.Position, pose.Rotation.normalized, Vector3.one);
        }

        /// <summary>
        /// Transform the world pose to be relative to the origin.
        /// </summary>
        private Matrix4x4 ToOrigin(Matrix4x4 pose)
        {
            return _worldToOrigin * pose;
        }

        /// <summary>
        /// Transform the world rotation to be relative to the origin.
        /// </summary>
        private Quaternion ToOrigin(Quaternion rotation)
        {
            return _worldToOrigin.rotation * rotation;
        }

        /// <summary>
        /// Convert matrix to a pose struct.
        /// </summary>
        private static Pose ToPose(Matrix4x4 pose)
        {
            return new Pose(
                new Vector3(pose[0, 3], pose[1, 3], pose[2, 3]),
                pose.rotation);
        }

        /// <summary>
        /// Handle player being removed
        /// </summary>
        private void OnPlayerRemoved(PhotonParticipants sender, PhotonParticipant player)
        {
            DeleteAvatar(player);            
        }
        #endregion Private Methods

        #region Logging Methods
        /// <summary>
        /// Log a message if verbose logging is enabled.
        /// </summary>
        private void LogVerbose(string message)
        {
            _logger.LogVerbose(message);
        }

        /// <summary>
        /// Log a message if verbose logging is enabled. 
        /// </summary>
        private void LogVerbose(string messageFormat, params object[] args)
        {
            _logger.LogVerbose(messageFormat, args);
        }

        /// <summary>
        /// Log a message if information logging is enabled.
        /// </summary>
        private void LogInformation(string message)
        {
            _logger.LogInformation(message);
        }

        /// <summary>
        /// Log a message if information logging is enabled. 
        /// </summary>
        private void LogInformation(string messageFormat, params object[] args)
        {
            _logger.LogInformation(messageFormat, args);
        }

        /// <summary>
        /// Log a message if warning logging is enabled.
        /// </summary>
        private void LogWarning(string message)
        {
            _logger.LogWarning(message);
        }

        /// <summary>
        /// Log a message if warning logging is enabled. 
        /// </summary>
        private void LogWarning(string messageFormat, params object[] args)
        {
            _logger.LogWarning(messageFormat, args);
        }


        /// <summary>
        /// Log a message if error logging is enabled.
        /// </summary>
        private void LogError(string message)
        {
            _logger.LogError(message);
        }

        /// <summary>
        /// Log a message if error logging is enabled. 
        /// </summary>
        private void LogError(string messageFormat, params object[] args)
        {
            _logger.LogError(messageFormat, args);
        }
        #endregion Logging Methods

        #region Private Structs and Enums
        private struct Operation : IDisposable
        {
            public OperationType type;
            public PhotonParticipant player;
            public AvatarPose pose;

            public void Dispose()
            {
                if (pose is IDisposable)
                {
                    ((IDisposable)pose).Dispose();
                }
                pose = null;
            }
        }

        private enum OperationType
        {
            Unknown,
            Update,
        }

        #endregion Private Structs and Enums
    }
}
#endif // PHOTON_INSTALLED
