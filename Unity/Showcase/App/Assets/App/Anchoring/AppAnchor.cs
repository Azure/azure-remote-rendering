// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.SpatialAnchors;
using Microsoft.Azure.SpatialAnchors.Unity;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// This wraps an Azure Spatial Anchor and native anchor with app specific logic.
    /// </summary>
    public class AppAnchor : IAppAnchor
    {
        private static GameObject _nativeAnchorContainer = null;

        private Transform _ownedTransform;
        private string _anchorId;
        private bool _createCloudAnchor;
        private CancellationTokenSource _anchorCancellation;
        private IAnchoringService _anchorService;
        private AppAnchorType _type;
        private LogHelper<AppAnchor> _log = new LogHelper<AppAnchor>();

        /// <summary>
        /// Create an empty app anchor with no native or cloud anchor immediate. To create these, a move must occur.
        /// </summary>
        /// <param name="allowNewCloudAnchorsOnMove">Should new cloud anchors be created, when a move occurs.</param>
        private AppAnchor(string name, AppAnchorType type, bool allowNewCloudAnchorsOnMove = true)
        {
            Name = name;
            _type = type;
            _anchorService = AppServices.AnchoringService ?? throw new ArgumentNullException("AnchoringService can't be null.");
            _createCloudAnchor = allowNewCloudAnchorsOnMove && _anchorService.IsCloudEnabled;
        }

        #region IAppAnchor Properties
        /// <summary>
        /// The debug name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Get the anchor id.
        /// </summary>
        public string AnchorId
        {
            get => _anchorId;
        }

        /// <summary>
        /// Get the located native anchor transform.
        /// </summary>
        public Transform Transform => _ownedTransform;

        /// <summary>
        /// Get the position of the anchor
        /// </summary>
        public Vector3 Position => Transform == null ? Vector3.zero : Transform.position;

        /// <summary>
        /// Get the rotation of the anchor.
        /// </summary>
        public Quaternion Rotation => Transform == null ? Quaternion.identity : Transform.rotation;

        /// <summary>
        /// Get if the anchor has been located
        /// </summary>
        public bool IsLocated
        {
            get => ArAnchor != null && ArAnchor.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.Tracking;
        }

        /// <summary>
        /// Did this anchor start from a cloud anchor. If true, the anchor was initialized from a cloud anchor id.
        /// </summary>
        public bool FromCloud => _type == AppAnchorType.FromCloud;

        /// <summary>
        /// Get the ARAnchor if it exists
        /// </summary>
        public ARAnchor ArAnchor { get; private set; }
        #endregion IAppAnchor Properties

        #region IAppAnchor Events
        /// <summary>
        /// Event raise when the cloud anchor has changed.
        /// </summary>
        public event Action<IAppAnchor, string> AnchorIdChanged;
        #endregion IAppAnchor Events

        #region IAppAnchor Methods
        /// <summary>
        /// Release resources used by this anchor object.
        /// </summary>
        public void Dispose()
        {
            StopAsyncOperation();
            ClearOwnedTransform(deleteCloud: false);
        }

        /// <summary>
        /// Start moving the cloud and/or native anchor to a new position, and complete a task once finished.
        /// </summary>
        public Task Move(Transform transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException("Given transform is null");
            }

            return TryRecreatingCloudNativeAnchor(new Pose(transform.position, transform.rotation));
        }

        /// <summary>
        /// Start moving the cloud and/or native anchor to a new position, and complete a task once finished.
        /// </summary>
        public Task Move(Pose pose)
        {
            if (!pose.position.IsValidVector() ||
                !pose.rotation.IsValidRotation())
            {
                throw new ArgumentNullException("Given pose is invalid");
            }

            return TryRecreatingCloudNativeAnchor(pose);
        }

        /// <summary>
        /// Delete the native and cloud anchors.
        /// </summary>
        public void Delete()
        {
            ClearOwnedTransform(deleteCloud: true);
            SetAnchorId(null);
        }
        #endregion IAppAnchor Methods

        #region Public Methods
        /// <summary>
        /// Create an anchor from a cloud anchor id.
        /// </summary>
        public static async Task<AppAnchor> Create(string name, string anchorId, bool allowNewCloudAnchorsOnMove = true)
        {
            if (string.IsNullOrEmpty(anchorId))
            {
                throw new ArgumentNullException("Anchor id is null or empty");
            }

            AppAnchor result = new AppAnchor(name, AppAnchorType.FromCloud, allowNewCloudAnchorsOnMove);

            try
            {
                await result.TryRecreatingCloudNativeAnchor(anchorId);
            }
            catch (Exception ex)
            {
                result.Dispose();
                throw ex;
            }

            return result;
        }

        /// <summary>
        /// Create an anchor from a transform.
        /// </summary>
        public static Task<AppAnchor> Create(string name, Transform transform, bool allowNewCloudAnchorsOnMove = true)
        {
            if (transform == null)
            {
                throw new ArgumentNullException("Transform is null or empty");
            }

            return Create(name, new Pose(transform.position, transform.rotation), allowNewCloudAnchorsOnMove);
        }

        /// <summary>
        /// Create an anchor from a pose.
        /// </summary>
        public static async Task<AppAnchor> Create(string name, Pose pose, bool allowNewCloudAnchorsOnMove = true)
        {
            AppAnchor result = new AppAnchor(name, AppAnchorType.FromPose, allowNewCloudAnchorsOnMove);

            try
            {
                await result.TryRecreatingCloudNativeAnchor(pose);
            }
            catch (Exception ex)
            {
                result.Dispose();
                throw ex;
            }

            return result;
        }

        /// <summary>
        /// Create an app anchor from a platform ARAnchor, and take ownership of the ARAnchor.
        /// </summary>
        public static AppAnchor Create(string name, ARAnchor arAnchor, bool allowNewCloudAnchorsOnMove = true)
        {
            if (arAnchor == null)
            {
                throw new ArgumentNullException("AR Anchor is null.");
            }

            AppAnchor result = new AppAnchor(name, AppAnchorType.FromNative, allowNewCloudAnchorsOnMove);
            result.SetOwnedTransform(arAnchor.transform);
            return result;
        }
        #endregion Public Methods

        #region Private Methods
        /// <summary>
        /// Get the native anchor container used by spatial anchors
        /// </summary>
        private static GameObject GetNativeAnchorContainer()
        {
            if (_nativeAnchorContainer == null)
            {
                _nativeAnchorContainer = new GameObject($"Spatial Query Native Anchors");
                MixedRealityPlayspace.AddChild(_nativeAnchorContainer.transform);
                _nativeAnchorContainer.transform.position = Vector3.zero;
                _nativeAnchorContainer.transform.rotation = Quaternion.identity;
            }

            return _nativeAnchorContainer;
        }

        /// <summary>
        /// Called at a start of a new async operation. This will cancel the last async operation
        /// </summary>
        private CancellationToken StartAsyncOperation()
        {
            StopAsyncOperation();
            _anchorCancellation = new CancellationTokenSource();
            return _anchorCancellation.Token;
        }

        /// <summary>
        /// Stop the current async operation.
        /// </summary>
        private void StopAsyncOperation()
        {
            if (_anchorCancellation != null)
            {
                _anchorCancellation.Cancel();
                _anchorCancellation.Dispose();
                _anchorCancellation = null;
            }
        }

        /// <summary>
        /// Set the Azure Spatial Anchor id, and invoke "anchor id changed" event.
        /// </summary>
        private void SetAnchorId(string id)
        {
            UpdateOwnedTransformName();

            // Only fire a change event if IDs changed, or the ID is an empty GUID. If the IDs are empty GUIDs, the 
            // anchors could be in the middle of creation, and could be different.
            if (_anchorId != id || id == AnchorSupport.EmptyAnchorId)
            {               
                _log.LogVerbose("Saving anchor ID (name: {0}) (old anchor: {1}) (new anchor: {2}) (listeners: {3})", Name, _anchorId, id, (AnchorIdChanged == null ? "null" : "not null"));
                _anchorId = id;
                AnchorIdChanged?.Invoke(this, id);
            }
        }

        /// <summary>
        /// Create a new CloudNativeAnchor object from a given Azure Spatial Anchor ID. This will destroy the old CloudNativeAnchor.
        /// </summary>
        private async Task TryRecreatingCloudNativeAnchor(string anchorId)
        {
            var cancelToken = StartAsyncOperation();
            ClearOwnedTransform(deleteCloud: false);
            SetAnchorId(anchorId);

            CloudSpatialAnchor cloudSpatialAnchor = null;
            if (_anchorService.IsCloudEnabled)
            {
                try
                {
                    SetAnchoringServiceFindOptions();
                    _log.LogVerbose("Searching for cloud anchor. Starting (name: {0}) (id: {1})", Name, anchorId);
                    cloudSpatialAnchor = await _anchorService.Find(anchorId, cancelToken);
                    await Task.Delay(TimeSpan.FromSeconds(3));
                    _log.LogVerbose("Searching for cloud anchor. Ending (name: {0}) (id: {1})", Name, anchorId);
                }
                catch (TaskCanceledException)
                {
                    _log.LogVerbose("Canceled searching for cloud anchor. (name: {0}) (id: {1})", Name, anchorId);
                }
            }

            // Only commit the new anchor, if the owned transform and anchor id hasn't changed.
            if (cloudSpatialAnchor != null &&
                Transform == null && 
                !AnchorSupport.IsEmptyAnchor(AnchorId) &&
                AnchorId == anchorId)
            {
                CreateOwnedTransform(cloudSpatialAnchor);
            }
            else
            {
                _log.LogVerbose("Ignoring anchor search result (has owned transform: {0}) (service returned cloud spatial anchor: {1}) (search anchor id: {2}) (found anchor id: {3})",
                    Transform != null,
                    cloudSpatialAnchor != null,
                   AnchorId,
                   anchorId);
            }
        }

        /// <summary>
        /// Use the Azure Spatial Anchors default settings when searching for model anchors.
        /// </summary>
        private void SetAnchoringServiceFindOptions()
        {
            _anchorService.FindOptions = default;
        }

        /// <summary>
        /// Create a new CloudNativeAnchor object from a given transform. This will destroy the old CloudNativeAnchor.
        /// </summary>
        private Task TryRecreatingCloudNativeAnchor(Transform transform, bool deleteOldCloud = true, bool allowNewCloud = true)
        {
            return TryRecreatingCloudNativeAnchor(new Pose(transform.position, transform.rotation), deleteOldCloud, allowNewCloud);
        }

        /// <summary>
        /// Create a new CloudNativeAnchor object from a given transform. This will destroy the old CloudNativeAnchor.
        /// </summary>
        private async Task TryRecreatingCloudNativeAnchor(Pose pose, bool deleteOldCloud = true, bool allowNewCloud = true)
        {
            _log.LogVerbose("TryRecreatingCloudNativeAnchorAndWait() starting (name: {0}) (allowNewCloud: {1}) (this._createCloudAnchor: {2})",
                Name,
                allowNewCloud,
                _createCloudAnchor);

            var cancelToken = StartAsyncOperation();

            // Create a new anchor object at the given pose
            ClearOwnedTransform(deleteCloud: deleteOldCloud);
            Transform anchorTransform = CreateOwnedTransform(pose);

            // Only create native anchors if the platform supports it.
            string anchorId;
            if (_createCloudAnchor && allowNewCloud)
            {
                anchorId = await CreateCloudAnchor(anchorTransform.gameObject, cancelToken);
            }
            else
            {
                _log.LogVerbose("TryRecreatingCloudNativeAnchorAndWait() Create cloud anchor ignored. (name: {0}) (existing anchor id: {1})", Name, AnchorId);
                anchorId = AnchorSupport.EmptyAnchorId;
            }

            // Only keep the new anchor if the cloudNativeAnchor hasn't changed, and we're still saving this transform
            // Otherwise destroy the used cloud anchor.
            if (!cancelToken.IsCancellationRequested &&
                Transform != null &&
                Transform == anchorTransform && 
                !string.IsNullOrEmpty(anchorId))
            {
                _log.LogVerbose("TryRecreatingCloudNativeAnchorAndWait() applying saved anchor for transform (name: {0}) (existing anchor id: {1}) (created anchor id: {2})",
                    Name,
                    AnchorId, 
                    anchorId);

                SetAnchorId(anchorId);
            }
            else
            {
                _log.LogVerbose("TryRecreatingCloudNativeAnchorAndWait() No longer saving this transform, destroying new cloud anchor (name: {0}) (existing anchor id: {1}) (created anchor id: {2}) (already has cloud anchor: {3}) (canceled: {4})",
                    Name,
                    AnchorId,
                    anchorId,
                    cancelToken.IsCancellationRequested);

                DestroyOwnedTransform(anchorTransform, deleteCloud: true);
            }

            _log.LogVerbose("TryRecreatingCloudNativeAnchorAndWait() ending (name: {0}) (allowNewCloud: {1}) (this._createCloudAnchor: {2})",
                Name,
                allowNewCloud,
                _createCloudAnchor);
        }

        /// <summary>
        /// Create a new CloudNativeAnchor object from a given Azure Spatial Anchor.
        /// </summary>
        private Transform CreateOwnedTransform(CloudSpatialAnchor cloudSpatialAnchor)
        {
            if (cloudSpatialAnchor == null)
            {
                return null;
            }

            _log.LogVerbose("Creating game object for cloud anchor. Starting (name: {0}) (id: {1})", Name, cloudSpatialAnchor.Identifier);
            Pose pose = cloudSpatialAnchor.GetPose();

            var anchorObject = new GameObject();
            anchorObject.transform.SetParent(GetNativeAnchorContainer().transform);
            anchorObject.transform.position = pose.position;
            anchorObject.transform.rotation = pose.rotation;
            anchorObject.EnsureComponent<ARAnchor>();
 
            SetOwnedTransform(anchorObject.transform);
            _log.LogVerbose("Creating game object for cloud anchor. Ending (name: {0}) (id: {1}) (position: {2}) (rotation: {3})", Name, cloudSpatialAnchor.Identifier, pose.position, pose.rotation.eulerAngles);
            return anchorObject.transform;
        }

        /// <summary>
        /// Create an owned transform from pose.
        /// </summary>
        private Transform CreateOwnedTransform(Pose pose)
        {
            _log.LogVerbose("Creating game object for pose. Starting (name: {0}) (pose: {1})", Name, pose);
            
            GameObject anchorObject = new GameObject();
            anchorObject.transform.SetParent(GetNativeAnchorContainer().transform);
            anchorObject.transform.position = pose.position;
            anchorObject.transform.rotation = pose.rotation;

            if (_anchorService.IsCloudEnabled)
            {
                anchorObject.FindOrCreateNativeAnchor();
                anchorObject.EnsureComponent<CloudNativeAnchor>();
            }
            else
            {
                anchorObject.EnsureComponent<ARAnchor>();
            }

            SetOwnedTransform(anchorObject.transform);
            _log.LogVerbose("Creating game object for pose. Ending (name: {0}) (pose: {1})", Name, pose);
            return anchorObject.transform;
        }

        /// <summary>
        /// Destroy an owned transform and its cloud anchor
        /// </summary>
        /// <param name="destroy"></param>
        private void DestroyOwnedTransform(Transform destroy, bool deleteCloud = true)
        {
            if (destroy != null)
            {
                RemoveCloudAnchor(destroy.gameObject, deleteCloud);
                UnityEngine.Object.Destroy(destroy.gameObject);
            }
        }

        /// <summary>
        /// Set the owned transform. This will destroy the old owned transform
        /// </summary>
        /// <param name="transform"></param>
        private void SetOwnedTransform(Transform transform)
        {
            ClearOwnedTransform(deleteCloud: false);
            _ownedTransform = transform;

            if (transform != null)
            {
                var cloudNativeAnchor = transform.GetComponent<CloudNativeAnchor>();
                if (cloudNativeAnchor != null)
                {
                    ArAnchor = cloudNativeAnchor.NativeAnchor;
                }
                else
                {
                    ArAnchor = transform.EnsureComponent<ARAnchor>();
                }
            }

            UpdateOwnedTransformName();
        }

        /// <summary>
        /// Clear the owned transform. 
        /// </summary>
        /// <param name="transform"></param>
        private void ClearOwnedTransform(bool deleteCloud = false)
        {
            if (_ownedTransform != null)
            {
                DestroyOwnedTransform(_ownedTransform, deleteCloud);
                _ownedTransform = null;
            }

            ArAnchor = null;
        }

        /// <summary>
        /// Update owned transform's name
        /// </summary>
        private void UpdateOwnedTransformName()
        {
            if (_ownedTransform != null)
            {
                _ownedTransform.name = $"Native Anchor (name: {Name}) ({AnchorId ?? "NULL"})";
            }
        }

        /// <summary>
        /// Add a cloud anchor to the given object if it supports it
        /// </summary>
        private async Task<string> CreateCloudAnchor(GameObject anchorObject, CancellationToken cancelToken)
        {
            // If platform doesn't support anchor's, we still want to assign an empty anchor id
            // for multi-user sharing purposes.
            string anchorId = AnchorSupport.EmptyAnchorId;

            var cloudNativeAnchor = anchorObject.GetComponent<CloudNativeAnchor>();
            if (cloudNativeAnchor != null)
            {
                _log.LogVerbose("AddCloudAnchor() saving anchor (name: {0}) (existing anchor id: {1})", Name, AnchorId);
                await cloudNativeAnchor.NativeToCloud();

                if (!cancelToken.IsCancellationRequested)
                {
                    anchorId = await _anchorService.Save(cloudNativeAnchor.CloudAnchor);
                }

                if (cancelToken.IsCancellationRequested)
                {
                    anchorId = AnchorSupport.EmptyAnchorId;
                }
                _log.LogVerbose("AddCloudAnchor() saved anchor (name: {0}) (existing anchor id: {1}) (created anchor id: {2})", Name, AnchorId, anchorId);
            }

            return anchorId;
        }

        /// <summary>
        /// Destroy the cloud anchor component for the given GameObject.
        /// </summary>
        private void RemoveCloudAnchor(GameObject anchorObject, bool deleteCloud = true)
        {
            if (anchorObject != null)
            {
                var cloudNativeAnchor = anchorObject.GetComponent<CloudNativeAnchor>();
                if (cloudNativeAnchor != null)
                {
                    UnityEngine.Object.Destroy(cloudNativeAnchor);
                    if (deleteCloud && cloudNativeAnchor.CloudAnchor != null)
                    {
                        AppServices.AnchoringService.Delete(cloudNativeAnchor.CloudAnchor);
                    }
                }
            }
        }
        #endregion Private Methods

        #region Private Enumerations
        private enum AppAnchorType
        {
            /// <summary>
            /// The anchor was initialized from a cloud anchor id. 
            /// </summary>
            FromCloud,

            /// <summary>
            /// The anchor was initialized from a native anchor. 
            /// </summary>
            FromNative,

            /// <summary>
            /// The anchor was initialized from an app pose. 
            /// </summary>
            FromPose,
        }

        #endregion Private Enumerations
    }
}
