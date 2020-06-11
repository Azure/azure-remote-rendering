// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// The event arguments used when the number of active anchor creations change.
    /// </summary>
    public class AnchoringServiceCreatingArgs
    {
        public AnchoringServiceCreatingArgs(int activeCreations)
        {
            ActiveCreationsCount = activeCreations;
        }

        /// <summary>
        /// The number of active anchor creations
        /// </summary>
        public int ActiveCreationsCount { get; }

        /// <summary>
        /// Get if the service is currently creating new cloud anchors.
        /// </summary>
        public bool IsCreating => ActiveCreationsCount > 0;
    }
}
