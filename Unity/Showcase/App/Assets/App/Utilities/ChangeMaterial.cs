// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.Input;
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using Remote = Microsoft.Azure.RemoteRendering;

/// <summary>
/// A class for changing remote materials.
/// </summary>
public class ChangeMaterial : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    [Tooltip("The remote material to apply.")]
    private RemoteMaterial material;

    /// <summary>
    /// The remote material to apply.
    /// </summary>
    public RemoteMaterial Material
    {
        get => material;
        set
        {
            if (material != value)
            {
                material = value;
                PreloadMaterial(material);
            }
        }
    }

    [Header("Events")]

    [SerializeField]
    [Tooltip("Event raised when a new material is applied to an entity.")]
    private UnityEvent<Entity, RemoteMaterial> materialApplied = new RemoteMaterialEvent();

    /// <summary>
    /// Event raised when a new material is applied to an entity.
    /// </summary>
    public UnityEvent<Entity, RemoteMaterial> MaterialApplied => materialApplied;
    #endregion Serialized Fields

    #region MonoBehavior Methods
    /// <summary>
    /// Preload the current material, so setting it is a little faster.
    /// </summary>
    private void Start()
    {
        PreloadMaterial(material);
    }
    #endregion MonoBehavior Methods

    #region Public Methods
    /// <summary>
    /// An event handler for PointerModeAndResponse.EnabledEvent
    /// </summary>
    public void SetMaterial(PointerMode pointerMode, object newMaterial)
    {
        SetMaterial(newMaterial);
    }

    /// <summary>
    /// Set the material, if the given value is a valid material
    /// </summary>
    public void SetMaterial(object newMaterial)
    {
        switch (newMaterial)
        {
            case RemoteMaterial remoteMaterial:
                Material = remoteMaterial;
                break;
            case RemoteMaterialObject remoteMaterialObject:
                Material = remoteMaterialObject?.Data;
                break;
        }
    }

    /// <summary>
    /// An event handler for PointerModeAndResponse.ClickedEvent. The pointer's target will have its material changed.
    /// </summary>
    public async void StartApplying(MixedRealityPointerEventData eventData, object newMaterial)
    {
        await Apply(eventData, newMaterial);
    }

    /// <summary>
    /// An event handler for pointer click events. The pointer's target will have its material changed.
    /// </summary>
    public async void StartApplying(MixedRealityPointerEventData eventData)
    {
        await Apply(eventData);
    }

    public Task Apply(MixedRealityPointerEventData eventData, object newMaterial)
    {
        SetMaterial(newMaterial);
        return Apply(eventData);
    }

    public Task Apply(MixedRealityPointerEventData eventData)
    {
        IRemotePointerResult pointerResult = AppServices.RemoteFocusProvider?.GetRemoteResult(eventData.Pointer);
        return Apply(pointerResult.TargetEntity);
    }

    public Task Apply(Entity entity, RemoteMaterial remoteMaterial)
    {
        if (remoteMaterial == null)
        {
            return Task.CompletedTask;
        }

        SetMaterial(remoteMaterial);
        return Apply(entity);
    }

    public async Task Apply(Entity entity)
    {
        IRemoteRenderingMachine machine = AppServices.RemoteRendering.PrimaryMachine;
        if (material == null || machine == null || entity == null || !entity.Valid)
        {
            return;
        }

        Task<Remote.Material> materialTask = machine.Actions.LoadMaterial(material);
        materialApplied?.Invoke(entity, material);

        Remote.Material loadedMaterial = await materialTask;
        if (loadedMaterial != null)
        {
            entity.ReplaceMaterials(loadedMaterial);
        }
    }
    #endregion Public Methods

    #region Private Methods
    /// <summary>
    /// Improve the speed at which materials are loaded, by preloading the material.
    /// </summary>
    private async void PreloadMaterial(RemoteMaterial loadMaterial)
    {
        if (loadMaterial == null)
        {
            return;
        }

        IRemoteRenderingMachine machine = AppServices.RemoteRendering?.PrimaryMachine;
        if (machine == null || !machine.Actions.IsValid())
        {
            return;
        }

        try
        {
            await machine.Actions.LoadMaterial(loadMaterial);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load material '{loadMaterial.Name}'.\r\nException: {ex.ToString()}");
        }
    }
    #endregion Private Method

    #region Private Classes
    [Serializable]
    private class RemoteMaterialEvent : UnityEvent<Entity, RemoteMaterial>
    { }
    #endregion Private Classes

}
