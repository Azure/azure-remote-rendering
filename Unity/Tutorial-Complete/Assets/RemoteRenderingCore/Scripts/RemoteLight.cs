// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.Azure.RemoteRendering.Unity;
using System;
using UnityEngine;

[RequireComponent(typeof(Light))]
public class RemoteLight : BaseRemoteLight
{
    public bool AutomaticallyCreate = true;

    private bool lightReady = false;
    public override bool LightReady
    {
        get => lightReady;
        set
        {
            lightReady = value;
            LightReadyChanged?.Invoke(lightReady);
        }
    }

    private ObjectType remoteLightType = ObjectType.Invalid;
    public override ObjectType RemoteLightType => remoteLightType;

    public UnityBoolEvent OnLightReadyChanged;

    public override event Action<bool> LightReadyChanged;

    private Light localLight; //Unity Light

    private Entity lightEntity;
    private LightComponentBase remoteLightComponent; //Remote Rendering Light

    private void Awake()
    {
        localLight = GetComponent<Light>();
        switch (localLight.type)
        {
            case LightType.Directional:
                remoteLightType = ObjectType.DirectionalLightComponent;
                break;
            case LightType.Point:
                remoteLightType = ObjectType.PointLightComponent;
                break;
            case LightType.Spot:
            case LightType.Area:
                //Not supported in tutorial
            case LightType.Disc:
                // No direct analog in remote rendering
                remoteLightType = ObjectType.Invalid;
                break;
        }
    }

    public void Start()
    {
        // Hook up the event to the Unity event
        LightReadyChanged += (ready) => OnLightReadyChanged?.Invoke(ready);

        RemoteRenderingCoordinator.CoordinatorStateChange += RemoteRenderingCoordinator_CoordinatorStateChange;
        RemoteRenderingCoordinator_CoordinatorStateChange(RemoteRenderingCoordinator.instance.CurrentCoordinatorState);
    }

    public void OnDestroy()
    {
        RemoteRenderingCoordinator.CoordinatorStateChange -= RemoteRenderingCoordinator_CoordinatorStateChange;
        lightEntity?.Destroy();
    }

    private void RemoteRenderingCoordinator_CoordinatorStateChange(RemoteRenderingCoordinator.RemoteRenderingState state)
    {
        switch (state)
        {
            case RemoteRenderingCoordinator.RemoteRenderingState.RuntimeConnected:
                if (AutomaticallyCreate)
                    CreateLight();
                break;
            default:
                DestroyLight();
                break;
        }
    }

    public override void CreateLight()
    {
        if (remoteLightComponent != null)
            return; //Nothing to do!

        //Create a root object for the light
        if (lightEntity == null)
            lightEntity = RemoteRenderingCoordinator.CurrentSession.Connection.CreateEntity();

        //Bind the remote entity to this game object
        lightEntity.BindToUnityGameObject(this.gameObject);

        //Sync the transform of this object so we can move the light
        var syncComponent = this.gameObject.GetComponent<RemoteEntitySyncObject>();
        syncComponent.SyncEveryFrame = true;

        //Add a light to the entity
        switch (RemoteLightType)
        {
            case ObjectType.DirectionalLightComponent:
                var remoteDirectional = RemoteRenderingCoordinator.CurrentSession.Connection.CreateComponent(ObjectType.DirectionalLightComponent, lightEntity) as DirectionalLightComponent;
                //No additional properties
                remoteLightComponent = remoteDirectional;
                break;

            case ObjectType.PointLightComponent:
                var remotePoint = RemoteRenderingCoordinator.CurrentSession.Connection.CreateComponent(ObjectType.PointLightComponent, lightEntity) as PointLightComponent;
                remotePoint.Radius = 0;
                remotePoint.Length = localLight.range;
                //remotePoint.AttenuationCutoff = //No direct analog in Unity legacy lights
                //remotePoint.ProjectedCubeMap = //No direct analog in Unity legacy lights

                remoteLightComponent = remotePoint;
                break;
            default:
                LightReady = false;
                return;
        }

        // Set the common values for all light types
        UpdateRemoteLightSettings();

        LightReady = true;
    }

    public override void UpdateRemoteLightSettings()
    {
        if (remoteLightComponent != null)
        {
            remoteLightComponent.Color = localLight.color.toRemote();
            remoteLightComponent.Intensity = localLight.intensity;
        }
    }

    public override void DestroyLight()
    {
        if (remoteLightComponent == null)
            return; //Nothing to do!

        if (RemoteRenderingCoordinator.instance.CurrentCoordinatorState == RemoteRenderingCoordinator.RemoteRenderingState.RuntimeConnected)
        {
            remoteLightComponent.Destroy();
            lightEntity.Destroy();
        }
        lightEntity = null;
        remoteLightComponent = null;
        LightReady = false;
    }

    [ContextMenu("Sync Remote Light Configuration")]
    public override void RecreateLight()
    {
        DestroyLight();
        CreateLight();
    }

    public override void SetIntensity(float intensity)
    {
        localLight.intensity = Mathf.Clamp(intensity, 0, 1);
        UpdateRemoteLightSettings();
    }

    public override void SetColor(Color color)
    {
        localLight.color = color;
        UpdateRemoteLightSettings();
    }
}
