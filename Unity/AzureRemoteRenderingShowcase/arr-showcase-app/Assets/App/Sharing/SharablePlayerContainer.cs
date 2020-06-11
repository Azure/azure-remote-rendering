// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.Utilities;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A class for sending the state of the local player, and creating 'SharablePlayer' object for non-local players.
/// </summary>
public class SharablePlayerContainer : MonoBehaviour
{
    private Dictionary<int, GameObject> _otherPlayers = new Dictionary<int, GameObject>();
    private ISharingServicePlayer _localPlayer;
    private Transform _head;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The prefab that represents other players within the shared experience.")]
    private GameObject playerPrefab = null;

    /// <summary>
    /// The prefab that represents other players within the shared experience.
    /// </summary>
    public GameObject PlayerPrefab
    {
        get => playerPrefab;
        set => playerPrefab = value;
    }
    #endregion Serialized Fields

    #region MonoBehaviour Functions
    private void Start()
    {
        _head = CameraCache.Main.transform;

        var sharingService = AppServices.SharingService;
        InitializePlayers(sharingService.Players);
        sharingService.PlayerAdded += SharingServicePlayerAdded;
        sharingService.PlayerRemoved += SharingServicePlayerRemoved;

        var modelLoader = AppServices.RemoteObjectFactory;
        if (modelLoader != null)
        {
            modelLoader.LoadStarted += ModelLoadStarted;
            modelLoader.LoadCompleted += ModelLoadCompleted;
        }

        var remoteRendering = AppServices.RemoteRendering;
        if (remoteRendering != null)
        {
            remoteRendering.StatusChanged += RemoteRenderingStatusChaged;
        }

        var anchoring = AppServices.AnchoringService;
        if (anchoring != null)
        {
            anchoring.ActiveSearchesCountChanged += AnchorSearchesCountChanged;
        }
    }

    /// <summary>
    /// Every frame notify other players of the local players movement.
    /// </summary>
    private void Update()
    {
        if (_localPlayer != null)
        {
            _localPlayer.SetTransform(
                transform.InverseTransformPoint(_head.position),
                Quaternion.LookRotation(transform.InverseTransformDirection(new Vector3(_head.forward.x, 0, _head.forward.z)), Vector3.up));
        }
    }

    private void OnDestroy()
    {
        var sharingService = AppServices.SharingService;
        sharingService.PlayerAdded -= SharingServicePlayerAdded;
        sharingService.PlayerRemoved -= SharingServicePlayerRemoved;

        var modelLoader = AppServices.RemoteObjectFactory;
        if (modelLoader != null)
        {
            modelLoader.LoadStarted -= ModelLoadStarted;
            modelLoader.LoadCompleted -= ModelLoadCompleted;
        }

        var remoteRendering = AppServices.RemoteRendering;
        if (remoteRendering != null)
        {
            remoteRendering.StatusChanged -= RemoteRenderingStatusChaged;
        }

        var anchoring = AppServices.AnchoringService;
        if (anchoring != null)
        {
            anchoring.ActiveSearchesCountChanged -= AnchorSearchesCountChanged;
        }

        UninitializdPlayers();
    }
    #endregion MonoBehaviour Functions

    #region Private Functions
    /// <summary>
    /// Add the list of players to the scene's hierarchy. For each player a new 'PlayerPrefab' will be instantiated and added
    /// to this object's children.
    /// </summary>
    private void InitializePlayers(IReadOnlyCollection<ISharingServicePlayer> players)
    {
        if (players != null)
        {
            foreach (var player in players)
            {
                AddPlayer(player);
            }
        }
    }

    /// <summary>
    /// Remove all added 'PlayerPrefab' objects from this object's children.
    /// </summary>
    private void UninitializdPlayers()
    {
        foreach (var entry in _otherPlayers)
        {
            if (entry.Value != null)
            {
                Destroy(entry.Value);
            }
        }
        _otherPlayers.Clear();
    }

    /// <summary>
    /// Instantiated a 'PlayerPrefab' for the given player, and add it to this object's children. Also ensure that the 
    /// newly created game object has a SharablePlayer component on it.
    /// </summary>
    private void AddPlayer(ISharingServicePlayer player)
    {
        if (player != null)
        {
            RemovePlayer(player);
            if (player.IsLocal)
            {
                _localPlayer = player;
                UpdateLocalPlayerLoadStatus();
            }
            else if (playerPrefab != null)
            {
                GameObject playerObject = Instantiate(playerPrefab, transform);
                playerObject.SetActive(false);
                playerObject.EnsureComponent<SharablePlayer>().Player = player;
                playerObject.SetActive(true);
                _otherPlayers[player.PlayerId] = playerObject;
            }
        }
    }

    /// <summary>
    /// Remove the associated 'PlayerPrefab' object from this object's children.
    /// </summary>
    private void RemovePlayer(ISharingServicePlayer player)
    {
        if (player == _localPlayer)
        {
            _localPlayer = null;
        }

        if (player != null)
        {
            GameObject playerObject;
            _otherPlayers.TryGetValue(player.PlayerId, out playerObject);
            if (playerObject != null)
            {
                Destroy(playerObject);
            }
        }
    }

    /// <summary>
    /// Instantiated a 'PlayerPrefab' for the given player, and add it to this object's children. Also ensure that the 
    /// newly created game object has a SharablePlayer component on it.
    /// </summary>
    private void SharingServicePlayerAdded(ISharingService sender, ISharingServicePlayer player)
    {
        AddPlayer(player);
    }

    /// <summary>
    /// Remove the associated 'PlayerPrefab' object from this object's children.
    /// </summary>
    private void SharingServicePlayerRemoved(ISharingService sender, ISharingServicePlayer player)
    {
        RemovePlayer(player);
    }

    /// <summary>
    /// Invoked when models started loading. When called, the local player's loading status is recalculated.
    /// </summary>
    private void ModelLoadStarted(IRemoteObjectFactoryService obj)
    {
        UpdateLocalPlayerLoadStatus();
    }

    /// <summary>
    /// Invoked when models finished loading. When called the local player's loading status is recalculated.
    /// </summary>
    private void ModelLoadCompleted(IRemoteObjectFactoryService obj)
    {
        UpdateLocalPlayerLoadStatus();
    }

    /// <summary>
    /// Invoked when RemoteRenderingService's status changes. When called the local player's loading status is recalculated.
    /// </summary>
    private void RemoteRenderingStatusChaged(object sender, IRemoteRenderingStatusChangedArgs e)
    {
        UpdateLocalPlayerLoadStatus();
    }

    /// <summary>
    /// Invoked when AnchorService's search count changes. When called the local player's loading status is recalculated.
    /// </summary>
    private void AnchorSearchesCountChanged(IAnchoringService arg1, AnchoringServiceSearchingArgs arg2)
    {
        UpdateLocalPlayerLoadStatus();
    }

    /// <summary>
    /// When called the local player's loading status is recalculated.
    /// </summary>
    private void UpdateLocalPlayerLoadStatus()
    {
        bool isLoading = false;

        var modelLoader = AppServices.RemoteObjectFactory;
        if (modelLoader != null)
        {
            isLoading = modelLoader.IsLoading;
        }

        var remoteRendering = AppServices.RemoteRendering;
        if (remoteRendering != null && !isLoading)
        {
            isLoading = remoteRendering.Status != RemoteRenderingServiceStatus.SessionReadyAndConnected;
        }

        var anchoring = AppServices.AnchoringService;
        if (anchoring != null && !isLoading)
        {
            isLoading = anchoring.ActiveSearchesCount > 0;
        }

        if (_localPlayer != null)
        {
            _localPlayer.SetProperty(SharableStrings.PlayerIsLoading, isLoading);
        }
    }
    #endregion Private Functions
}

