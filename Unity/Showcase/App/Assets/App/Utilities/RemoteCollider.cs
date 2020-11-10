// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.Azure.RemoteRendering.Unity;
using UnityEngine;

/// <summary>
/// This behaviour can enable or disable the colliders on a remote object.
/// </summary>
public class RemoteCollider : MonoBehaviour
{
    private Entity _entity;

    #region Public Properties
    /// <summary>
    /// Get or set if the Entity's colliders are enabled or disabled.
    /// </summary>
    public bool ColliderEnabled
    {
        get
        {
            Entity entity = GetOrFindEntity();
            return (entity == null) ? false : entity.GetColliderEnabledOverride();
        }

        set
        {
            Entity entity = GetOrFindEntity();
            if (entity != null)
            {
                entity.SetColliderEnabledOverride(value);
            }
        }
    }
    #endregion Public Properties

    #region Private Functions
    /// <summary>
    /// Get the currently cached entity or find the first valid one within the hierarchy.
    /// </summary>
    private Entity GetOrFindEntity()
    {
        if (_entity != null && _entity.Valid)
        {
            return _entity;
        }

        // clear caches entity in case its invalid
        _entity = null;

        // find the first entity sync object
        var entitySyncObject = GetComponentInChildren<RemoteEntitySyncObject>();
        if (entitySyncObject != null && entitySyncObject.IsEntityValid)
        {
            _entity = entitySyncObject.Entity;
        }

        return _entity;
    }
    #endregion Private Functions
}
