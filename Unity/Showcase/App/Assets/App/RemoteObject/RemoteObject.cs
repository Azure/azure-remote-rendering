// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.Azure.RemoteRendering.Unity;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Physics;
using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

public class RemoteObject : MonoBehaviour
{
    private TaskCompletionSource<bool> _loadingTaskSource;
    private bool _visibleModel = true;
    private bool _enabled = true;
    private bool _started = false;
    private List<EntitySnapshot> _transformSnapshot = null;

    #region Serialized Fields
    [Header("General Settings")]

    [SerializeField]
    [Tooltip("The remote data of the asset")]
    private RemoteItemBase data = null;

    /// <summary>
    /// The remote data of the asset
    /// </summary>
    public RemoteItemBase Data
    {
        get => data;
        set
        {
            if (data != value)
            {
                Unload(true);
                data = value;
                Load();

                if (_started)
                {
                    dataChanged?.Invoke(new RemoteObjectDataChangedEventData(this, data));
                }
            }
        }
    }

    [SerializeField]
    [Tooltip("This is the root transform where all dynamically create objects will be placed.")]
    private Transform root = null;

    /// <summary>
    /// This is the root transform where all dynamically create objects will be placed.
    /// </summary>
    public Transform Root
    {
        get => root;
        set => root = value;
    }
 
    [SerializeField]
    [Tooltip("Set to true if this object should be placed on the ground. This transform's position is used as the ground point.")]
    private bool grounded = false;

    /// <summary>
    /// Set to true if this object should be placed on the ground. This transform's position is used as the ground point.
    /// </summary>
    public bool Grounded
    {
        get => grounded;
        set => grounded = value;
    }

    [SerializeField]
    [Tooltip("The placeholder to show while loading an object.")]
    private Transform placeholder = null;

    /// <summary>
    /// The placeholder to show while loading an object.
    /// </summary>
    public Transform Placeholder
    {
        get => placeholder;
        set => placeholder = value;
    }

    [SerializeField]
    [PhysicsLayer]
    [Tooltip("The layer to apply to the loaded objects that are interactable.")]
    private int interactionLayer = -1;

    /// <summary>
    /// The layer to apply to the loaded objects that are interactable.
    /// </summary>
    public int InteractionLayer
    {
        get => interactionLayer;
        set => interactionLayer = value;
    }

    [Header("Events")]

    [SerializeField]
    [Tooltip("Invoked when model data has changed.")]
    private RemoteObjectDataChangedEvent dataChanged = new RemoteObjectDataChangedEvent();

    /// <summary>
    /// Invoked when model data has changed.
    /// </summary>
    public RemoteObjectDataChangedEvent DataChanged => dataChanged;

    [SerializeField]
    [Tooltip("Invoked when model is loaded")]
    private RemoteObjectLoadedEvent loaded = new RemoteObjectLoadedEvent();

    /// <summary>
    /// Invoked when model is loaded.
    /// </summary>
    public RemoteObjectLoadedEvent Loaded => loaded;

    [SerializeField]
    [Tooltip("Invoked when the user deletes the object.")]
    private RemoteObjectDeletedEvent deleted = new RemoteObjectDeletedEvent();

    /// <summary>
    /// Invoked when the user deletes the object.
    /// </summary>
    public RemoteObjectDeletedEvent Deleted => deleted;

    [SerializeField]
    [Tooltip("Event raised when remote object is disabled.")]
    private UnityEvent objectDisabled = new UnityEvent();

    /// <summary>
    /// Event raised when remote object is disabled.
    /// </summary>
    public UnityEvent ObjectDisabled => objectDisabled;

    [SerializeField]
    [Tooltip("Event raised when the remote object has been enabled or disabled.")]
    private UnityEvent<bool> isEnabledChanged = new RemoteObjectEnabledChangedEvent();

    /// <summary>
    /// Event raised when the remote object has been enabled or disabled.
    /// </summary>
    public UnityEvent<bool> IsEnabledChanged => isEnabledChanged;
    #endregion Serialized Fields

    #region Public Properties
    /// <summary>
    /// Get or set the primary machine to use for loading
    /// </summary>
    public IRemoteRenderingMachine PrimaryMachine { get; set; }
    
    /// <summary>
    /// Get or set if the remote model is visible. If hidden, the placeholder will be shown
    /// </summary>
    public bool IsVisible
    {
        get => _visibleModel;

        set
        {
            _visibleModel = value;
            UpdatePlaceholderVisibility();
            UpdateContainerVisibility(Data, MainContainer);
        }
    }

    /// <summary>
    /// Set or set if the placeholder can be shown.
    /// </summary>
    public bool IsEnabled
    {
        get => _enabled;

        set
        {
            if (_enabled != value)
            {
                _enabled = value;
                UpdatePlaceholderVisibility();
                UpdateContainerVisibility(Data, MainContainer);
                isEnabledChanged?.Invoke(value);
            }
        }
    }

    /// <summary>
    /// Get if the remote entity has loaded
    /// </summary>
    public bool IsLoaded => RemoteContainer?.Entity != null && !IsLoading;

    /// <summary>
    /// Get if the remote entity is loading
    /// </summary>
    public bool IsLoading => _loadingTaskSource != null && !_loadingTaskSource.Task.IsCompleted;
 
    /// <summary>
    /// Get the currently loaded remote entity
    /// </summary>
    public Entity Entity { get => RemoteContainer?.Entity; }

    /// <summary>
    /// A snapshot of the a loaded model's entity transforms. Used as a base for animations like explode.
    /// </summary>
    public List<EntitySnapshot> TransformSnapshot
    {
        get => _transformSnapshot;
        set => _transformSnapshot = value;
    }

    #endregion Public Properties

    #region Private Properties
    /// <summary>
    /// Get the sync object the holds child models.
    /// </summary>
    private RemoteEntitySyncObject RemoteContainer { get; set; }

    /// <summary>
    /// The container for local placeholder models.
    /// </summary>
    private Transform LocalContainer { get; set; }

    /// <summary>
    /// The target of the RemoteItemBase.Transform changes.
    /// </summary>
    private Transform MainContainer { get; set; }
    #endregion Private Properties

    #region MonoBehavior Methods
    private void Start()
    {
        if (Root == null)
        {
            Root = transform;
        }

        if (AppServices.RemoteRendering != null)
        {
            AppServices.RemoteRendering.StatusChanged += 
                RemoteRendering_StatusChanged;
        }

        _started = true;
        Load();

        if (data != null)
        {
            dataChanged?.Invoke(new RemoteObjectDataChangedEventData(this, data));
        }
    }

    private void OnEnable()
    {
        Load();
    }

    private void OnDestroy()
    {
        if (AppServices.RemoteRendering != null)
        {
            AppServices.RemoteRendering.StatusChanged -=
                RemoteRendering_StatusChanged;
        }

        var oldLoadTaskSource = _loadingTaskSource;
        _loadingTaskSource = null;
        oldLoadTaskSource?.TrySetResult(false);
        
        deleted?.Invoke(new RemoteObjectDeletedEventData(this));
    }
    #endregion MonoBehavior Methods

    #region Private Methods
    private void RemoteRendering_StatusChanged(object sender, IRemoteRenderingStatusChangedArgs status)
    {
        if (status.NewStatus == RemoteRenderingServiceStatus.SessionReadyAndConnected)
        {
            Reload();
        }
        else if (status.OldStatus == RemoteRenderingServiceStatus.SessionReadyAndConnected)
        {
            Unload();
        }
    }

    private void Reload()
    {
        Unload();
        Load();
    }

    private async void Load()
    {
        var machine = PrimaryMachine ?? AppServices.RemoteRendering?.PrimaryMachine;
        var item = data;

        // Validate state
        if ((IsLoading) ||
            (IsLoaded) ||
            (item == null) ||
            (!isActiveAndEnabled) ||
            (!_started))
        {
            return;
        }

        // Cache the task source for validating loading status after async operations
        Debug.Assert(_loadingTaskSource == null, "There shouldn't be an active loading task.");
        var loadingTaskSource = _loadingTaskSource = new TaskCompletionSource<bool>();

        // Show placeholder visual
        UpdatePlaceholderVisibility();

        try
        {
            await LoadWorker(machine, item, loadingTaskSource.Task);
        }
        catch (Exception ex)
        {
            var msg = $"Error loading model: {ex.Message}";
            AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
            UnityEngine.Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  msg);
        }

        // If still loading, continue
        if (!loadingTaskSource.Task.IsCompleted)
        {
            // Notify listeners that load has completed
            loadingTaskSource.TrySetResult(true);

            // Hide placeholder
            UpdatePlaceholderVisibility();

            // Show container object
            UpdateContainerVisibility(Data, MainContainer);

            // Raise event if loaded
            if (IsLoaded)
            {
                loaded?.Invoke(new RemoteObjectLoadedEventData(RemoteContainer, _transformSnapshot));
            }
        }
    }

    private async Task LoadWorker(IRemoteRenderingMachine machine, RemoteItemBase item, Task loadingTask)
    {
        // Is this item showable?
        IsEnabled = item.Enabled;

        // Set local name first
        name = GetName(item);

        // Create the transform that will be the target to item.Transform data.
        CreateMainContainer(item);

        // Create a new local container, if needed.
        Transform localContainer = null;
        if (LocalContainer == null)
        {
            localContainer = CreateLocalContainer(MainContainer);
        }

        // Load remote model children
        await LoadLocalModels(item, localContainer);

        // If not loading anymore, exit early.
        if (loadingTask.IsCompleted)
        {
            DestroyLocalContainer(localContainer);
            return;
        }

        // If local container is empty, destroy it.
        if (localContainer != null && localContainer.transform.childCount == 0)
        {
            DestroyLocalContainer(localContainer);
            localContainer = null;
        }

        // Commit local container early, set it's transforms. This way the user sees the local model while the remote
        // model is loading.
        if (LocalContainer == null && localContainer != null)
        {
            var containerBounds = await GetBounds(localContainer); 
            SetColliderBounds(localContainer, containerBounds);
            ApplyTransform(item, localContainer, containerBounds);
            MoveContainerToGround(localContainer, containerBounds);

            LocalContainer = localContainer;
            UpdatePlaceholderVisibility();
        }

        // Create a remote container
        RemoteEntitySyncObject remoteContainer = CreateRemoteContainer(machine, MainContainer);

        // Hide remote container while loading
        UpdateContainerVisibility(item, MainContainer);

        // Load remote model children
        IList<LoadModelResult> loadedRemoteModels = null;
        if (remoteContainer != null)
        {
            loadedRemoteModels = await LoadRemoteModels(machine, item, remoteContainer.Entity);
        }

        // If not loading anymore, destroy remote container and exit early
        if (loadingTask.IsCompleted)
        {
            DestroyRemoteContainer(remoteContainer);
            return;
        }

        // If remote container is empty, destroy it
        if (remoteContainer != null && remoteContainer.transform.childCount == 0)
        {
            DestroyRemoteContainer(remoteContainer);
            remoteContainer = null;
        }

        // If there's a remote container, set it's transforms
        if (remoteContainer != null)
        {
            remoteContainer.SyncToRemote();

            var containerBounds = await GetBounds(remoteContainer); 
            SetColliderBounds(remoteContainer, containerBounds);
            ApplyTransform(item, remoteContainer, containerBounds);
            MoveContainerToGround(remoteContainer, containerBounds);
        }

        // Sync now and save initiali positions of the remote models
        if (loadedRemoteModels != null && remoteContainer != null)
        {
            remoteContainer.SyncToRemote();

            await LoadRemoteSnapshot(loadedRemoteModels, remoteContainer.Entity);
        }

        // If not loading exit
        if (loadingTask.IsCompleted)
        {
            DestroyRemoteContainer(remoteContainer);
            return;
        }

        // Load lights children
        if (remoteContainer != null)
        {
            await LoadRemoteLights(machine, item, remoteContainer.Entity);
        }

        // If not loading exit
        if (loadingTask.IsCompleted)
        {
            DestroyRemoteContainer(remoteContainer);
            return;
        }

        // Commit the containers if done loading
        RemoteContainer = remoteContainer;
    }

    /// <summary>
    /// Unload the currently loaded item.
    /// </summary>
    private void Unload(bool unloadLocal = false)
    {
        // Destory model and container sync entities
        DestroyRemoteContainer(RemoteContainer);
        RemoteContainer = null;

        // Destroy local stuff if need. Typically this is only done id model data changed
        if (unloadLocal)
        {
            DestroyLocalContainer(LocalContainer);
            LocalContainer = null;
        }

        // Create a new task for the next load
        var oldLoadingTaskSource = _loadingTaskSource;
        _loadingTaskSource = null;

        // Show placeholder visual
        UpdatePlaceholderVisibility();

        UpdateContainerVisibility(data, MainContainer);

        // Notify listeners that load has failed, so they can listen for the new load
        oldLoadingTaskSource?.TrySetResult(false); 
    }

    private async Task LoadLocalModels(RemoteItemBase model, Transform localParent)
    {
        RemoteContainer modelWithChildren = model as RemoteContainer;
        if (modelWithChildren == null || localParent == null)
        {
            return;
        }

        // Load children
        List<Task<GameObject>> loadingItems = new List<Task<GameObject>>();
        if (modelWithChildren.Items != null)
        {
            foreach (var item in modelWithChildren.Items)
            {
                if (item is RemotePlaceholderModel)
                {
                    loadingItems.Add(LoadLocalModel((RemotePlaceholderModel)item, localParent));
                }
            }
        }

        // Wait for the items to finish loading
        if (loadingItems.Count > 0)
        {
            await Task.WhenAll(loadingItems);
        }
    }

    private async Task<GameObject> LoadLocalModel(RemotePlaceholderModel model, Transform localParent)
    {
        if (model == null || localParent == null)
        {
            return null;
        }

        GameObject prefab = null;
        try
        {
            prefab = await BundleLoader.LoadModel(model.Url, model.AssetName);
        }
        catch (Exception ex)
        {
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  $"Failed to load local model '{model.AssetName}' from '{model.Url}'. Reason: {ex.Message}");
        }

        GameObject localModel = null;
        if (prefab != null)
        {
            localModel = GameObject.Instantiate(prefab, localParent);
            localModel.name = model.Name;

            // Create a collider so we can interact with this object
            MakePiecesInteractable(localModel.GetComponentsInChildren<MeshRenderer>());

            // Apply transform to this object
            UnityEngine.Bounds modelBounds = await GetBoundsAsNeeded(model, localModel.transform);
            ApplyTransform(model, localModel.transform, modelBounds);
        }

        return localModel;
    }

    /// <summary>
    /// Make the given mesh renderers interactable. This way the locally rendered pieces can be manipulated.
    /// </summary>
    private void MakePiecesInteractable(MeshRenderer[] meshes)
    {
        if (meshes == null || meshes.Length == 0)
        {
            return;
        }

        int count = meshes.Length;
        for (int i = 0; i < count; i++)
        {
            GameObject meshObject = meshes[i].gameObject;
            ApplyInteractionLayer(meshObject.gameObject);
            meshObject.EnsureComponent<BoxCollider>();
            meshObject.EnsureComponent<NearInteractionGrabbable>();
        }
    }

    private async Task<IList<LoadModelResult>> LoadRemoteModels(IRemoteRenderingMachine machine, RemoteItemBase model, Entity remoteParent)
    {
        RemoteContainer modelWithChildren = model as RemoteContainer;
        if (modelWithChildren == null || remoteParent == null)
        {
            return new List<LoadModelResult>();
        }

        // Load children
        List<Task<LoadModelResult>> loadingItems = new List<Task<LoadModelResult>>();
        if (modelWithChildren.Items != null)
        {
            foreach (var item in modelWithChildren.Items)
            {
                if (item is RemoteModel)
                {
                    loadingItems.Add(LoadRemoteModel(machine, (RemoteModel)item, remoteParent));
                }
            }
        }
        // Wait for the items to finish loading
        if (loadingItems.Count > 0)
        {
            return await Task.WhenAll(loadingItems);
        }
        else
        {
            return new List<LoadModelResult>();
        }
    }

    /// <summary>
    /// Save the initial transforms of the remote entity.  
    /// </summary>
    private async Task LoadRemoteSnapshot(IList<LoadModelResult> loadedRemoteModels, Entity remoteParent)
    {
        _transformSnapshot = null;
        if (loadedRemoteModels == null || remoteParent == null)
        {
            return;
        }

        var remoteParentSnapshot = remoteParent.CreateSnapshot();
        List<Task<List<EntitySnapshot>>> snapshotItems = new List<Task<List<EntitySnapshot>>>();
        foreach (LoadModelResult result in loadedRemoteModels)
        {
            if (result != null)
            {
                snapshotItems.Add(CreateTransformSnapshot(result, remoteParentSnapshot));
            }
        }

        var snapshots = await Task.WhenAll(snapshotItems);
        // one slot for root snapshot
        var capacity = 1;
        foreach (List<EntitySnapshot> snapshotList in snapshots)
        {
            capacity += snapshotList.Count;
        }
        var transformSnapshot = new List<EntitySnapshot>(capacity);
        foreach (List<EntitySnapshot> snapshotList in snapshots)
        {
            transformSnapshot.AddRange(snapshotList);
        }
        _transformSnapshot = transformSnapshot;
    }

    private Task<List<EntitySnapshot>> CreateTransformSnapshot(LoadModelResult result, EntitySnapshot parent)
    {
        // run calculations on background thread
        return Task.Run(() =>
        {
            var entities = result.GetLoadedObjectsOfType(ObjectType.Entity);
            var entityCount = entities.Count;
            var snapshotList = new List<EntitySnapshot>(entityCount);
            var entityToEntitySnapshot = new Dictionary<Entity, EntitySnapshot>(parent != null ? entityCount + 1 : entityCount);
            if (parent != null)
            {
                entityToEntitySnapshot[parent.Entity] = parent;
            }

            foreach (var e in entities)
            {
                var current = e as Entity;
                var currentParent = entityToEntitySnapshot[current.Parent];
                var entitySnapshot = new EntitySnapshot(current, currentParent);
                entityToEntitySnapshot[current] = entitySnapshot;
                snapshotList.Add(entitySnapshot);
            }
            return snapshotList;
        });
    }

    private async Task<LoadModelResult> LoadRemoteModel(IRemoteRenderingMachine machine, RemoteModel model, Entity remoteParent)
    {
        if (machine == null || model == null || remoteParent == null)
        {
            return null;
        }

        Task<LoadModelResult> loadTask;
        var remoteObjectFactoryService = MixedRealityToolkit.Instance.GetService<IRemoteObjectFactoryService>();

        if (remoteObjectFactoryService != null)
        {
            loadTask = remoteObjectFactoryService.Load(model, remoteParent);
        }
        else
        {
            loadTask = Task.FromResult<LoadModelResult>(null);
        }

        LoadModelResult result = null;
        try
        {
            result = await loadTask;
            UnityEngine.Debug.Assert(result.Root != null, $"Failed to load model, invalid id returned. ({model.Url})");
        }
        catch (Exception ex)
        {
            var msg = $"Failed to load model from '{model.Url}'. Reason: {ex.Message}";
            AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
            msg += "\r\nTo troubleshoot refer to: https://docs.microsoft.com/azure/remote-rendering/resources/troubleshoot#failed-to-load-model.";
            UnityEngine.Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  msg);
        }

        // Apply item's transform
        Entity entity = result?.Root;
        if (entity != null && entity.Valid)
        {
            GameObject entityGameObject = entity.GetOrCreateGameObject(UnityCreationMode.DoNotCreateUnityComponents);
            UnityEngine.Bounds entityBounds = await GetBoundsAsNeeded(model, entityGameObject.transform);
            ApplyTransform(model, entityGameObject.transform, entityBounds);
            entityGameObject.GetComponent<RemoteEntitySyncObject>()?.SyncToRemote();
        }

        return result;
    }

    private async Task LoadRemoteLights(IRemoteRenderingMachine machine, RemoteItemBase model, Entity remoteParent)
    {
        RemoteContainer modelWithChildren = model as RemoteContainer;
        if (modelWithChildren == null || remoteParent == null || !remoteParent.Valid)
        {
            return;
        }

        // Load children
        List<Task<Entity>> loadingItems = new List<Task<Entity>>();
        if (modelWithChildren.Items != null)
        {
            foreach (var item in modelWithChildren.Items)
            {
                if (item is RemoteLight)
                {
                    loadingItems.Add(LoadRemoteLight(machine, (RemoteLight)item, remoteParent));
                }
            }
        }

        // Wait for the items to finish loading
        if (loadingItems.Count > 0)
        {
            await Task.WhenAll(loadingItems);
        }
    }

    private async Task<Entity> LoadRemoteLight(IRemoteRenderingMachine machine, RemoteLight light, Entity remoteParent)
    {
        if (machine == null || light == null || remoteParent == null || !remoteParent.Valid)
        {
            return null;
        }

        RemoteEntitySyncObject result = null;

        // Create an entity that will host the light source
        Entity lightEntity = machine.Actions.CreateEntity();
        lightEntity.Parent = remoteParent;

        // Add light component
        await AddRemoteLight(machine, light, lightEntity);

        // Ensure entity is still valid
        if (lightEntity.Valid)
        {
            // Create unity wrapper
            result = lightEntity.GetOrCreateGameObject(
                UnityCreationMode.CreateUnityComponents).GetComponent<RemoteEntitySyncObject>();

            // Name light
            result.gameObject.name = $"{GetName(light)} Light";

            // Apply transform
            ApplyTransform(light, result.transform, LocalBounds.Zero);

            // Sync 
            result.SyncToRemote();
            result.SyncEveryFrame = true;
        }

        return result.Entity;
    }

    private static async Task<LightComponentBase> AddRemoteLight(IRemoteRenderingMachine machine, RemoteLight light, Entity remoteParent)
    {
        LightComponentBase result = null;
        if (light is RemotePointLight)
        {
            result = await AddRemoteLight(machine, (RemotePointLight)light, remoteParent);
        }
        else if (light is RemoteDirectionalLight)
        {
            result = await AddRemoteLight(machine, (RemoteDirectionalLight)light, remoteParent);
        }
        else if (light is RemoteSpotlight)
        {
            result = await AddRemoteLight(machine, (RemoteSpotlight)light, remoteParent);
        }

        if (result != null)
        {
            result.Color = light.Color.toRemote();
            result.Intensity = light.Intensity;
        }

        return result;

    }
    private static Task<LightComponentBase> AddRemoteLight(IRemoteRenderingMachine machine, RemoteDirectionalLight light, Entity remoteParent)
    {
        return Task.FromResult<LightComponentBase>(remoteParent.EnsureComponentOfType<DirectionalLightComponent>());
    }

    private static async Task<LightComponentBase> AddRemoteLight(IRemoteRenderingMachine machine, RemotePointLight light, Entity remoteParent)
    {
        PointLightComponent result = null;

        // first grab texture
        Microsoft.Azure.RemoteRendering.Texture cubeMap = null;
        if (!string.IsNullOrEmpty(light.ProjectedCubeMap))
        {
            cubeMap = await machine.Actions.LoadTextureCubeMap(light.ProjectedCubeMap);
        }

        // ensure parent is still valid
        if (remoteParent.Valid)
        {
            result = remoteParent.EnsureComponentOfType<PointLightComponent>();
            result.AttenuationCutoff = light.AttenuationCutoff.toRemote();
            result.Length = light.Length;
            result.Radius = light.Radius;
            result.ProjectedCubeMap = cubeMap;
        }

        return result;
    }

    private static async Task<LightComponentBase> AddRemoteLight(IRemoteRenderingMachine machine, RemoteSpotlight light, Entity remoteParent)
    {
        SpotLightComponent result = null;

        // first grab texture
        Microsoft.Azure.RemoteRendering.Texture texture = null;
        if (!string.IsNullOrEmpty(light.Projected2DTexture))
        {
            texture = await machine.Actions.LoadTexture2D(light.Projected2DTexture);
        }

        // ensure parent is still valid
        if (remoteParent.Valid)
        {
            result = remoteParent.EnsureComponentOfType<SpotLightComponent>();
            result.AttenuationCutoff = light.AttenuationCutoff.toRemote();
            result.SpotAngleDeg = light.Angle.toRemote();
            result.FalloffExponent = light.Falloff;
            result.Radius = light.Radius;
            result.Projected2dTexture = texture;
        }

        return result;
    }

    private Transform CreateLocalContainer(Transform parent)
    {
        GameObject localObject = new GameObject($"{name} (Local Container)");
        localObject.transform.SetParent(parent, false);
        return localObject.transform;
    }  

    private void DestroyLocalContainer(Transform container)
    {
        if (container != null && container.gameObject != null)
        {
            GameObject.Destroy(container.gameObject);
        }
    }

    private RemoteEntitySyncObject CreateRemoteContainer(IRemoteRenderingMachine machine, Transform parent)
    {
        RemoteEntitySyncObject syncObject = null;

        if (machine?.Session.Connection.ConnectionStatus == ConnectionStatus.Connected)
        {
            GameObject remoteObject = new GameObject($"{name} (Remote Container)");
            remoteObject.transform.SetParent(parent, false); 

            machine.Actions.CreateEntity()?.BindToUnityGameObject(remoteObject);
            syncObject = remoteObject.GetComponent<RemoteEntitySyncObject>();
            syncObject.SyncToRemote();
            syncObject.SyncEveryFrame = true;

            // Create a collider, this will be used if the remote entity doesn't have remote colliders
            MakeInteractable(syncObject);
        }

        return syncObject;
    }

    private void DestroyRemoteContainer(RemoteEntitySyncObject container)
    {
        if (container != null)
        {
            if (container.Entity != null &&
                container.Entity.Valid)
            {
                container.Entity.Destroy();
            }

            if (container.gameObject != null)
            {
                GameObject.Destroy(container.gameObject);
            }
        }
    }

    private static string GetName(RemoteItemBase item)
    {
        return string.IsNullOrEmpty(item.Name) ? "Model" : item.Name;
    }
    
    private static async Task<UnityEngine.Bounds> GetBounds(Component container)
    {
        UnityEngine.Bounds result = LocalBounds.PositiveInfinity;
        RemoteEntitySyncObject remoteEntitySyncObject = container.GetComponentInChildren<RemoteEntitySyncObject>();
        Entity entityContainer = null;

        // Only allow measuring of this container if it's not empty
        if (remoteEntitySyncObject != null &&
            remoteEntitySyncObject.IsEntityValid &&
            remoteEntitySyncObject.Entity.Children.Count > 0)
        {
            entityContainer = remoteEntitySyncObject.Entity;
        }

        if (entityContainer != null)
        {
            result = await entityContainer.SafeQueryLocalBoundsAsync();
        }
        else if (container != null)
        { 
            result = container.transform.RendererLocalBounds();
        }

        return result;
    }

    private static Task<UnityEngine.Bounds> GetBoundsAsNeeded(RemoteItemBase item, Transform container)
    {
        if (!ModelNeedsBounds(item))
        {
            return Task.FromResult(LocalBounds.Zero);
        }

        return GetBounds(container);
    }

    private static bool ModelNeedsBounds(RemoteItemBase item)
    {
        return
            (item != null && (item.Transform.Center || item.Transform.MinSize != Vector3.zero || item.Transform.MaxSize != Vector3.zero || !HasRemoteColliders(item)));
    }

    private static bool HasRemoteColliders(RemoteItemBase item)
    {
        return item is RemoteContainer && ((RemoteContainer)item).HasColliders;
    }

    private void SetColliderBounds(Component container, UnityEngine.Bounds localBounds)
    {
        if (container == null || container.transform == null)
        {
            return;
        }

        if (localBounds.IsInvalidOrInfinite())
        {
            UnityEngine.Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  $"Trying to set an invalid local bounds on the object. ({localBounds})");
            localBounds.size = Vector3.zero;
        }

        BoxCollider collider = container.GetComponent<BoxCollider>();
        if (collider != null)
        {
            collider.center = localBounds.center;
            collider.size = localBounds.size;
        }
    }

    private void CreateMainContainer(RemoteItemBase item)
    {
        if (MainContainer != null)
        {
            return;
        }

        GameObject transformObject = new GameObject($"{name} (Container)");
        transformObject.transform.SetParent(root, false);
        MainContainer = transformObject.transform;
    }

    /// <summary>
    /// Create collider around a container, and disabled it. Enabling should happen later, once the container has
    /// been sized correctly.
    /// </summary>
    private void MakeInteractable(Component container)
    {
        if (container == null)
        {
            return;
        }

        BoxCollider collider = container.EnsureComponent<BoxCollider>();
        ApplyInteractionLayer(container.gameObject);
        container.EnsureComponent<NearInteractionGrabbable>();
        collider.enabled = false;
    }

    /// <summary>
    /// Apply the specified object layer to the container.
    /// </summary>
    private void ApplyInteractionLayer(GameObject container)
    {
        if (container != null && interactionLayer > 0)
        {
            container.layer = interactionLayer;
        }
    }

    private void ApplyTransform(RemoteItemBase item, Component container, UnityEngine.Bounds bounds)
    {
        if (item == null || item.Transform == null)
        {
            return;
        }

        if (container == null)
        {
            return;
        };

        // Apply scale before calculating min/max
        container.transform.localScale = item.Transform.Scale;

        // Scale item, so it fits within the min/max constraints
        ApplyMinMaxSize(item, container, bounds);

        // Apply transform, this may be overridden if model is centered
        container.transform.localPosition = item.Transform.Position;

        // Rotate after centering and applying min/max
        container.transform.localRotation = UnityEngine.Quaternion.Euler(item.Transform.Rotation);

        // Move item so it's at least at origin of this object
        MoveToCenter(item, container, bounds); 
    }

    /// <summary>
    /// Ensure the bounds of the object falls within the min and max settings.
    /// Min and max settings are in the model coordinate system. Outside 
    /// scales are ignored.
    /// </summary>
    private void ApplyMinMaxSize(RemoteItemBase item, Component container, UnityEngine.Bounds bounds)
    {
        if (item.Transform.MaxSize == Vector3.zero && item.Transform.MaxSize == Vector3.zero)
        {
            return;
        }

        if (container == null)
        {
            return;
        }

        if (bounds.size == Vector3.zero ||
            bounds.IsInvalidOrInfinite())
        {
            return;
        }

        //
        // Get max and min size and adjust by parent transform
        //

        var max = item.Transform.MaxSize;
        var min = item.Transform.MinSize;

        //
        // Get the global size, but along the local coordinate system.
        //

        var currentSize = Vector3.Scale(item.Transform.Scale, bounds.size);

        //
        // Apply Min Size
        //

        float scale = 1.0f;

        if (min.x > 0 &&
            currentSize.x > 0 &&
            currentSize.x < min.x)
        {
            scale = Mathf.Max(scale, min.x / currentSize.x);
        }

        if (min.y > 0 &&
            currentSize.y > 0 &&
            currentSize.y < min.y)
        {
            scale = Mathf.Max(scale, min.y / currentSize.y);
        }

        if (min.z > 0 &&
            currentSize.z > 0 &&
            currentSize.z < min.z)
        {
            scale = Mathf.Max(scale, min.z / currentSize.z);
        }

        currentSize = currentSize * scale;
        container.transform.localScale = item.Transform.Scale * scale;

        //
        // Apply Max Size 
        //

        scale = 1.0f;

        if (max.x > 0 &&
            currentSize.x > 0 &&
            currentSize.x > max.x)
        {
            scale = Mathf.Min(scale, max.x / currentSize.x);
        }

        if (max.y > 0 &&
           currentSize.y > 0 &&
           currentSize.y > max.y)
        {
            scale = Mathf.Min(scale, max.y / currentSize.y);
        }

        if (max.z > 0 &&
            currentSize.z > 0 &&
            currentSize.z > max.z)
        {
            scale = Mathf.Min(scale, max.z / currentSize.z);
        }

        container.transform.localScale = item.Transform.Scale * scale;
    }

    private void MoveToCenter(RemoteItemBase item, Component container, UnityEngine.Bounds bounds)
    {
        if (!item.Transform.Center)
        {
            return;
        }

        if (container == null)
        {
            return;
        }

        if (bounds.size == Vector3.zero ||
            bounds.IsInvalidOrInfinite())
        {
            return;
        }

        Vector3 offset = container.transform.localRotation * bounds.center;
        offset = -Vector3.Scale(container.transform.localScale, offset);
        if (item.Transform != null)
        {
            offset += item.Transform.Position;
        }

        container.transform.localPosition = offset;
    }

    private void MoveContainerToGround(Component container, UnityEngine.Bounds bounds)
    {
        if (!grounded)
        {
            return;
        }

        if (container == null)
        {
            return;
        }

        if (bounds.size == Vector3.zero ||
            bounds.IsInvalidOrInfinite())
        {
            return;
        }

        Vector3 globalSize = container.transform.TransformSize(bounds.size);

        // Calculate the Y coordinate of the ground
        float ground = container.transform.position.y + (globalSize.y * 0.5f);
        container.transform.position = new Vector3(container.transform.position.x, ground, container.transform.position.z);;
    }

    /// <summary>
    /// Update model container visibility
    /// </summary>
    private void UpdateContainerVisibility(RemoteItemBase item, Transform container)
    {
        if (container == null)
        {
            return;
        }

        // Only show remote if done loading
        var remoteObject = container.GetComponentInChildren<RemoteEntitySyncObject>(includeInactive: true);
        bool remoteVisible = _visibleModel && IsEnabled && remoteObject != null && remoteObject.IsEntityValid && remoteObject.Entity.Children.Count > 0 && !IsLoading;
        SetRemoteEntityVisibility(item, remoteObject, remoteVisible);

        // Only show load if remote is not visible
        var localRenderers = container.GetComponentsInChildren<Renderer>(includeInactive: true);
        bool localVisible = _visibleModel && IsEnabled && localRenderers != null && localRenderers.Length > 0 && !remoteVisible;
        SetLocalRendererVisibility(localRenderers, localVisible);
    }

    private void UpdatePlaceholderVisibility()
    {
        if (placeholder != null &&
            placeholder.gameObject != null)
        {
            // show placeholder if model is not visible or not loaded, and only show if this object is enabled.
            bool showPlaceholder = (IsEnabled) && (!_visibleModel || (!IsLoaded && LocalContainer == null));
            if (showPlaceholder)
            {
                placeholder.gameObject.SetActive(true);
            }
            else
            {
                placeholder.gameObject.SetActive(false);
            }
        }
    }

    private void SetLocalRendererVisibility(Renderer[] renderers, bool visible)
    {
        int count = renderers?.Length ?? 0;
        for (int i = 0; i < count; i++)
        {
            renderers[i].gameObject.SetActive(visible);
        }
    }

    private void SetRemoteEntityVisibility(RemoteItemBase item, RemoteEntitySyncObject syncObject, bool visible)
    {
        if (syncObject == null || !syncObject.IsEntityValid)
        {
            return;

        }
        syncObject.Entity.SetVisibilityOverride(visible);

        // Enable model's collider only if something is visible and the remote object doesn't have remote colliders
        var collider = syncObject.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = visible && (item != null && !HasRemoteColliders(item));
        }
    }
    #endregion Private functions

    #region Private Classes
    /// <summary>
    /// An event used to pass along enablement changes.
    /// </summary>
    private class RemoteObjectEnabledChangedEvent : UnityEvent<bool>
    { }
    #endregion Private Classes
}
