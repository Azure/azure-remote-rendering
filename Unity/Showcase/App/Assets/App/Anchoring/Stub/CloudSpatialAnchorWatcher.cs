// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if !AZURE_SPATIAL_ANCHORS_ENABLED
using System;

namespace Microsoft.Azure.SpatialAnchors.Stub
{
    public class CloudSpatialAnchorWatcher
    {
        internal void Stop()
        {
            throw new NotImplementedException();
        }
    }
}
#endif
