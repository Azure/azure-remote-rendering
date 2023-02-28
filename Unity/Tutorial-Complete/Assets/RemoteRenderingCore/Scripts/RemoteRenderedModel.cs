// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.Azure.RemoteRendering.Unity;
using System;
using UnityEngine;
using UnityEngine.Events;

public class RemoteRenderedModel : BaseRemoteRenderedModel
{
    public bool AutomaticallyLoad = true;

    private ModelState currentModelState = ModelState.NotReady;

    [SerializeField]
    [Tooltip("The friendly name for this model")]
    private string modelDisplayName;
    public override string ModelDisplayName { get => modelDisplayName; set => modelDisplayName = value; }

    [SerializeField]
    [Tooltip("The URI for this model")]
    private string modelPath;
    public override string ModelPath
    {
        get => modelPath.Trim();
        set => modelPath = value;
    }

    public override ModelState CurrentModelState
    {
        get => currentModelState;
        protected set
        {
            if (currentModelState != value)
            {
                currentModelState = value;
                ModelStateChange?.Invoke(value);
            }
        }
    }

    public override event Action<ModelState> ModelStateChange;
    public override event Action<float> LoadProgress;
    public override Entity ModelEntity { get; protected set; }

    public UnityEvent OnModelNotReady = new UnityEvent();
    public UnityEvent OnModelReady = new UnityEvent();
    public UnityEvent OnStartLoading = new UnityEvent();
    public UnityEvent OnModelLoaded = new UnityEvent();
    public UnityEvent OnModelUnloading = new UnityEvent();

    public UnityFloatEvent OnLoadProgress = new UnityFloatEvent();

    public void Awake()
    {
        // Hook up the event to the Unity event
        LoadProgress += (progress) => OnLoadProgress?.Invoke(progress);

        ModelStateChange += HandleUnityStateEvents;
    }

    private void HandleUnityStateEvents(ModelState modelState)
    {
        switch (modelState)
        {
            case ModelState.NotReady:  OnModelNotReady?.Invoke();  break;
            case ModelState.Ready:     OnModelReady?.Invoke();     break;
            case ModelState.Loading:   OnStartLoading?.Invoke();   break;
            case ModelState.Loaded:    OnModelLoaded?.Invoke();    break;
            case ModelState.Unloading: OnModelUnloading?.Invoke(); break;
        }
    }

    private void Start()
    {
        //Attach to and initialize current state (in case we're attaching late)
        RemoteRenderingCoordinator.CoordinatorStateChange += Instance_CoordinatorStateChange;
        Instance_CoordinatorStateChange(RemoteRenderingCoordinator.instance.CurrentCoordinatorState);
    }

    /// <summary>
    /// Listen for state changes on the coordinator, clean up this model's remote objects if we're no longer connected.
    /// Automatically load if required
    /// </summary>
    private void Instance_CoordinatorStateChange(RemoteRenderingCoordinator.RemoteRenderingState state)
    {
        switch (state)
        {
            case RemoteRenderingCoordinator.RemoteRenderingState.RuntimeConnected:
                CurrentModelState = ModelState.Ready;
                if (AutomaticallyLoad)
                    LoadModel();
                break;
            default:
                UnloadModel();
                break;
        }
    }

    private void OnDestroy()
    {
        ModelStateChange -= HandleUnityStateEvents;
        RemoteRenderingCoordinator.CoordinatorStateChange -= Instance_CoordinatorStateChange;
        UnloadModel();
    }

    /// <summary>
    /// Asks the coordinator to create a model entity and listens for coordinator state changes
    /// </summary>
    [ContextMenu("Load Model")]
    public override async void LoadModel()
    {
        if (CurrentModelState != ModelState.Ready)
            return; //We're already loaded, currently loading, or not ready to load

        CurrentModelState = ModelState.Loading;

        ModelEntity = await RemoteRenderingCoordinator.instance?.LoadModel(ModelPath, this.transform, SetLoadingProgress);

        if (ModelEntity != null)
        {
            CurrentModelState = ModelState.Loaded;
        }
        else
        {
            CurrentModelState = ModelState.Error;
        }
    }

    /// <summary>
    /// Clean up the local model instances
    /// </summary>
    [ContextMenu("Unload Model")]
    public override void UnloadModel()
    {
        CurrentModelState = ModelState.Unloading;

        if (ModelEntity != null)
        {
            var modelGameObject = ModelEntity.GetOrCreateGameObject(UnityCreationMode.DoNotCreateUnityComponents);
            Destroy(modelGameObject);
            if (RemoteRenderingCoordinator.instance.CurrentCoordinatorState == RemoteRenderingCoordinator.RemoteRenderingState.RuntimeConnected) {
                ModelEntity.Destroy();
            }
            ModelEntity = null;
        }

        if (RemoteRenderingCoordinator.instance.CurrentCoordinatorState == RemoteRenderingCoordinator.RemoteRenderingState.RuntimeConnected)
            CurrentModelState = ModelState.Ready;
        else
            CurrentModelState = ModelState.NotReady;
    }

    /// <summary>
    /// Update the Unity progress event
    /// </summary>
    /// <param name="progressValue"></param>
    public override void SetLoadingProgress(float progressValue)
    {
        LoadProgress?.Invoke(progressValue);
    }
}
