// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using UnityEngine;

/// <summary>
/// A component that can be hidden by the IAppSettings service.
/// </summary>
public class AppSettingsInterface : MonoBehaviour
{
    private void Start()
    {
        AppServices.AppSettingsService.AddInterface(gameObject);
    }
}
