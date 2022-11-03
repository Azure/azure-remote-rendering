// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    public interface ISharingServiceAddressSearchStrategy
    {
        /// <summary>
        /// Get if a search is possible
        /// </summary>
        bool Enabled { get; }

        /// <summary>
        /// Find the addresses around the user's physical location.
        /// </summary>
        Task<IList<SharingServiceAddress>> FindAddresses(CancellationToken ct);
    }
}
