// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Identity.Client;
using Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication;
using UnityEngine;
using UnityEngine.Audio;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// The sharing service settings that can be set via the Mixed Reality Toolkits inspector.
    /// </summary>
	[MixedRealityServiceProfile(typeof(ISharingService))]
	[CreateAssetMenu(fileName = "SharingServiceProfile", menuName = "ARR Showcase/Configuration Profile/Sharing Service")]
	public class SharingServiceProfile : BaseMixedRealityProfile
    {
        /// <summary>
        /// The networking service used to share data. Currently only Photon is supported.
        /// </summary>
        public enum ProviderService
        {
            None = 0,
            Photon = 1,
            Offline = 3,
        }

        [Header("General Settings")]

        [Tooltip("The networking service providing connectivity")]
        public ProviderService Provider = ProviderService.None;

        [Tooltip("True to automatically login to sharing service on app startup.")]
        public bool AutoStart = true;

        [Tooltip("The format of the new public room names. The {0} field will be filled with an integer.")]
        public string RoomNameFormat = "Room {0}";

        [Tooltip("The format of the new private room names. The {0} field will be filled with an integer.")]
        public string PrivateRoomNameFormat = "Private {0}";

        [Tooltip("Include verbose logging for diagnostics")]
        public bool VerboseLogging = false;

        [Header("Photon Settings")]

        [Tooltip("The Photon service's PUN app id.")]
        public string PhotonRealtimeId = null;

        [Tooltip("The Photon service's voice app id.")]
        public string PhotonVoiceId = null;

        [Tooltip("The Photon service avatar prefab")]
        public GameObject PhotonAvatarPrefab = null;

        [Tooltip("The primary colors to apply to players.")]
        public Color[] PhotonPlayerColors = null;
    }
}
