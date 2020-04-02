// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.Azure.RemoteRendering.Unity;
using Microsoft.MixedReality.Toolkit.Extensions;
using System.Collections.Generic;
using UnityEngine;

using Remote = Microsoft.Azure.RemoteRendering;

public class RemoteObjectReset : MonoBehaviour
{
    private List<EntityState> _originalState = new List<EntityState>();
    private IRemoteRenderingMachine _machine = null;

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
        set
        {
            if (root != value)
            {
                root = value;
                CaptureInitialStateOnce();
            }
        }
    }
    #endregion Serialized Fields

    #region MonoBehavior Methods
    private void Start()
    {
        CaptureInitialStateOnce();
    }
    #endregion MonoBehavior Methods

    #region Public Methods
    /// <summary>
    /// Get the root object, and save it's state.
    /// </summary>
    public void InitializeObject(RemoteObjectLoadedEventData data)
    {
        Root = data?.SyncObject?.transform;
    }

    /// <summary>
    /// Reset to the original Entity state
    /// </summary>
    public void ResetObject()
    {
        if (_machine == null)
        {
            return;
        }

        foreach (var state in _originalState)
        {
            Entity entity = state.entity;
            if (entity != null)
            {
                entity.ReplaceMaterials(null);
                entity.Parent = state.parent;
                entity.Position = state.position;
                entity.Rotation = state.rotation;
                entity.Scale = state.scale;
            }
        }
    }
    #endregion Public Methods

    #region Private Methods
    private void CaptureInitialStateOnce()
    {
        _originalState.Clear();
        _machine = null;
        if (root == null)
        {
            return;
        }

        var syncObject = root.GetComponent<RemoteEntitySyncObject>();
        if (syncObject == null)
        {
            return;
        }

        syncObject.Entity.VisitEntity((Entity entity) =>
        {
            if (entity != null)
            {
                _originalState.Add(new EntityState()
                {
                    entity = entity,
                    parent = entity.Parent,
                    position = entity.Position,
                    rotation = entity.Rotation,
                    scale = entity.Scale
                });
            }
            return Entity.VisitorResult.ContinueVisit;
        });

        _machine = AppServices.RemoteRendering.PrimaryMachine;
    }
    #endregion Private Methods

    #region Private Structs
    private struct EntityState
    {
        public Entity entity;
        public Entity parent;
        public Remote.Double3 position;
        public Remote.Quaternion rotation;
        public Remote.Float3 scale;
    }

    #endregion Private Structs
}
