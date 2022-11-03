// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    [MixedRealityServiceProfile(typeof(IAppSettingsService))]
    [CreateAssetMenu(fileName = "AppSettingsServiceProfile", menuName = "ARR Showcase/Configuration Profile/App Settings Service")]
    public class AppSettingsServiceProfile : BaseMixedRealityProfile
    {
        // Store config data in serialized fields
    }
}