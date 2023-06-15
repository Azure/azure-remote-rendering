// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if !AZURE_SPATIAL_ANCHORS_ENABLED
using System;

using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Microsoft.Azure.SpatialAnchors.Stub
{
    public static class SpatialAnchorExtensions
    {
        static public ARAnchor FindOrCreateNativeAnchor(this GameObject gameObject)
        {
            throw new NotImplementedException();
        }
    }
}
#endif
