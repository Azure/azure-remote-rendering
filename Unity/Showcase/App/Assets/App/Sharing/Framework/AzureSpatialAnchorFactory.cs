// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    public class AzureSpatialAnchorFactory : ISharingServiceAddressFactory
    {
        #region ISharingServiceAddressFactory
        /// <summary>
        /// Create a new anchor for the user's physical location
        /// </summary>
        public Task<SharingServiceAddress> CreateAddress(Transform transform, CancellationToken ct)
        {
            return SharingServiceAddress.CreateAddress(transform, ct);
        }
        #endregion ISharingServiceAddressFactory
    }
}
