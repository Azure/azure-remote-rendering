// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if !AZURE_SPATIAL_ANCHORS_ENABLED
using System;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Microsoft.Azure.SpatialAnchors.Stub
{
    public class CloudNativeAnchor : MonoBehaviour
    {
        public CloudSpatialAnchor CloudAnchor { get; internal set; }
        public ARAnchor NativeAnchor { get; internal set; }

        internal Task NativeToCloud()
        {
            throw new NotImplementedException();
        }
    }
}
#endif
