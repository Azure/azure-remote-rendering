// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.Utilities;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.WSA;

public class RemoteObjectStage : MonoBehaviour
{
    private ObjectPlacement _stagedObjectsContainerPlacement;
    private bool _isLocked;

    #region Serialized Fields
    [Header("General Settings")]

    [SerializeField]
    [Tooltip("The initial position offset.")]
    private Vector3 initialPositionOffset = new Vector3(0, 0, 2);

    /// <summary>
    /// The initial position offset.
    /// </summary>
    public Vector3 InitialPositionOffset
    {
        get => initialPositionOffset;
        set => initialPositionOffset = value;
    }

    [SerializeField]
    [Tooltip("The anchored stage. Objects in here will be anchored to this transform. If null, will default to this transform.")]
    private Transform stagedArea = null;

    /// <summary>
    /// The anchor stage container. Objects in here will be anchored to this transform. 
    /// If null, will default to this.
    /// </summary>
    public Transform StagedArea
    {
        get => stagedArea;
        set => stagedArea = value;
    }

    [SerializeField]
    [Tooltip("Staged items will be placed in this container, this should be child of stagedArea. If null, a container will be made for you.")]
    private GameObject stagedObjectsContainer;

    /// <summary>
    /// Staged items will be placed in this container, this should be child of stagedArea. If null, a container will be made for you.
    /// </summary>
    public GameObject StagedObjectsContainer
    {
        get => stagedObjectsContainer;
        set => stagedObjectsContainer = value;
    }

    [SerializeField]
    [Tooltip("The unanchor container. Objects in here will have to set their own anchors. If null, nothing can be unstaged.")]
    private Transform unstagedArea = null;

    /// <summary>
    /// The unanchor area. Objects in here will have to set their own anchors.
    /// If null, objects can't can be unstaged.
    /// </summary>
    public Transform UnstagedArea
    {
        get => unstagedArea;
        set => unstagedArea = value;
    }

    [SerializeField]
    [Tooltip("Unstaged items will be placed in this container, this should be child of stagedArea. If null, a container will be made for you.")]
    private GameObject unstagedObjectsContainer;

    /// <summary>
    /// Unstaged items will be placed in this container, this should be child of stagedArea. If null, a container will be made for you.
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
    [Tooltip("Clear stage when disconnected.")]
    private bool clearOnDisconnect = false;

    /// <summary>
    /// Clear stage when disconnected.
    /// </summary>
    public bool ClearOnDisconnect
    {
        get => clearOnDisconnect;
        set => clearOnDisconnect = value;
    }

    [SerializeField]
    [Tooltip("The offset applied to staged objects.  This is this offset from the stage origin.")]
    private Vector3 stagedAreaOffset = new Vector3(0, 0.3f, 0);

    /// <summary>
    /// he offset applied to staged objects.  This is this offset from the stage origin.
    /// </summary>
    public Vector3 StagedAreaOffset
    {
        get => stagedAreaOffset;
        set => stagedAreaOffset = value;
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

    [SerializeField]
    [Tooltip("Remote object collections will be loaded into this prefab.")]
    private GameObject remoteObjectCollectionPrefab = null;

    /// <summary>
    /// Remote object collections will be loaded into this prefab.
    /// </summary>
    public GameObject RemoteObjectCollectionPrefab
    {
        get => remoteObjectCollectionPrefab;
        set => remoteObjectCollectionPrefab = value;
    }
    #endregion Serialized Fields

    #region Public Properties
    /// <summary>
    /// Is the current stage locked.
    /// </summary>
    public bool IsLocked
    {
        get => _isLocked;

        set
        {
            _isLocked = true;
            if (_isLocked)
            {
                LockStage();
            }
            else
            {
                UnlockStage();
            }
        }
    }

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
                stageVisual.gameObject.SetActive(value);
            }
        }

    }
    #endregion Public Properties

    #region Public Methods
    public void ClearContainer()
    {
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}",  "Clearing model containers per request...");
        ClearContainer(stagedObjectsContainer);
        ClearContainer(unstagedObjectsContainer);
    }

    public async void MoveStage()
    {
        if (_stagedObjectsContainerPlacement != null)
        {
            IsStageVisible = true;

            UnlockStage();
            await _stagedObjectsContainerPlacement.StartPlacement();
            LockStage();
        }
    }

    private void LockStage()
    {
        stagedArea.EnsureComponent<WorldAnchor>();
    }

    private void UnlockStage()
    {
        var worldAnchor = stagedArea.GetComponent<WorldAnchor>();
        if (worldAnchor != null)
        {
            Component.DestroyImmediate(worldAnchor);
        }
    }

    public void Load(RemoteItemBase item)
    {
        EnsureContainers();
        GameObject container = IsStageVisible || unstagedObjectsContainer == null ? 
            stagedObjectsContainer : 
            unstagedObjectsContainer;

        if (container == null)
        {
            return;
        }

        bool stagedObject = container == stagedObjectsContainer;

        // only allow one staged item
        if (stagedObject)
        {
            ClearContainer(stagedObjectsContainer);
        }

        Vector3 groundPoint = stageVisual.position + stagedAreaOffset;
        RemoteObject remoteObject = RemoteObjectHelper.Load(
            item,
            remoteObjectPrefab,
            container.transform,
            (RemoteObject initializeRemoteObject) =>
            {
                if (!stagedObject)
                {
                    initializeRemoteObject.IsAnchored = true;
                    initializeRemoteObject.GetComponent<ObjectPlacement>()?.StartPlacement();
                }
            });
    }
    #endregion Public Methods

    #region MonoBehavior Methods
    private void Awake()
    {
        if (stagedArea == null)
        {
            stagedArea = transform;
        }

        _stagedObjectsContainerPlacement = stagedArea.GetComponent<ObjectPlacement>();
        if (_stagedObjectsContainerPlacement != null)
        {
            _stagedObjectsContainerPlacement.OnPlacing.AddListener(OnPlacementStarted);
            _stagedObjectsContainerPlacement.OnPlaced.AddListener(OnPlacementStopped);
        }

        EnsureContainers();
        PlaceInFront();
        IsLocked = true;

        if (AppServices.RemoteRendering != null)
        {
            AppServices.RemoteRendering.StatusChanged += RemoteRendering_StatusChanged;
        }
    }

    private void OnDestroy()
    {
        if (AppServices.RemoteRendering != null)
        {
            AppServices.RemoteRendering.StatusChanged -= RemoteRendering_StatusChanged;
        }

        if (_stagedObjectsContainerPlacement != null)
        {
            _stagedObjectsContainerPlacement.OnPlacing.RemoveListener(OnPlacementStarted);
            _stagedObjectsContainerPlacement.OnPlaced.RemoveListener(OnPlacementStopped);
            _stagedObjectsContainerPlacement = null;
        }
    }
    #endregion MonoBehavior Methods

    #region Private Methods
    private void ClearContainer(GameObject container)
    {
        int childern = container.transform.childCount;
        for (int i = 0; i < childern; i++)
        {
            GameObject.Destroy(container.transform.GetChild(i).gameObject);
        }
    }

    private void EnsureContainers()
    {
        if (stagedObjectsContainer == null && stagedArea != null)
        {
            stagedObjectsContainer = new GameObject();
            stagedObjectsContainer.transform.SetParent(stagedArea, false);
            stagedObjectsContainer.name = $"{name} Staged Container";
        }

        if (unstagedObjectsContainer == null && unstagedArea != null)
        {
            unstagedObjectsContainer = new GameObject();
            unstagedObjectsContainer.transform.SetParent(unstagedArea, false);
            unstagedObjectsContainer.name = $"{name} Unstaged Container";
        }
    }

    private void PlaceInFront(bool applyAnchor = true)
    {
        UnlockStage();

        if (CameraCache.Main != null)
        {
            Transform camera = CameraCache.Main.transform;
            transform.position = camera.position +
                (camera.forward * Vector3.Dot(Vector3.forward, initialPositionOffset)) +
                (camera.up * Vector3.Dot(Vector3.up, initialPositionOffset)) +
                (camera.right * Vector3.Dot(Vector3.right, initialPositionOffset));
        }

        if (applyAnchor && _isLocked)
        {
            LockStage();
        }
    }

    private void OnPlacementStarted()
    {
        IsStageVisible = true;
        PlaceInFront(false);
    }

    private void OnPlacementStopped()
    {
        if (_isLocked)
        {
            LockStage();
        }
    }
    private void RemoteRendering_StatusChanged(object sender, IRemoteRenderingStatusChangedArgs args)
    {
        if (clearOnDisconnect &&
            args.OldStatus == RemoteRenderingServiceStatus.SessionReadyAndConnected)
        {
            ClearContainer();
        }
    }
    #endregion Private Methods
}
