// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Rendering;
using UnityEngine;

public static class MaterialInstanceExtensions
{
    /// <summary>
    /// First try acquiring an existing material instance in the MaterialInstance, and
    /// if needed create a new material instance.
    /// </summary>
    public static Material AcquireExistingMaterial(this MaterialInstance instance)
    {
        return instance.AcquireMaterial(owner: null, instance: false) ??
            instance.AcquireMaterial(owner: null, instance: true);

    }

    /// <summary>
    /// First try acquiring an existing material instances in the MaterialInstance, and
    /// if needed create a new material instances.
    /// </summary>
    public static Material[] AcquireExistingMaterials(this MaterialInstance instance)
    {
        return instance.AcquireMaterials(owner: null, instance: false) ??
            instance.AcquireMaterials(owner: null, instance: true);

    }
}
