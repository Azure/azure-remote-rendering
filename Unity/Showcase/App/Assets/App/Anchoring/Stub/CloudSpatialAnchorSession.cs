// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if !AZURE_SPATIAL_ANCHORS_ENABLED
using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.SpatialAnchors.Stub
{
    public class CloudSpatialAnchorSession
    {
        public PlatformLocationProvider LocationProvider { get; internal set; }
        public object SessionId { get; internal set; }

        internal Task CreateAnchorAsync(CloudSpatialAnchor cloudSpatialAnchor)
        {
            throw new NotImplementedException();
        }

        internal CloudSpatialAnchorWatcher CreateWatcher(AnchorLocateCriteria anchorLocateCriteria)
        {
            throw new NotImplementedException();
        }

        internal Task DeleteAnchorAsync(CloudSpatialAnchor cloudSpatialAnchor)
        {
            throw new NotImplementedException();
        }

        internal Task UpdateAnchorPropertiesAsync(CloudSpatialAnchor cloudSpatialAnchor)
        {
            throw new NotImplementedException();
        }
    }
}
#endif
