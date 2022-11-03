// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This will show a "create session" dialog, if there is no known Azure Remote Rendering (ARR) session.
/// </summary>
public class StartSessionDialogController : MonoBehaviour
{
    private IRemoteObjectStage _stage = null;
    private HashSet<RemoteObject> _objects = new HashSet<RemoteObject>();

    #region MonoBehavior Functions
    /// <summary>
    /// Delay showing the dialog slightly.
    /// </summary>
    private async void Start()
    {
        _stage = await AppServices.RemoteObjectStageService.GetRemoteStage();

        if (AppServices.RemoteRendering == null || _stage == null)
        {
            DestroyController();
        }
        else
        {
            _stage.StagedObjectChanged.AddListener(OnStageObjectCreate);
            _stage.UnstagedObjectsChanged.AddListener(OnStageObjectCreate);
        }
    }

    private void OnDestroy()
    {
        if (_stage != null)
        {
            _stage.StagedObjectChanged.RemoveListener(OnStageObjectCreate);
            _stage.UnstagedObjectsChanged.RemoveListener(OnStageObjectCreate);
        }

        RemoveAllFromObjectList();
    }
    #endregion MonoBehavior Functions

    #region Private Functions
    /// <summary>
    /// Handle the addition of a remote object
    /// </summary>
    /// <param name="stageObject"></param>
    private void OnStageObjectCreate(RemoteObject remoteObject)
    {
        if (remoteObject != null && _objects.Add(remoteObject))
        {
            AddEventHandlers(remoteObject);
            OnRemoteObjectDataChanged(remoteObject, remoteObject.Data);
        }
    }

    /// <summary>
    /// When there is data on the remote object, check if a remote rendering session is needed.
    /// </summary>
    private void OnRemoteObjectDataChanged(RemoteObjectDataChangedEventData args)
    {
        OnRemoteObjectDataChanged(args.Sender, args.Data);
    }

    /// <summary>
    /// When there is data on the remote object, check if a remote rendering session is needed.
    /// </summary>
    private void OnRemoteObjectDataChanged(RemoteObject remoteObject, RemoteItemBase data)
    { 
        RemoteContainer container = data as RemoteContainer;
        if (container != null && container.HasRemoteModel())
        {
            StartCoroutine(OnRemoteModelAdded(remoteObject));
        }
    }

    /// <summary>
    /// Handle remote object being destroyed.
    /// </summary>
    /// <param name="args"></param>
    private void OnRemoteObjectDestroyed(RemoteObjectDeletedEventData args)
    {
        FindAndRemoveFromObjectList(args.Sender);
    }

    /// <summary>
    /// Handle a remote model being added, and wait for the remote rendering service component to be initialized.
    /// </summary>
    private IEnumerator OnRemoteModelAdded(RemoteObject stageObject)
    {
        yield return 0;

        // wait for object to be placed
        ObjectPlacement objectPlacement = stageObject.GetComponent<ObjectPlacement>();
        while (objectPlacement != null && objectPlacement.InPlacement)
        {
            yield return 0;
        }

        while (AppServices.RemoteRendering.Status == RemoteRenderingServiceStatus.Unknown)
        {
            yield return 0;
        }

        if (AppServices.RemoteRendering.Status == RemoteRenderingServiceStatus.NoSession ||
            AppServices.RemoteRendering.Status == RemoteRenderingServiceStatus.SessionStopped ||
            AppServices.RemoteRendering.Status == RemoteRenderingServiceStatus.SessionError ||
            AppServices.RemoteRendering.Status == RemoteRenderingServiceStatus.SessionExpired)
        {
            RemoteRenderingStartHelper.StartWithPrompt();
        }
    }

    private void DestroyController()
    {
        if (gameObject != null)
        {
            GameObject.Destroy(gameObject);
        }
    }

    private void FindAndRemoveFromObjectList(RemoteObject remoteObject)
    {
        if (remoteObject != null)
        {
            _objects.Remove(remoteObject);
            RemoveEventHandlers(remoteObject);
        }
    }

    private void RemoveAllFromObjectList()
    {
        foreach (var current in _objects)
        {
            if (current != null)
            {
                RemoveEventHandlers(current);
            }
        }
        _objects.Clear();
    }

    private void AddEventHandlers(RemoteObject remoteObject)
    {
        if (remoteObject != null)
        {
            remoteObject.DataChanged.AddListener(OnRemoteObjectDataChanged);
            remoteObject.Deleted.AddListener(OnRemoteObjectDestroyed);
        }
    }

    private void RemoveEventHandlers(RemoteObject remoteObject)
    {
        if (remoteObject != null)
        {
            remoteObject.DataChanged.RemoveListener(OnRemoteObjectDataChanged);
            remoteObject.Deleted.RemoveListener(OnRemoteObjectDestroyed);
        }
    }
    #endregion Private Functions
}
