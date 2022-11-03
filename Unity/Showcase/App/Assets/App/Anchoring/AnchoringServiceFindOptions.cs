// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// Options to apply when a Find() operation is performed using an IAnchoringService.
    /// </summary>
    /// <remarks>
    /// Any value not set defaults to the Azure Spatial Anchor defaults.
    /// </remarks>
    public struct AnchoringServiceFindOptions
    {
        /// <summary>
        /// Should caches be bypassed when searching for anchors.
        /// </summary>
        public bool? BypassCache;

        /// <summary>
        /// Use the devices sensors to filter what anchors are searched for.
        /// </summary>
        public bool? NearDevice;

        /// <summary>
        /// This field is used along with NearDevice, and determines how far a 'near device' anchor can be.
        /// </summary>
        public float? MaxDistanceInMeters;

        /// <summary>
        /// This field is used along with NearDevice, and determines how many 'near device' anchors will be returned.
        /// </summary>
        public int? MaxNearResults;
    }
}