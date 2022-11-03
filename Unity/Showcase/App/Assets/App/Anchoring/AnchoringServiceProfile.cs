// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// The anchoring service settings that can be set via the Mixed Reality Toolkits inspector.
    /// </summary>
	[MixedRealityServiceProfile(typeof(IAnchoringService))]
    [CreateAssetMenu(fileName = "AnchoringServiceProfile", menuName = "ARR Showcase/Configuration Profile/Anchoring Service")]
    public class AnchoringServiceProfile : BaseMixedRealityProfile
    {
        [Header("General Settings")]

        [Tooltip("Include verbose logging for diagnostics")]
        public bool VerboseLogging = true;

        [Tooltip("The time, in seconds, for an Azure Spatial Anchor search to timeout. If negative, there is no timeout.")]
        public float SearchTimeout = 5.0f * 60.0f;

        [Header("Azure Spatial Anchor Settings")]

        [Tooltip("The account id to use for Azure Spatial Anchors")]
        public string AnchorAccountId;

        [Tooltip("The account key to use for Azure Spatial Anchors")]
        public string AnchorAccountKey;

        [Tooltip("The account domain to use for Azure Spatial Anchors")]
        public string AnchorAccountDomain;

        [Tooltip("The number of day(s) new anchors will expire. If zero or negative, anchors will never expire.")]
        public float AnchorExpirationInDays = 1;
    }
}
