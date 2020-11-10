// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// The event arguments used when the number of anchor searches change.
    /// </summary>
    public class AnchoringServiceSearchingArgs
    {
        public AnchoringServiceSearchingArgs(int searchesCount)
        {
            ActiveSearchesCount = searchesCount;
        }

        /// <summary>
        /// The number of anchors being searched for
        /// </summary>
        public int ActiveSearchesCount { get; }

        /// <summary>
        /// Get if the service is currently searching for cloud anchors in the real-world.
        /// </summary>
        public bool IsSearching => ActiveSearchesCount > 0;
    }
}
