// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// Represents the root co-localizable space for the sharing service. The purpose of this class is so that
    /// consumers can track the root pose without worrying about address changes.
    /// </summary>
    public class SharingServiceRootAddress : IAppAnchor
    {
        private ISharingService _sharingService;

        #region Constructors
        public SharingServiceRootAddress(ISharingService sharingService)
        {
            if (sharingService == null)
            {
                throw new ArgumentNullException("The sharing service cannot be null.");
            }

            _sharingService = sharingService;
        }
        #endregion Constructors

        #region Public Properties
        /// <summary>
        /// The address id
        /// </summary>
        public string Data => _sharingService.PrimaryAddress?.Data;

        /// <summary>
        /// The type of the address
        /// </summary>
        public SharingServiceAddressType Type => _sharingService.PrimaryAddress?.Type ?? SharingServiceAddressType.Device;

        /// <summary>
        /// When address was joined, was this address an existing address, with potentially other users.
        /// </summary>
        public bool IsExisting => _sharingService.PrimaryAddress?.IsExisting ?? false;
        #endregion Public Properties

        #region IAppAnchor Properties
        /// <summary>
        /// The debug name.
        /// </summary>
        public string Name
        {
            get => _sharingService.PrimaryAddress?.Name ?? "SharingServiceAddressRoot:Null";
            set {}
        }

        /// <summary>
        /// Get the anchor id.
        /// </summary>
        public string AnchorId => _sharingService.PrimaryAddress?.AnchorId;

        /// <summary>
        /// Get the located native anchor transform.
        /// </summary>
        public Transform Transform => _sharingService.PrimaryAddress?.Transform;

        /// <summary>
        /// Get the position of the anchor
        /// </summary>
        public Vector3 Position => _sharingService.PrimaryAddress?.Position ?? Vector3.zero;

        /// <summary>
        /// Get the rotation of the anchor.
        /// </summary>
        public Quaternion Rotation => _sharingService.PrimaryAddress?.Rotation ?? Quaternion.identity;

        /// <summary>
        /// Did this anchor start from a cloud anchor. If true, the anchor was initialized from a cloud anchor id.
        /// If false, the anchor was initialized from a native anchor. 
        /// </summary>
        public bool FromCloud => _sharingService.PrimaryAddress?.FromCloud ?? false;

        /// <summary>
        /// Get if the anchor has been located
        /// </summary>
        public bool IsLocated => _sharingService.PrimaryAddress?.IsLocated ?? false;

        /// <summary>
        /// Get the inner ar anchor
        /// </summary>
        public ARAnchor ArAnchor => _sharingService.PrimaryAddress?.ArAnchor;
        #endregion IAppAnchor Properties

        #region IAppAnchor Events
        /// <summary>
        /// Event raise when the cloud anchor has changed.
        /// </summary>
        /// <remarks>
        /// This event never fires. If the consumer needs to know about address changes, it should listen to 
        /// the SharingService events directly.
        /// </remarks>
#pragma warning disable CS0067 // The event 'SharingServiceAddressRoot.AnchorIdChanged' is never used
        public event Action<IAppAnchor, string> AnchorIdChanged;
#pragma warning restore CS0067 // The event 'SharingServiceAddressRoot.AnchorIdChanged' is never used
        #endregion IAppAnchor Events

        #region IAppAnchor Methods
        /// <summary>
        /// Release resources
        /// </summary>
        public void Dispose()
        {
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
            return _sharingService.PrimaryAddress?.ToString() ?? $"SharingServiceAddress:RootEmpty";
        }
        #endregion Public Methods
    }
}
