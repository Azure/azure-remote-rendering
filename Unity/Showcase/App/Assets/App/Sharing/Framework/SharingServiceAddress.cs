// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// Represents a co-localizable space for the sharing service
    /// </summary>
    public class SharingServiceAddress : IAppAnchor
    {
        private IAppAnchor _inner;
        private string _anchorId;
        private CancellationTokenSource _saveCancellationSource = null;
        private bool _disposed = false;
        private const string _anchorStoreName = "sharing_service_address_anchor";
        private LogHelper<SharingServiceAddress> _log = new LogHelper<SharingServiceAddress>();

        #region Constructors
        public SharingServiceAddress(SharingServiceAddressType type, string data)
        {
            Data = data;
            Type = type;
            IsExisting = true;

            if (Type == SharingServiceAddressType.Anchor)
            {
                AnchorId = data;
            }
        }

        private SharingServiceAddress(SharingServiceAddressType type)
        {
            Type = type;
            IsExisting = false;
        }
        #endregion Constructors

        #region Public Properties
        /// <summary>
        /// The address id
        /// </summary>
        public string Data { get; private set; }

        /// <summary>
        /// The type of the address
        /// </summary>
        public SharingServiceAddressType Type { get; }

        /// <summary>
        /// When address was joined, was this address an existing address, with potentially other users.
        /// </summary>
        public bool IsExisting { get; }
        #endregion Public Properties

        #region IAppAnchor Properties
        /// <summary>
        /// The debug name.
        /// </summary>
        public string Name
        {
            get => $"SharingServiceAddress:{Type}:{Data}";
        }

        /// <summary>
        /// Get the anchor id.
        /// </summary>
        public string AnchorId
        {
            get => _anchorId;

            private set
            {
                _anchorId = value;
                if (Type == SharingServiceAddressType.Anchor)
                {
                    Data = value;
                }
            }
        }

        /// <summary>
        /// Get the located native anchor transform.
        /// </summary>
        public Transform Transform
        {
            get
            {
                return _inner?.Transform;
            }
        }

        /// <summary>
        /// Get the position of the anchor
        /// </summary>
        public Vector3 Position
        {
            get
            {
                return _inner?.Position ?? Vector3.zero;
            }
        }

        /// <summary>
        /// Get the rotation of the anchor.
        /// </summary>
        public Quaternion Rotation
        {
            get
            {
                return _inner?.Rotation ?? Quaternion.identity;
            }
        }

        /// <summary>
        /// Did this anchor start from a cloud anchor. If true, the anchor was initialized from a cloud anchor id.
        /// If false, the anchor was initialized from a native anchor. 
        /// </summary>
        public bool FromCloud
        {
            get => _inner?.FromCloud ?? false;
        }

        /// <summary>
        /// Get if the anchor has been located
        /// </summary>
        public bool IsLocated
        {
            // If there's a known transform for an "online" anchor, assume the pose is known.
            // This is done because an "online" anchor could be in the process of creation, meaning _inner.IsLocated is false.
            // So when using a "online" anchor, we want to use a known transform to prevent a "jumpy" origin visual  (i.e. "jumpy" stage).
            get
            {
                return (_inner?.IsLocated == true) || (Type == SharingServiceAddressType.Anchor && _inner?.Transform != null);
            }
        }

        /// <summary>
        /// Get the ARAnchor if it exists
        /// </summary>
        public UnityEngine.XR.ARFoundation.ARAnchor ArAnchor => (_inner?.ArAnchor);
        #endregion IAppAnchor Properties

        #region Public Static Properties
        /// <summary>
        /// Get the data for the device addresses created on this machine.
        /// </summary>
        public static string DeviceData => SystemInfo.deviceUniqueIdentifier;
        #endregion Public Static Properties

        #region IAppAnchor Events
        /// <summary>
        /// Event raise when the cloud anchor has changed.
        /// </summary>
        public event Action<IAppAnchor, string> AnchorIdChanged;
        #endregion IAppAnchor Events

        #region IAppAnchor Methods
        /// <summary>
        /// Release resources
        /// </summary>
        public void Dispose()
        {
            if (_inner != null)
            {
                _inner.AnchorIdChanged -= OnInnerAnchorIdChanged;
                _inner = null;
            }

            CancelSave();
            _disposed = true;
        }

        /// <summary>
        /// Try to move the anchor to this new position.
        /// </summary>
        public Task Move(Transform transform)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Try to move the anchor to this new position.
        /// </summary>
        public Task Move(Pose pose)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Delete the native and cloud anchors.
        /// </summary>
        public void Delete()
        {
        }

        /// <summary>
        /// Forget the cloud anchor.
        /// </summary>
        public void ForgetCloud()
        {
        }
        #endregion IAppAnchor Methods

        #region Public Methods
        public override string ToString()
        {
            return $"SharingServiceAddress:{Type}:{Data}:{IsExisting}:{IsLocated}:{Position}";
        }

        public static bool IsNullOrEmpty(SharingServiceAddress value)
        {
            return string.IsNullOrEmpty(value?.Data);
        }

        public Task Save()
        {
            var anchorStore = ARAnchorStore.Instance;
            if (anchorStore == null)
            {
                _log.LogWarning("Unable to save anchor, there is no ARAnchor Store instance.");
                return Task.CompletedTask;
            }
            else if (!_disposed)
            {
                CancelSave();
                _saveCancellationSource = new CancellationTokenSource();
                return ARAnchorStore.Instance?.SaveAnchor(_anchorStoreName, _inner?.ArAnchor, force: true, _saveCancellationSource.Token);
            }
            else
            {
                return Task.CompletedTask;
            }
        }
        #endregion Public Methods

        #region Public Static Methods
        /// <summary>
        /// Create an address that is unique to this device.
        /// </summary>
        public static SharingServiceAddress DeviceAddress(string deviceData = null)
        {
            SharingServiceAddress result = new SharingServiceAddress(SharingServiceAddressType.Device)
            {
                Data = deviceData ?? DeviceData
            };

            return result;
        }

        /// <summary>
        /// Load an anchor address using the given cloud id and transform
        /// </summary>
        public static async Task<SharingServiceAddress> LoadAddress(string cloudAnchorId, Transform transform, CancellationToken ct)
        {
            SharingServiceAddress result = new SharingServiceAddress(SharingServiceAddressType.Anchor, cloudAnchorId);
            try
            {
                await result.CreateInnerAnchor(transform, createCloudAnchor: false, ct);
                ct.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                result.Dispose();
                throw ex;
            }

            return result;
        }

        /// <summary>
        /// Load an anchor address using the given cloud id and world pose
        /// </summary>
        public static async Task<SharingServiceAddress> LoadAddress(string cloudAnchorId, Pose worldPose, CancellationToken ct)
        {
            SharingServiceAddress result = new SharingServiceAddress(SharingServiceAddressType.Anchor, cloudAnchorId);

            try
            {
                await result.CreateInnerAnchor(worldPose, createCloudAnchor: false, ct);
                ct.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                result.Dispose();
                throw ex;
            }

            return result;
        }

        /// <summary>
        /// Load an anchor address using the given cloud id and an app anchor. Note, this take ownership of the appAnchor.
        /// </summary>
        public static Task<SharingServiceAddress> LoadAddress(string cloudAnchorId, IAppAnchor appAnchor, CancellationToken ct)
        {
            SharingServiceAddress result = new SharingServiceAddress(SharingServiceAddressType.Anchor, cloudAnchorId);

            try
            {
                result.SetInner(appAnchor, ct);
                result.AnchorId = cloudAnchorId;
            }
            catch (Exception ex)
            {
                result.Dispose();
                throw ex;
            }

            return Task.FromResult(result);
        }

        /// <summary>
        /// Create an anchor address using the custom ASA account configured in the application configuration.
        /// </summary>
        public static async Task<SharingServiceAddress> CreateAddress(Transform transform, CancellationToken ct)
        {
            SharingServiceAddress result = new SharingServiceAddress(SharingServiceAddressType.Anchor);

            try
            {
                await result.CreateInnerAnchor(transform, createCloudAnchor: true, ct);
                ct.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                result.Dispose();
                throw ex;
            }

            return result;
        }

        /// <summary>
        /// Load an anchor address using the anchor store which persists anchors on the local device.
        /// </summary>
        public static async Task<SharingServiceAddress> LoadOfflineAddress(CancellationToken ct)
        {
            SharingServiceAddress result = null;
            ARAnchor arAnchor = null;
            if (ARAnchorStore.Instance != null)
            {
                arAnchor = await ARAnchorStore.Instance.LoadAnchor(_anchorStoreName, ct);
            }

            if (arAnchor != null)
            {
                result = new SharingServiceAddress(SharingServiceAddressType.OfflineAnchor, _anchorStoreName);
                try
                {
                    ct.ThrowIfCancellationRequested();
                    result.CreateInnerAnchor(arAnchor, createCloudAnchor: false);
                }
                catch (Exception ex)
                {
                    result.Dispose();
                    UnityEngine.Object.Destroy(arAnchor.gameObject);
                    throw ex;
                }
            }

            return result;
        }

        /// <summary>
        /// Create an anchor address used when offline
        /// </summary>
        public static async Task<SharingServiceAddress> CreateOfflineAddress(Transform transform, CancellationToken ct)
        {
            SharingServiceAddress result = new SharingServiceAddress(SharingServiceAddressType.OfflineAnchor, _anchorStoreName);

            try
            {
                await result.CreateInnerAnchor(transform, createCloudAnchor: false, ct);
                ct.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                result.Dispose();
                throw ex;
            }

            return result;
        }
        #endregion Public Static Methods

        #region Private Methods
        /// <summary>
        /// Cancel current saves.
        /// </summary>
        private void CancelSave()
        {
            if (_saveCancellationSource != null)
            {
                _saveCancellationSource.Cancel();
                _saveCancellationSource.Dispose();
                _saveCancellationSource = null;
            }
        }

        /// <summary>
        /// Create an inner anchor from the given pose
        /// </summary>
        private async Task CreateInnerAnchor(Pose pose, bool createCloudAnchor, CancellationToken ct)
        {
            if (_inner != null)
            {
                throw new InvalidOperationException("Creating a new inner anchor, once one is already created, is not supported.");
            }

            if (Type != SharingServiceAddressType.Anchor &&
                Type != SharingServiceAddressType.OfflineAnchor)
            {
                throw new InvalidOperationException("Type is not of type anchor");
            }

            SetInner(await AppAnchor.Create($"{Name}:Inner", pose, createCloudAnchor), ct);
        }

        /// <summary>
        /// Create an inner anchor from the given tranform.
        /// </summary>
        private async Task CreateInnerAnchor(Transform transform, bool createCloudAnchor, CancellationToken ct)
        {
            if (_inner != null)
            {
                throw new InvalidOperationException("Creating a new inner anchor, once one is already created, is not supported.");
            }

            if (Type != SharingServiceAddressType.Anchor &&
                Type != SharingServiceAddressType.OfflineAnchor)
            {
                throw new InvalidOperationException("Type is not of type anchor");
            }

            SetInner(await AppAnchor.Create($"{Name}:Inner", transform, createCloudAnchor), ct);
        }

        /// <summary>
        /// Create an inner anchor from an AR anchor
        /// </summary>
        private void CreateInnerAnchor(ARAnchor arAnchor, bool createCloudAnchor)
        {
            if (_inner != null)
            {
                throw new InvalidOperationException("Creating a new inner anchor, once one is already created, is not supported.");
            }

            if (Type != SharingServiceAddressType.Anchor &&
                Type != SharingServiceAddressType.OfflineAnchor)
            {
                throw new InvalidOperationException("Type is not of type anchor");
            }
            
            SetInner(AppAnchor.Create($"{Name}:Inner", arAnchor, createCloudAnchor));
        }

        private void SetInner(IAppAnchor appAnchor, CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested)
            {
                appAnchor?.Dispose();
                ct.ThrowIfCancellationRequested();
            }

            _inner = appAnchor;
            if (_inner != null)
            {
                _inner.AnchorIdChanged += OnInnerAnchorIdChanged;
                OnInnerAnchorIdChanged(_inner, _inner.AnchorId);
            }
        }

        private void OnInnerAnchorIdChanged(IAppAnchor sender, string anchorId)
        {
            if (!IsExisting && AnchorId != anchorId)
            {
                AnchorId = anchorId;
                AnchorIdChanged?.Invoke(this, anchorId);
            }
        }
        #endregion Private Methods
    }

    public enum SharingServiceAddressType
    {
        Device,
        Anchor,
        OfflineAnchor,
    }
}
