// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// Use the local anchors to find and create user location addresses
    /// </summary>
    public sealed class OfflineAnchorSearchStrategy : ISharingServiceAddressSearchStrategy
    {
        private LogHelper<OfflineAnchorSearchStrategy> _logger = new LogHelper<OfflineAnchorSearchStrategy>();

        #region Constructor
        public OfflineAnchorSearchStrategy()
        {
        }
        #endregion Constructor

        #region ISharingServiceAddressSearchStrategy
        /// <summary>
        /// Get if a search is possible
        /// </summary>
        public bool Enabled => AnchorSupport.IsNativeEnabled;

        /// <summary>
        /// Find the addresses around the user's physical location.
        /// </summary>
        public Task<IList<SharingServiceAddress>> FindAddresses(CancellationToken ct)
        {
            if (AnchorSupport.IsNativeEnabled)
            {
                return FindAddressAnchorsFromKnownAddresses(ct);
            }
            else
            {
                return Task.FromResult<IList<SharingServiceAddress>>(
                    new List<SharingServiceAddress>() { SharingServiceAddress.DeviceAddress() });
            }
        }
        #endregion ISharingServiceAddressSearchStrategy

        #region Private Functions
        private async Task<IList<SharingServiceAddress>> FindAddressAnchorsFromKnownAddresses(CancellationToken ct)
        {
            _logger.LogVerbose("FindAddressAnchorFromKnownAddresses() Entered");
            var result = new List<SharingServiceAddress>();

            _logger.LogVerbose("Loading an offline address.");
            var newAddress = await SharingServiceAddress.LoadOfflineAddress(ct);
            if (ct.IsCancellationRequested)
            {
                _logger.LogVerbose("Failed to load an offline address. Operation canceled.");
                newAddress?.Dispose();
            }
            else if (newAddress != null)
            {
                _logger.LogVerbose("Loaded an offline address.");
                result.Add(newAddress);
            }
            else
            {
                _logger.LogVerbose("No offline address was loaded.");
            }

            _logger.LogVerbose("FindAddressAnchorFromKnownAddresses() Exit");
            return result;
        }
        #endregion Private Functions
    }
}
