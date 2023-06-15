// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if !AZURE_SPATIAL_ANCHORS_ENABLED
namespace Microsoft.Azure.SpatialAnchors.Stub
{
    public class SensorCapabilities
    {
        public bool BluetoothEnabled { get; internal set; }
        public bool GeoLocationEnabled { get; internal set; }
        public bool WifiEnabled { get; internal set; }
    }
}
#endif
