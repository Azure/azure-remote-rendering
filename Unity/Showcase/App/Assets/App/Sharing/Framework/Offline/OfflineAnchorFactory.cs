// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    public class OfflineAnchorFactory : ISharingServiceAddressFactory
    {
        private LogHelper<OfflineAnchorFactory> _log = new LogHelper<OfflineAnchorFactory>();

        #region ISharingServiceAddressFactory
        /// <summary>
        /// Create a new anchor for the user's physical location
        /// </summary>
        public async Task<SharingServiceAddress> CreateAddress(Transform transform, CancellationToken ct)
        {
            SharingServiceAddress result = null;

            try
            {
                _log.LogVerbose("Creating offline anchor address @ {0}", transform?.position);
                result = await SharingServiceAddress.CreateOfflineAddress(transform, ct);
                ct.ThrowIfCancellationRequested();

                await Task.Delay(TimeSpan.FromSeconds(20));

                _log.LogVerbose("Saving offline anchor address @ {0} (located: {1}) (state: {2})", result.Position, result.IsLocated, result.ArAnchor?.trackingState);
                await result.Save();
                ct.ThrowIfCancellationRequested();

                _log.LogVerbose("Saved offline anchor address @ {0} (located: {1}) (state: {2})", result.Position, result.IsLocated, result.ArAnchor?.trackingState);
            }
            catch (Exception ex)
            {
                result?.Dispose();
                throw ex;
            }
            return result;
        }
        #endregion ISharingServiceAddressFactory
    }
}
