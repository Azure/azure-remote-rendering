// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if !AZURE_SPATIAL_ANCHORS_ENABLED
using System;

namespace Microsoft.Azure.SpatialAnchors.Stub
{
    public enum LocateAnchorStatus : int
    {
        AlreadyTracked = 0,
        Located = 1,
        NotLocated = 2,
        NotLocatedAnchorDoesNotExist = 3,
    }

    public class AnchorLocatedEventArgs : EventArgs
    {
        public CloudSpatialAnchor Anchor { get; internal set; }
        public string Identifier { get; internal set; }
        public LocateAnchorStatus Status { get; internal set; }
        public object Strategy { get; internal set; }
    }
}
#endif

