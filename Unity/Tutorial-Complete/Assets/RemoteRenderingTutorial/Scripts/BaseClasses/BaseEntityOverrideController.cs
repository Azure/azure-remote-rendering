// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.Azure.RemoteRendering.Unity;
using System;
using UnityEngine;

public abstract class BaseEntityOverrideController : MonoBehaviour
{
    public abstract ARRHierarchicalStateOverrideComponent LocalOverride { get; }
    public abstract RemoteEntitySyncObject TargetEntity { get; }

    public abstract event Action<HierarchicalStates> FeatureOverrideChange;

    public abstract HierarchicalEnableState GetState(HierarchicalStates feature);
    
    public abstract void ToggleDisabledCollision();
    public abstract void ToggleHidden();
    public abstract void ToggleSeeThrough();
    public abstract void ToggleSelect();
    public abstract void ToggleTint(Color tintColor = default);
    public abstract void RemoveOverride();
}