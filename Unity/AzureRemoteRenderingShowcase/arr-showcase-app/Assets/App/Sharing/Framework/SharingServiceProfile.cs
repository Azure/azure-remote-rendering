// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// The sharing service settings that can be set via the Mixed Reality Toolkits inspector.
    /// </summary>
	[MixedRealityServiceProfile(typeof(ISharingService))]
	[CreateAssetMenu(fileName = "SharingServiceProfile", menuName = "MixedRealityToolkit/SharingService Configuration Profile")]
	public class SharingServiceProfile : BaseMixedRealityProfile
    {
        /// <summary>
        /// The networking service used to share data. Currently only Photon is supported.
        /// </summary>
        public enum ProviderService
        {
            Photon
        }

        [Header("General Settings")]

        [Tooltip("The networking service providing connectivity")]
        public ProviderService Provider = ProviderService.Photon;

        [Tooltip("True to automatically initialize the provider")]
        public bool AutoConnect = true;

        [Tooltip("True to automatically reconnect if the connection is lost")]
        public bool AutoReconnect = true;

        [Tooltip("The format of the new room names. The {0} field will be filled with an integer.")]
        public string RoomNameFormat = "Room {0}";

        [Tooltip("Include verbose logging for diagnostics")]
        public bool VerboseLogging = true;

        [Header("Photon Settings")]

        [Tooltip("The app id to use to initialize Photon's realtime service.")]
        public string PhotonRealtimeId;
    }
}
