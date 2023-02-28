// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using UnityEngine;

public abstract class BaseRemoteEntityHelper : MonoBehaviour
{
    public abstract BaseEntityOverrideController EnsureOverrideComponent(Entity entity);
    
    public abstract HierarchicalEnableState GetState(Entity entity, HierarchicalStates feature);
    public abstract void ToggleDisableCollision(Entity entity);
    public abstract void ToggleHidden(Entity entity);
    public abstract void ToggleSeeThrough(Entity entity);
    public abstract void ToggleSelect(Entity entity);
    public abstract void ToggleTint(Entity entity);
    public abstract void RemoveOverrides(Entity entity);
} 