// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.SpatialAnchors;
using Microsoft.Azure.SpatialAnchors.Unity;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

#if UNITY_WSA && !UNITY_EDITOR
using UnityAnchor = UnityEngine.XR.WSA.WorldAnchor;
#else
using UnityAnchor = UnityEngine.Object;
#endif

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// This wraps an Azure Spatial Anchor and native anchor with app specific logic.
    /// </summary>
    public class AppAnchor : IAppAnchor
    {
        private string _anchorId;
        private bool _createCloudAnchor;
        private CancellationTokenSource _findAnchorIdCancellation;
        private Transform _savingTransform;
        private bool _savingTransformMoved;
        private IAnchoringService _anchorService;
        private CloudNativeAnchor _cloudNativeAnchor;
        private static GameObject _cloudNativeAnchorContainer = null;

        /// <summary>
        /// Create an empty app anchor with no native or cloud anchor.
        /// </summary>
        /// <param name="createCloudAnchor">Should new cloud anchors be created, when a move occurs.</param>
        public AppAnchor(bool createCloudAnchor = true)
        {
            _createCloudAnchor = createCloudAnchor;
            _anchorService = AppServices.AnchoringService ?? throw new ArgumentNullException("AnchoringService can't be null.");
        }

        /// <summary>
        /// Create an app anchor from an existing cloud anchor.
        /// </summary>
        /// <param name="createCloudAnchor">Should new cloud anchors be created, when a move occurs.</param>
        public AppAnchor(string anchorId, bool allowNewCloudAnchors = true) : this(allowNewCloudAnchors)
        {
            if (string.IsNullOrEmpty(anchorId))
            {
                throw new ArgumentNullException("Anchor id is null or empty");
            }

            FromCloud = true;
            TryRecreatingCloudNativeAnchor(anchorId);
        }

        /// <summary>
        /// Create an app anchor from a tranfrom \.
        /// </summary>
        /// <param name="createCloudAnchor">Should new cloud anchors be created.</param>
        public AppAnchor(Transform transform, bool createCloudAnchor = true) : this(createCloudAnchor)
        {
            if (transform == null)
            {
                throw new ArgumentNullException("Given transform is null");
            }

            FromNative = true;
            TryRecreatingCloudNativeAnchor(transform);
        }

        #region IAppAnchor Properties
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
        public Transform Transform { get; private set; }

        /// <summary>
        /// Get if the anchor has been located
        /// </summary>
        public bool IsLocated { get; private set; }

        /// <summary>
        /// Did this anchor start from a native anchor. If true, the anchor was initialized from a native anchor.
        /// If false, the anchor was initialized from a cloud anchor id. 
        /// </summary>
        public bool FromNative { get; }

        /// <summary>
        /// Did this anchor start from a cloud anchor. If true, the anchor was initialized from a cloud anchor id.
        /// If false, the anchor was initialized from a native anchor. 
        /// </summary>
        public bool FromCloud { get; }
        #endregion IAppAnchor Properties

        #region IAppAnchor Events
        /// <summary>
        /// Event raised when the cloud anchor has been located.
        /// </summary>
        public event Action<IAppAnchor> Located;

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
            DestroyCloudNativeAnchor(deleteCloud: false);
        }

        /// <summary>
        /// Move this cloud and native anchor to a new position
        /// </summary>
        /// <param name="transform"></param>
        public void Move(Transform transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException("Given transform is null");
            }
            
            TryRecreatingCloudNativeAnchor(transform);
        }

        /// <summary>
        /// Delete the native and cloud anchors.
        /// </summary>
        public void Delete()
        {
            DestroyCloudNativeAnchor(deleteCloud: true);
            Transform = null;
            SetAnchorId(null);
        }
        #endregion IAppAnchor Methods

        #region Public Properties
        /// <summary>
        /// An id representing an anchor that hasn't been saved to the cloud yet.
        /// </summary>
        public static string EmptyAnchorId => AnchoringService.EmptyAnchorId;
        #endregion Public Properties

        #region Public Methods
        /// <summary>
        /// Get if the platform support anchors.
        /// </summary>
        public static bool AnchorsSupported()
        {
            bool isDisplayOpaque = true;

#if UNITY_WSA
            isDisplayOpaque = UnityEngine.XR.WSA.HolographicSettings.IsDisplayOpaque;
#endif

            return !isDisplayOpaque && !Application.isEditor;
        }
        #endregion Public Methods

        #region Private Methods
        /// <summary>
        /// Get the native anchor container used by spatial anchors
        /// </summary>
        private static GameObject GetNativeAnchorContainer()
        {
            if (_cloudNativeAnchorContainer == null)
            {
                _cloudNativeAnchorContainer = new GameObject($"Spatial Query Native Anchors");
                MixedRealityPlayspace.AddChild(_cloudNativeAnchorContainer.transform);
                _cloudNativeAnchorContainer.transform.position = Vector3.zero;
                _cloudNativeAnchorContainer.transform.rotation = Quaternion.identity;
            }

            return _cloudNativeAnchorContainer;
        }

        /// <summary>
        /// Set the Azure Spatial Anchor id, and invoke "anchor id changed" event.
        /// </summary>
        private void SetAnchorId(string id)
        {
            // Only fire a change event if IDs changed, or the ID is an empty GUID. If the IDs are empty guids, the 
            // anchors could be in the middle of creation, and could be different.
            if (_anchorId != id || id == EmptyAnchorId)
            {
                _anchorId = id;
                AnchorIdChanged?.Invoke(this, id);
            }
        }

        /// <summary>
        /// Create a new CloudNativeAnchor object from a given Azure Spatial Anchor ID. This will destroy the old CloudNativeAnchor.
        /// </summary>
        private async void TryRecreatingCloudNativeAnchor(string anchorId)
        {
            _savingTransform = null;
            DestroyCloudNativeAnchor(deleteCloud: true);
            Transform = null;
            SetAnchorId(anchorId);

            CloudSpatialAnchor cloudNativeAnchor = null;
            if (AnchorsSupported() && anchorId != EmptyAnchorId)
            {
                try
                {
                    Debug.Assert(_findAnchorIdCancellation == null, "Old cancellation sources should have been destroyed at this point.");
                    _findAnchorIdCancellation = new CancellationTokenSource();
                    cloudNativeAnchor = await _anchorService.Find(anchorId, _findAnchorIdCancellation.Token);
                }
                catch (TaskCanceledException)
                {
                    // Ignore cancellations
                }
            }

            // Only commit the new anchor, if the cloudNativeAnchor hasn't changed.
            if (_cloudNativeAnchor == null &&
                cloudNativeAnchor != null &&
                AnchorId == anchorId &&
                !string.IsNullOrEmpty(AnchorId))
            {
                var nativeAnchorObject = new GameObject($"Native Anchor ({anchorId})");
                nativeAnchorObject.transform.SetParent(GetNativeAnchorContainer().transform);
                _cloudNativeAnchor = nativeAnchorObject.EnsureComponent<CloudNativeAnchor>();
                _cloudNativeAnchor.CloudToNative(cloudNativeAnchor);
                Transform = _cloudNativeAnchor.transform;
                StartTrackingNativeAnchor();
            }
        }

        /// <summary>
        /// Create a new CloudNativeAnchor object from a given transform. This will destroy the old CloudNativeAnchor.
        /// </summary>
        private async void TryRecreatingCloudNativeAnchor(Transform transform)
        {
            // If in the process of saving this transform escape early. After first save is done, anchor will be updated if transform moved.
            if (transform == _savingTransform)
            {
                _savingTransformMoved = true;
                return;
            }

            DestroyCloudNativeAnchor(deleteCloud: true);
            Transform = transform;
            SetAnchorId(EmptyAnchorId);
            _savingTransform = transform;

            // Singal a move to kickstart the while loop.
            _savingTransformMoved = true;
            CloudNativeAnchor cloudNativeAnchor = null;

            // If platform doesn't support anchor's, we still want to assign an empty anchor id
            // for multi-user sharing purposes.
            string anchorId = EmptyAnchorId;

            // Update anchor if it moved during saving
            while (_savingTransformMoved && _savingTransform == transform && _cloudNativeAnchor == null)
            {
                _savingTransformMoved = false;

                // Delete old anchor position
                DestroyCloudNativeAnchor(cloudNativeAnchor, deleteCloud: true);

                // Only create native anchors if the platform supports it.
                if (AnchorsSupported())
                {
                    GameObject nativeAnchorObject = new GameObject($"Native Anchor (Creating Cloud for {transform.name})");
                    nativeAnchorObject.transform.SetParent(GetNativeAnchorContainer().transform);
                    nativeAnchorObject.transform.position = transform.position;
                    nativeAnchorObject.transform.rotation = transform.rotation;
                    nativeAnchorObject.FindOrCreateNativeAnchor();

                    if (_createCloudAnchor)
                    {
                        cloudNativeAnchor = nativeAnchorObject.EnsureComponent<CloudNativeAnchor>();
                        cloudNativeAnchor.NativeToCloud();
                        anchorId = await _anchorService.Save(cloudNativeAnchor.CloudAnchor);
                        cloudNativeAnchor.name = $"Native Anchor ({anchorId})";
                    }
                }
            }

            // Only keep the new anchor if the cloudNativeAnchor hasn't changed, and we're still saving this transform
            // Otherwise destroy the used cloud anchor.
            if (_cloudNativeAnchor == null &&
                _savingTransform == transform &&
                !string.IsNullOrEmpty(anchorId))
            {
                _cloudNativeAnchor = cloudNativeAnchor;
                SetAnchorId(anchorId);
                StartTrackingNativeAnchor();
            }
            else
            {
                DestroyCloudNativeAnchor(cloudNativeAnchor, deleteCloud: true);
            }

            // Only clear flag if this was the last transform being saved.
            if (_savingTransform == transform)
            {
                _savingTransform = null;
            }
        }

        /// <summary>
        /// Destroy the cloud and native anchor for this object.
        /// </summary>
        private void DestroyCloudNativeAnchor(bool deleteCloud = true)
        {
            if (_findAnchorIdCancellation != null)
            {
                _findAnchorIdCancellation.Cancel();
                _findAnchorIdCancellation = null;
            }

            StopTrackingNativeAnchor();
            DestroyCloudNativeAnchor(_cloudNativeAnchor, deleteCloud);
            _cloudNativeAnchor = null;
        }

        /// <summary>
        /// Destroy the cloud and native anchor for this object.
        /// </summary>
        private static void DestroyCloudNativeAnchor(CloudNativeAnchor cloudNativeAnchor, bool deleteCloud = true)
        {
            if (cloudNativeAnchor != null)
            {
                if (cloudNativeAnchor.gameObject != null)
                {
                    UnityAnchor.Destroy(cloudNativeAnchor.gameObject);
                }

                if (deleteCloud && cloudNativeAnchor.CloudAnchor != null && !string.IsNullOrEmpty(cloudNativeAnchor.CloudAnchor.Identifier))
                {
                    AppServices.AnchoringService.Delete(cloudNativeAnchor.CloudAnchor);
                }
            }
        }

        /// <summary>
        /// Listen for native anchor being located+
        /// </summary>
        private void StartTrackingNativeAnchor()
        {
            if (_cloudNativeAnchor == null ||
                _cloudNativeAnchor.NativeAnchor == null ||
                !AnchorsSupported())
            {
                return;
            }

#if UNITY_WSA && !UNITY_EDITOR
            _cloudNativeAnchor.NativeAnchor.OnTrackingChanged += NativeAnchorTrackingUpdated;
            NativeAnchorTrackingUpdated(_cloudNativeAnchor.NativeAnchor, _cloudNativeAnchor.NativeAnchor.isLocated);
#endif
        }

        /// <summary>
        /// Stop tracking the native anchor
        /// </summary>
        private void StopTrackingNativeAnchor()
        {
            if (_cloudNativeAnchor == null ||
                _cloudNativeAnchor.NativeAnchor == null)
            {
                return;
            }

#if UNITY_WSA && !UNITY_EDITOR
            _cloudNativeAnchor.NativeAnchor.OnTrackingChanged -= NativeAnchorTrackingUpdated;
            NativeAnchorTrackingUpdated(_cloudNativeAnchor.NativeAnchor, false);
#else
            NativeAnchorTrackingUpdated(_cloudNativeAnchor, false);
#endif
        }

        /// <summary>
        /// Handle tracking updates
        /// </summary>
        private void NativeAnchorTrackingUpdated(UnityAnchor worldAnchor, bool located)
        {
            if (IsLocated != located)
            {
                IsLocated = located;
                if (IsLocated)
                {
                    Located?.Invoke(this);
                }
            }
        }
        #endregion Private Methods
    }
}
