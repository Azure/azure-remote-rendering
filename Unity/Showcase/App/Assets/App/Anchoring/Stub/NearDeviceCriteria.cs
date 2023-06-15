// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if !AZURE_SPATIAL_ANCHORS_ENABLED
namespace Microsoft.Azure.SpatialAnchors.Stub
{
    public class NearDeviceCriteria
    {
        public float DistanceInMeters { get; internal set; }
        public int MaxResultCount { get; internal set; }
    }
}
#endif
