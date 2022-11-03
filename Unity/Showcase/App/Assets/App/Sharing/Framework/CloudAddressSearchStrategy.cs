// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    public abstract class CloudAddressSearchStrategy : ISharingServiceAddressSearchStrategy
    {
        private ISharingServiceRoomAddresses _room;

        #region Constructor
        protected CloudAddressSearchStrategy(ISharingServiceRoomAddresses room)
        {
            _room = room ?? throw new ArgumentNullException("The CloudAddressSearchStrategy can't be null");
        }
        #endregion Constructor

        #region ISharingServiceAddressSearchStrategy
        /// <summary>
        /// Get if a search is possible
        /// </summary>
        public bool Enabled => CanFindAddressAnchors();

        /// <summary>
        /// Find the addresses around the user's physical location.
        /// </summary>
        public async Task<IList<SharingServiceAddress>> FindAddresses(CancellationToken ct)
        {
            var knownAddress = await _room.GetAddresses();

            if (AnchorSupport.IsNativeEnabled)
            {
                return await FindAddressAnchorsFromKnownAddresses(knownAddress, ct);
            }
            else
            {
                return await CreateFakeAddressAnchorsFromKnownAddresses(knownAddress, ct);
            }
        }
        #endregion ISharingServiceAddressSearchStrategy

        #region Protected Functions
        /// <summary>
        /// Compute if the device is able to find cloud anchors.
        /// </summary>
        /// <returns></returns>
        protected abstract bool CanFindAddressAnchors();

        /// <summary>
        /// Find the session anchors to the user's physical location.
        /// </summary>
        protected abstract Task<IList<SharingServiceAddress>> FindAddressAnchorsFromKnownAddresses(IEnumerable<SharingServiceAddress> anchors, CancellationToken ct);
        #endregion Protected Functions

        #region Private Functions
        /// <summary>
        /// Create a fake anchor from known the addresses. This should only be used
        /// on platforms that don't support anchoring, so to test Azure Spatial Anchor addresses.
        /// </summary>
        private async Task<IList<SharingServiceAddress>> CreateFakeAddressAnchorsFromKnownAddresses(IEnumerable<SharingServiceAddress> anchors, CancellationToken ct)
        {
            Transform camera = CameraCache.Main.transform;
            List<Task<SharingServiceAddress>> resultTasks = new List<Task<SharingServiceAddress>>();

            foreach (var anchor in anchors)
            {
                if (anchor?.Data != null)
                {
                    var randomDistance = UnityEngine.Random.Range(1.0f, 5.0f);
                    var randomRotation = UnityEngine.Random.Range(-90f, 90f);
                    var randomForward = randomDistance * (Quaternion.Euler(0, randomRotation, 0) * Vector3.forward);
                    var randomPose = new Pose(camera.position + randomForward, Quaternion.identity);
                    resultTasks.Add(SharingServiceAddress.LoadAddress(anchor.Data, randomPose, ct));
                }
            }
            ct.ThrowIfCancellationRequested();

            return await Task.WhenAll(resultTasks);
        }
        #endregion Private Functions
    }
}
