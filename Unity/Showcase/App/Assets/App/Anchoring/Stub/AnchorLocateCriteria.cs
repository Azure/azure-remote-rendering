// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if !AZURE_SPATIAL_ANCHORS_ENABLED
namespace Microsoft.Azure.SpatialAnchors.Stub
{
    public enum AnchorDataCategory : int
    {
        None = 0,
        Properties = 1,
        Spatial = 2,
    }

    public enum LocateStrategy : int
    {
        AnyStrategy = 0,
        VisualInformation = 1,
        Relationship = 2,
    }

    public class AnchorLocateCriteria
    {
        public bool BypassCache { get; internal set; }
        public string[] Identifiers { get; internal set; }
        public NearDeviceCriteria NearDevice { get; internal set; }
        public AnchorDataCategory RequestedCategories { get; internal set; }
        public LocateStrategy Strategy { get; internal set; }
    }
}
#endif

