// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using System;
using UnityEngine;
using UnityEngine.Events;

public class RemoteObjectStage : MonoBehaviour, IRemoteObjectStage
{
    private bool _moving;
    private ObjectPlacement _stagesContainerPlacement;
    private MovableObject _stagesMovableObject;

    #region Serialized Fields
    [Header("General Settings")]

    [SerializeField]
    [Tooltip("Staged items will be placed in this container. If null, a container will be made for you.")]
    private GameObject stagedObjectsContainer;

    /// <summary>
    /// Staged items will be placed in this container. If null, a container will be made for you.
    /// </summary>
    public GameObject StagedObjectsContainer
    {
        get => stagedObjectsContainer;
        set => stagedObjectsContainer = value;
    }

    [SerializeField]
    [Tooltip("Unstaged items will be placed in this container. If null, a container will be made for you.")]
    private GameObject unstagedObjectsContainer;

    /// <summary>
    /// Unstaged items will be placed in this container. If null, a container will be made for you.
    /// </summary>
    public GameObject UnstagedObjectsContainer
    {
        get => unstagedObjectsContainer;
        set => unstagedObjectsContainer = value;
    }

    [SerializeField]
    [Tooltip("The stage's visual container. This will be shown and hidden")]
    private Transform stageVisual = null;

    /// <summary>
    /// The stage's visual container.
    /// </summary>
    public Transform StageVisual
    {
        get => stageVisual;
        set => stageVisual = value;
    }

    [SerializeField]
    [Tooltip("The offset applied to staged objects.  This is this offset from the stage origin.")]
    private Vector3 stagedAreaOffset = new Vector3(0, 0.1f, 0);

    /// <summary>
    /// The offset applied to staged objects.  This is this offset from the stage origin.
    /// </summary>
    public Vector3 StagedAreaOffset
    {
        get => stagedAreaOffset;
        set => stagedAreaOffset = value;
    }

    [SerializeField]
    [Tooltip("Should the staged object be hidden when object is moving.")]
    private bool hideObjectWhenMoving = false;

    /// <summary>
    /// Should the staged object be hidden when object is moving.
    /// </summary>
    public bool HideObjectWhenMoving
    {
        get => hideObjectWhenMoving;
        set => hideObjectWhenMoving = value;
    }

    [Header("Object Prefabs")]

    [SerializeField]
    [Tooltip("Remote objects will be loaded into this prefab.")]
    private GameObject remoteObjectPrefab = null;

    /// <summary>
    /// Remote objects will be loaded into this prefab.
    /// </summary>
    public GameObject RemoteObjectPrefab
    {
        get => remoteObjectPrefab;
        set => remoteObjectPrefab = value;
    }

    [Header("Events")]

    [SerializeField]
    [Tooltip("Event raised when the staged object has changed.")]
    private UnityEvent<RemoteObject> stagedObjectChanged = new RemoteObjectStageEvent();

    /// <summary>
    /// Event raised when the staged object has changed.
    /// </summary>
    public UnityEvent<RemoteObject> StagedObjectChanged => stagedObjectChanged;

    [SerializeField]
    [Tooltip("Event raised when there's a new unstaged object added.")]
    private UnityEvent<RemoteObject> unstagedObjectsChanged = new RemoteObjectStageEvent();

    /// <summary>
    /// Event raised when there's a new unstaged object added
    /// </summary>
    public UnityEvent<RemoteObject> UnstagedObjectsChanged => unstagedObjectsChanged;

    [SerializeField]
    [Tooltip("Event raised when the stage visual changes visibility. This is only raised with visibility changes via IsStageVisible.Set()")]
    private UnityEvent<bool> stageVisualVisibilityChanged = new RemoteObjectStageVisibilityEvent();

    /// <summary>
    /// Event raised when the stage visual changes visibility. This is only raised with visibility changes via IsStageVisible.Set()
    /// </summary>
    public UnityEvent<bool> StageVisualVisibilityChanged => stageVisualVisibilityChanged;


    [SerializeField]
    [Tooltip("Event raised when the stage has moved.")]
    private UnityEvent stageMoved = new UnityEvent();

    /// <summary>
    /// Event raised when the stage has moved.
    /// </summary>
    public UnityEvent StageMoved => stageMoved;

    #endregion Serialized Fields

    #region Public Properties
    /// <summary>
    /// Get or set if the stage visual is visible
    /// </summary>
    public bool IsStageVisible
    {
        get => stageVisual == null ? false : stageVisual.gameObject.activeInHierarchy;

        set
        {
            if (stageVisual != null)
            {
                bool oldValue = stageVisual.gameObject.activeInHierarchy;
                stageVisual.gameObject.SetActive(value);
                if (stageVisual.gameObject.activeInHierarchy != oldValue)
                {
                    stageVisualVisibilityChanged?.Invoke(stageVisual.gameObject.activeInHierarchy);
                }
            }
        }
    }

    /// <summary>
    /// Get or set if the model containers are visible.
    /// </summary>
    public bool IsModelContainerVisible
    {
        get
        {
            EnsureContainers();
            return stagedObjectsContainer.activeInHierarchy && unstagedObjectsContainer.activeInHierarchy;
        }

        set
        {
            EnsureContainers();
            SetModelVisible(stagedObjectsContainer, value);
            SetModelVisible(unstagedObjectsContainer, value);
        }
    }

    /// <summary>
    /// Get the currently staged object.
    /// </summary>
    public RemoteObject StagedObject
    {
        get
        {
            return stagedObjectsContainer?.GetComponentInChildren<RemoteObject>(true);
        }
    }
    #endregion Public Properties

    #region IRemoteObjectStage Methods
    /// <summary>
    /// Stage the given object. This will delete the old staged object.
    /// </summary>
    public void StageObject(GameObject item, bool reposition)
    {
        if (item != null)
        {
            StageObject(item.GetComponent<RemoteObject>(), reposition);
        }
    }

    /// <summary>
    /// Move this object to the unstaged area
    /// </summary>
    public void UnstageObject(GameObject item, bool reposition)
    {
        if (item != null)
        {
            UnstageObject(item.GetComponent<RemoteObject>(), reposition);
        }
    }
    #endregion IRemoteObjectStage Methods

    #region Public Methods
    public void ClearContainer()
    {
        ClearContainer(force: false);
    }

    public async void ClearContainer(bool force)
    {
        bool confirmed = force;
        if (!confirmed)
        {
            confirmed = await AppServices.AppNotificationService.ShowDialog(new DialogOptions()
            {
                Title = "Erase All Models",
                Message = "Do you really want to erase all models?",
                OKLabel = "Yes"
            }) == AppDialog.AppDialogResult.Ok;
        }

        if (confirmed)
        {
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", "Clearing all model containers per request...");
            ClearContainer(stagedObjectsContainer);
            ClearContainer(unstagedObjectsContainer);
        }
    }

    public async void MoveStage()
    {
        if (_stagesContainerPlacement != null)
        {
            IsStageVisible = true;
            await _stagesContainerPlacement.StartPlacement();
        }
    }

    /// <summary>
    /// Clear the stage area
    /// </summary>
    public void ClearStage()
    {
        ClearStage(force: false);
    }

    /// <summary>
    /// Clear the stage area
    /// </summary>
    public async void ClearStage(bool force)
    {
        bool confirmed = force;
        if (!confirmed)
        {
            confirmed = await AppServices.AppNotificationService.ShowDialog(new DialogOptions()
            {
                Title = "Erase Staged Model",
                Message = "Do you really want to erase the staged model?",
                OKLabel = "Yes"
            }) == AppDialog.AppDialogResult.Ok;
        }

        if (confirmed)
        {
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", "Clearing staged model per request...");
            ClearContainer(stagedObjectsContainer);
        }
    }

    /// <summary>
    /// Stage the given object. This will delete the old staged object
    /// </summary>
    public void StageObject(RemoteObject remoteObject, bool reposition)
    {
        if (stagedObjectsContainer == null ||
            remoteObject == null)
        {
            return;
        }

        if (!remoteObject.transform.IsChildOf(stagedObjectsContainer.transform))
        {
            ClearContainer(stagedObjectsContainer);
            remoteObject.transform.SetParent(stagedObjectsContainer.transform, true);
            StagedObjectChanged?.Invoke(remoteObject);
        }

        if (reposition)
        {
            MoveObjectToStage(remoteObject.GetComponent<MovableObject>());
        }
    }

    /// <summary>
    /// Move this object to the unstaged area
    /// </summary>
    public void UnstageObject(RemoteObject remoteObject, bool reposition)
    {
        if (unstagedObjectsContainer == null ||
            remoteObject == null)
        {
            return;
        }

        if (!remoteObject.transform.IsChildOf(unstagedObjectsContainer.transform))
        {
            remoteObject.transform.SetParent(unstagedObjectsContainer.transform, true);
            UnstagedObjectsChanged?.Invoke(remoteObject);
        }

        if (reposition)
        {
            remoteObject.GetComponent<ObjectPlacement>()?.StartPlacement();
        }
    }
    #endregion Public Methods

    #region MonoBehavior Methods
    private void Awake()
    {
        EnsureContainers();

        _stagesContainerPlacement = GetComponent<ObjectPlacement>();
        _stagesMovableObject = GetComponent<MovableObject>();
        if (_stagesMovableObject != null)
        {
            _stagesMovableObject.Moving.AddListener(OnMovingStarted);
            _stagesMovableObject.Moved.AddListener(OnMovingStopped);
        }

        AppServices.RemoteObjectStageService.SetRemoteStage(this);
    }

    private void OnDestroy()
    {
        if (_stagesMovableObject != null)
        {
            _stagesMovableObject.Moving.RemoveListener(OnMovingStarted);
            _stagesMovableObject.Moved.RemoveListener(OnMovingStopped);
            _stagesMovableObject = null;
        }
    }
    #endregion MonoBehavior Methods

    #region Private Methods
    private void ClearContainer(GameObject container)
    {
        if (container == null)
        {
            return;
        }

        int children = container.transform.childCount;
        for (int i = 0; i < children; i++)
        {
            Destroy(container.transform.GetChild(i).gameObject);
        }
    }

    private void EnsureContainers()
    {
        if (stagedObjectsContainer == null)
        {
            stagedObjectsContainer = new GameObject();
            stagedObjectsContainer.transform.SetParent(transform, false);
            stagedObjectsContainer.name = $"{name} Staged Container";
        }

        if (unstagedObjectsContainer == null)
        {
            unstagedObjectsContainer = new GameObject();
            unstagedObjectsContainer.transform.SetParent(transform, false);
            unstagedObjectsContainer.name = $"{name} Unstaged Container";
        }
    }

    private void OnMovingStarted()
    {
        if (!_moving)
        {
            _moving = true;
            IsStageVisible = true;

            if (hideObjectWhenMoving)
            {
                SetStagedObjectEnablement(false);
            }
        }
    }

    private void OnMovingStopped()
    {
        if (_moving)
        {
            _moving = false;

            if (hideObjectWhenMoving)
            {
                SetStagedObjectEnablement(true);
            }

            stageMoved?.Invoke();
        }
    }

    private void SetStagedObjectEnablement(bool isEnabled)
    {
        var stagedObject = StagedObject;
        if (stagedObject != null)
        {
            stagedObject.IsEnabled = isEnabled;
        }
    }

    private void MoveObjectToStage(MovableObject toStage)
    {
        if (toStage != null)
        {
            toStage.MoveOrigin(stagedAreaOffset, Quaternion.identity);
        }
    }

    private void SetModelVisible(GameObject container, bool visible)
    {
        if (container == null)
        {
            return;
        }

        var remoteObjects = container.GetComponentsInChildren<RemoteObject>(includeInactive: true);
        foreach (var remoteObject in remoteObjects)
        {
            remoteObject.IsVisible = visible;
        }

        container.SetActive(visible);
    }
    #endregion Private Methods

    #region Public Classes
    /// <summary>
    /// A event type raised by the remote object stage.
    /// </summary>
    [Serializable]
    private class RemoteObjectStageEvent : UnityEvent<RemoteObject>
    {
    }

    /// <summary>
    /// A event type raised by the remote object stage, when visibility changes.
    /// </summary>
    [Serializable]
    private class RemoteObjectStageVisibilityEvent : UnityEvent<bool>
    {
    }
    #endregion Public Classes
}
