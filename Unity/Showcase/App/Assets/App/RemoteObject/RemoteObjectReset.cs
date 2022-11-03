// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.Azure.RemoteRendering.Unity;
using System.Collections.Generic;
using UnityEngine;

public class RemoteObjectReset : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    [Tooltip("The transform containing the root entity.")]
    private Transform root = null;

    /// <summary>
    /// Get or set the transform containing the root entity
    /// </summary>
    public Transform Root
    {
        get => root;
        set => root = value;
    }

    [Header("Events")]

    [SerializeField]
    [Tooltip("Called when ResetObject has completed.")]
    private RemoteObjectResetCompletedEvent resetCompleted = new RemoteObjectResetCompletedEvent();

    /// <summary>
    /// Called when ResetObject has completed.
    /// </summary>
    public RemoteObjectResetCompletedEvent ResetCompleted
    {
        get => resetCompleted;
    }
    #endregion Serialized Fields

    #region Public Properties
    /// <summary>
    /// Get the initial state of the remote object.
    /// </summary>
    public IEnumerable<EntitySnapshot> OriginalState
    {
        get
        {
            RemoteObject remoteObject = GetComponent<RemoteObject>();
            if (remoteObject == null)
            {
                return null;
            }
            else
            {
                return remoteObject.TransformSnapshot;
            }
        }
    }

    /// <summary>
    /// Get the root container entity
    /// </summary>
    public Entity ContainerEntity
    {
        get
        {
            RemoteObject remoteObject = GetComponent<RemoteObject>();
            if (remoteObject == null)
            {
                return null;
            }
            else
            {
                return remoteObject.Entity;
            }
        }
    }
    #endregion Public Properties

    #region Public Methods
    /// <summary>
    /// Call this once a remote object is loaded. This gets the root object, and save it's state.
    /// </summary>
    /// <remarks>This is called via an event binding in the inspector window.</remarks>
    public void InitializeObject(RemoteObjectLoadedEventData data)
    {
        Root = data?.SyncObject?.transform;
    }

    /// <summary>
    /// Reset to the original Entity state
    /// </summary>
    public void ResetObject()
    {
        ResetObject(true);
    }

    /// <summary>
    /// Reset to the original Entity state
    /// </summary>
    public void ResetObject(bool resetMaterials)
    {
        var originalState = OriginalState;
        if (originalState != null)
        {
            foreach (var state in originalState)
            {
                Entity entity = state.Entity;

                // Only reset valid entities. Also ignore the "container" entity, we only want to reset remote object pieces.
                if (entity != null && entity.Valid && entity != ContainerEntity)
                {
                    if (resetMaterials)
                    {
                        entity.ReplaceMaterials(null);
                    }

                    // This also filters out static entities that do not support reparenting.
                    if (entity.Parent != state.Parent?.Entity)
                    {
                        entity.Parent = state.Parent?.Entity;
                    }

                    entity.Position = state.LocalPosition.toRemotePos();
                    entity.Rotation = state.LocalRotation.toRemote();
                    entity.Scale = state.LocalScale.toRemote();
                }
            }
        }
        resetCompleted?.Invoke(new RemoteObjectResetCompletedEventData(this));
    }
    #endregion Public Methods
}
