// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.Azure.RemoteRendering.Unity;
using System;
using UnityEngine;

public class EntityOverrideController : BaseEntityOverrideController
{
    public override event Action<HierarchicalStates> FeatureOverrideChange;

    private ARRHierarchicalStateOverrideComponent localOverride;
    public override ARRHierarchicalStateOverrideComponent LocalOverride
    {
        get
        {
            if (localOverride == null)
            {
                localOverride = gameObject.GetComponent<ARRHierarchicalStateOverrideComponent>();
                if (localOverride == null)
                {
                    localOverride = gameObject.AddComponent<ARRHierarchicalStateOverrideComponent>();
                }

                var remoteStateOverride = TargetEntity.Entity.FindComponentOfType<HierarchicalStateOverrideComponent>();

                if (remoteStateOverride == null)
                {
                    // if there is no HierarchicalStateOverrideComponent on the remote side yet, create one
                    localOverride.Create(RemoteManagerUnity.CurrentSession);
                }
                else
                {
                    // otherwise, bind our local stateOverride component to the remote component
                    localOverride.Bind(remoteStateOverride);

                }
            }
            return localOverride;
        }
    }

    private RemoteEntitySyncObject targetEntity;
    public override RemoteEntitySyncObject TargetEntity
    {
        get
        {
            if (targetEntity == null)
                targetEntity = gameObject.GetComponent<RemoteEntitySyncObject>();
            return targetEntity;
        }
    }

    private HierarchicalEnableState ToggleState(HierarchicalStates feature)
    {
        HierarchicalEnableState setToState = HierarchicalEnableState.InheritFromParent;
        switch (LocalOverride.RemoteComponent.GetState(feature))
        {
            case HierarchicalEnableState.ForceOff:
            case HierarchicalEnableState.InheritFromParent:
                setToState = HierarchicalEnableState.ForceOn;
                break;
            case HierarchicalEnableState.ForceOn:
                setToState = HierarchicalEnableState.InheritFromParent;
                break;
        }

        return SetState(feature, setToState);
    }

    private HierarchicalEnableState SetState(HierarchicalStates feature, HierarchicalEnableState enableState)
    {
        if (GetState(feature) != enableState) //if this is actually different from the current state, act on it
        {
            LocalOverride.RemoteComponent.SetState(feature, enableState);
            FeatureOverrideChange?.Invoke(feature);
        }

        return enableState;
    }

    public override HierarchicalEnableState GetState(HierarchicalStates feature) => LocalOverride.RemoteComponent.GetState(feature);

    public override void ToggleHidden() => ToggleState(HierarchicalStates.Hidden);

    public override void ToggleSelect() => ToggleState(HierarchicalStates.Selected);

    public override void ToggleSeeThrough() => ToggleState(HierarchicalStates.SeeThrough);

    public override void ToggleTint(Color tintColor = default)
    {
        if (tintColor != default) LocalOverride.RemoteComponent.TintColor = tintColor.toRemote();
        ToggleState(HierarchicalStates.UseTintColor);
    }

    public override void ToggleDisabledCollision() => ToggleState(HierarchicalStates.DisableCollision);

    public override void RemoveOverride()
    {
        var remoteStateOverride = TargetEntity.Entity.FindComponentOfType<HierarchicalStateOverrideComponent>();
        if (remoteStateOverride != null)
        {
            remoteStateOverride.Destroy();
        }

        if (localOverride == null)
            localOverride = gameObject.GetComponent<ARRHierarchicalStateOverrideComponent>();

        if (localOverride != null)
        {
            Destroy(localOverride);
        }
    }
}
