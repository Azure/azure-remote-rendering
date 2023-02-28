// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using System;
using UnityEngine;

public abstract class BaseEntityMaterialController : MonoBehaviour
{
    public abstract OverrideMaterialProperty<Color> ColorOverride { get; set; }
    public abstract OverrideMaterialProperty<float> RoughnessOverride { get; set; }
    public abstract OverrideMaterialProperty<float> MetalnessOverride { get; set; }
    public abstract event Action<Entity> TargetEntityChanged;
    public abstract Entity TargetEntity { get; set; }
    protected abstract void ConfigureTargetEntity();
    public abstract bool RevertOnEntityChange { get; set; }
    public abstract void Revert();
}

