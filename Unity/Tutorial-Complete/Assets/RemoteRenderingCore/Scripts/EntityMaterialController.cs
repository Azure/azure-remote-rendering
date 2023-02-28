// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.Azure.RemoteRendering.Unity;
using System;
using System.Linq;
using UnityEngine;
// to prevent namespace conflicts
using ARRMaterial = Microsoft.Azure.RemoteRendering.Material;

public class EntityMaterialController : BaseEntityMaterialController
{
    public override bool RevertOnEntityChange { get; set; } = true;

    public override OverrideMaterialProperty<Color> ColorOverride { get; set; }
    public override OverrideMaterialProperty<float> RoughnessOverride { get; set; }
    public override OverrideMaterialProperty<float> MetalnessOverride { get; set; }

    private Entity targetEntity;
    public override Entity TargetEntity
    {
        get => targetEntity;
        set
        {
            if (targetEntity != value)
            {
                if (targetEntity != null && RevertOnEntityChange)
                {
                    Revert();
                }

                targetEntity = value;
                ConfigureTargetEntity();
                TargetEntityChanged?.Invoke(value);
            }
        }
    }

    private ARRMaterial targetMaterial;
    private ARRMeshComponent meshComponent;

    public override event Action<Entity> TargetEntityChanged;
    public UnityRemoteEntityEvent OnTargetEntityChanged;

    public void Start()
    {
        // Forward events to Unity events
        TargetEntityChanged += (entity) => OnTargetEntityChanged?.Invoke(entity);

        // If there happens to be a remote RayCaster on this object, assume we should listen for events from it
        if (GetComponent<BaseRemoteRayCastPointerHandler>() != null)
            GetComponent<BaseRemoteRayCastPointerHandler>().RemoteEntityClicked += (entity) => TargetEntity = entity;
    }

    protected override void ConfigureTargetEntity()
    {
        //Get the Unity object, to get the sync object, to get the mesh component, to get the material.
        var targetEntityGameObject = TargetEntity.GetOrCreateGameObject(UnityCreationMode.DoNotCreateUnityComponents);

        var localSyncObject = targetEntityGameObject.GetComponent<RemoteEntitySyncObject>();
        meshComponent = targetEntityGameObject.GetComponent<ARRMeshComponent>();
        if (meshComponent == null)
        {
            var mesh = localSyncObject.Entity.FindComponentOfType<MeshComponent>();
            if (mesh != null)
            {
                targetEntityGameObject.BindArrComponent<ARRMeshComponent>(mesh);
                meshComponent = targetEntityGameObject.GetComponent<ARRMeshComponent>();
            }
        }

        meshComponent.enabled = true;

        targetMaterial = meshComponent.RemoteComponent.Mesh.Materials.FirstOrDefault();
        if (targetMaterial == default)
        {
            return;
        }

        ColorOverride = new OverrideMaterialProperty<Color>(
            GetMaterialColor(targetMaterial), //The original value
            targetMaterial, //The target material
            ApplyMaterialColor); //The action to take to apply the override

        //If the material is a PBR material, we can override some additional values
        if (targetMaterial.MaterialSubType == MaterialType.Pbr)
        {
            var firstPBRMaterial = (PbrMaterial)targetMaterial;

            RoughnessOverride = new OverrideMaterialProperty<float>(
                firstPBRMaterial.Roughness, //The original value
                targetMaterial, //The target material
                ApplyRoughnessValue); //The action to take to apply the override

            MetalnessOverride = new OverrideMaterialProperty<float>(
                firstPBRMaterial.Metalness, //The original value
                targetMaterial, //The target material
                ApplyMetalnessValue); //The action to take to apply the override
        }
        else //otherwise, ensure the overrides are cleared out from any previous entity
        {
            RoughnessOverride = null;
            MetalnessOverride = null;
        }
    }

    public override void Revert()
    {
        if (ColorOverride != null)
            ColorOverride.OverrideActive = false;

        if (RoughnessOverride != null)
            RoughnessOverride.OverrideActive = false;

        if (MetalnessOverride != null)
            MetalnessOverride.OverrideActive = false;
    }

    private Color GetMaterialColor(ARRMaterial material)
    {
        if (material == null)
            return default;

        if (material.MaterialSubType == MaterialType.Color)
            return ((ColorMaterial)material).AlbedoColor.toUnity();
        else
            return ((PbrMaterial)material).AlbedoColor.toUnity();
    }

    private void ApplyMaterialColor(ARRMaterial material, Color color)
    {
        if (material == null)
            return;

        if (material.MaterialSubType == MaterialType.Color)
            ((ColorMaterial)material).AlbedoColor = color.toRemoteColor4();
        else
            ((PbrMaterial)material).AlbedoColor = color.toRemoteColor4();
    }

    private void ApplyRoughnessValue(ARRMaterial material, float value)
    {
        if (material == null)
            return;

        if (material.MaterialSubType == MaterialType.Pbr) //Only PBR has Roughness
            ((PbrMaterial)material).Roughness = value;
    }

    private void ApplyMetalnessValue(ARRMaterial material, float value)
    {
        if (material == null)
            return;

        if (material.MaterialSubType == MaterialType.Pbr) //Only PBR has Metalness
            ((PbrMaterial)material).Metalness = value;
    }
}