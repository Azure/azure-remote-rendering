// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.Azure.RemoteRendering.Unity;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

using Remote = Microsoft.Azure.RemoteRendering;

/// <summary>
/// A set of Azure Remoting Rendering Entity extensions used by the application.
/// </summary>
public static class RemoteEntityExtensions
{
    /// <summary>
    /// Get the first game object in the hierarchy this entity.
    /// </summary>
    public static GameObject GetExistingParentGameObject(this Entity entity)
    {
        GameObject result = null;
        while (result == null && entity != null && entity.Valid)
        {
            result = entity.GetExistingGameObject();
            entity = entity.Parent;
        }
        return result;
    }

    /// <summary>
    /// This is a "no exception" version of QueryLocalBoundsAsync(). This will catch exceptions and return a default result.
    /// </summary>
    public static async Task<UnityEngine.Bounds> SafeQueryLocalBoundsAsync(this Remote.Entity entity)
    {
        UnityEngine.Bounds result = new UnityEngine.Bounds(Vector3.positiveInfinity, Vector3.negativeInfinity);

        try
        {
            var arrBounds = await entity.QueryLocalBoundsAsync();
            result = arrBounds.toUnity();
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}", $"Failed to get bounds of remote object. Reason: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Find first entity in this entity's parents (inclusive of itself) that fulfills pred. 
    /// </summary>
    public static Entity FindFirstParentEntity(this Remote.Entity entity, Entity.EntitySearchDelegate pred)
    {
        if (pred(entity))
        {
            return entity;
        }

        if (entity.Parent != null)
        {
            Entity res = entity.Parent.FindFirstParentEntity(pred);
            if (res != null)
            {
                return res;
            }
        }

        return null;
    }

    /// <summary>
    /// This is the implementation of FindFirstParentEntity. Call FindFirstParentEntity(Remote.Entity)
    /// to correctly start the recursion. 
    /// </summary>
    private static Entity.VisitorResult VisitParentEntityImpl(this Remote.Entity entity, Entity.VisitEntityDelegate visitor)
    {
        if (entity == null || visitor(entity) == Entity.VisitorResult.ExitVisit)
        {
            return Entity.VisitorResult.ExitVisit;
        }

        if (entity.Parent != null)
        {
            if (entity.Parent.VisitParentEntityImpl(visitor) == Entity.VisitorResult.ExitVisit)
            {
                return Entity.VisitorResult.ExitVisit;
            }
        }

        return Entity.VisitorResult.ContinueVisit;
    }

    /// <summary>
    /// Set the visibility override
    /// </summary>
    public static void SetVisibilityOverride(this Remote.Entity entity, bool value)
    {
        if (entity == null || !entity.Valid)
        {
            return;
        }

        var component = entity.EnsureComponentOfType<HierarchicalStateOverrideComponent>();

        component.HiddenState = value ? HierarchicalEnableState.InheritFromParent : HierarchicalEnableState.ForceOn;
        component.DisableCollisionState = value ? HierarchicalEnableState.InheritFromParent : HierarchicalEnableState.ForceOn;
    }

    /// <summary>
    /// Set the collider enable override
    /// </summary>
    public static void SetColliderEnabledOverride(this Remote.Entity entity, bool enable)
    {
        if (entity == null || !entity.Valid)
        {
            return;
        }

        var component = entity.EnsureComponentOfType<HierarchicalStateOverrideComponent>();

        if (enable)
        {
            component.DisableCollisionState = HierarchicalEnableState.InheritFromParent;
        }
        else
        {
            component.DisableCollisionState = HierarchicalEnableState.ForceOn;
        }
    }

    /// <summary>
    /// Get the collider enable override. True if entity's colliders are enabled, false otherwise.
    /// </summary>
    public static bool GetColliderEnabledOverride(this Remote.Entity entity)
    {
        if (entity == null || !entity.Valid)
        {
            return false;
        }

        var component = entity.EnsureComponentOfType<HierarchicalStateOverrideComponent>();

        return component.DisableCollisionState == HierarchicalEnableState.InheritFromParent;
    }

    /// <summary>
    /// Find all matching entities by a regex pattern. This entity's and all children's names will be tested against 
    /// the given regex pattern.
    /// </summary>
    public static Remote.Entity[] FindAllByPattern(this Remote.Entity entity, string pattern)
    {
        List<Remote.Entity> entities = new List<Entity>();

        Regex regex = null;

        try
        {
            regex = new Regex(pattern);
        }
        catch (Exception)
        {
            Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}", $"Invalid regex pattern '{pattern}' given to FindAllByPattern.");
        }

        if (regex != null)
        {
            entity.VisitEntity((Remote.Entity child) =>
            {
                if (regex.IsMatch(child.Name))
                {
                    entities.Add(child);
                }

                return Entity.VisitorResult.ContinueVisit;
            });
        }

        return entities.ToArray();
    }

    /// <summary>
    /// This creates a "snapshot" of an entity's global positions and rotation.
    /// </summary>
    public static EntitySnapshot CreateSnapshot(this Entity entity)
    {
        if (entity == null)
        {
            return null;
        }

        var result = new EntitySnapshot(entity);
        while (entity.Parent != null)
        {
            entity = entity.Parent;
            result.Parent = new EntitySnapshot(entity);
        }

        return result;
    }

    /// <summary>
    /// Replace all mesh materials with the given material.
    /// </summary>
    public static void ReplaceMaterials(this Remote.Entity entity, Remote.Material material)
    {
        IEnumerable<Remote.MeshComponent> meshComponents =
            entity?.FindComponentsOfType<MeshComponent>();

        foreach (var mesh in meshComponents)
        {
            int materialCount = mesh.UsedMaterials.Count;
            for (int i = 0; i < materialCount; i++)
            {
                mesh.SetMaterial(i, material);
            }
        }
    }

    /// <summary>
    /// Find the Azure Remote Rendering components of a given type on the given entity.
    /// </summary>
    public static IEnumerable<T> FindComponentsOfType<T>(this Remote.Entity entity)
        where T : ComponentBase
    {
        if (entity == null)
        {
            yield break;
        }

        IReadOnlyList<ComponentBase> components = entity.Components;
        int count = components.Count;
        for (int i = 0; i < count; i++)
        {
            ComponentBase component = components[i];
            if (component is T)
            {
                yield return (T)component;
            }
        }
    }

    /// <summary>
    /// Create an Azure Remote Rendering component of a given type on the given entity.
    /// </summary>
    public static T CreateComponentOfType<T>(this Remote.Entity entity) where T : ComponentBase
    {
        if (entity == null || !RemoteManagerUnity.IsInitialized)
        {
            return default(T);
        }

        T result = RemoteManagerUnity.CurrentSession?.Connection.CreateComponent(GetObjectType<T>(), entity) as T ?? null;
        return result;
    }

    /// <summary>
    /// Create an Azure Remote Rendering component of a given type on the given entity, if one doesn't already exist.
    /// </summary>
    public static T EnsureComponentOfType<T>(this Remote.Entity entity) where T : ComponentBase
    {
        if (entity == null)
        {
            return default(T);
        }

        T component = entity.FindComponentOfType<T>();
        if (component == null)
        {
            component = entity.CreateComponentOfType<T>();
        }

        return component;
    }

    /// <summary>
    /// Is the given entity a child of the given parent. This is incuslive of the entity itself.
    /// </summary>
    public static bool IsChildOf(this Entity entity, Entity parent)
    {
        if (parent == null)
        {
            return false;
        }

        return entity.FindFirstEntity((Entity test) => parent == test) == parent;
    }

    /// <summary>
    /// Get the object type enum value of a given Azure Remote Rendering component.
    /// </summary>
    private static Remote.ObjectType GetObjectType<T>() where T : ComponentBase
    {
        var type = typeof(T);
        if (type == typeof(CutPlaneComponent))
        {
            return ObjectType.CutPlaneComponent;
        }
        else if (type == typeof(MeshComponent))
        {
            return ObjectType.MeshComponent;
        }
        else if (type == typeof(HierarchicalStateOverrideComponent))
        {
            return ObjectType.HierarchicalStateOverrideComponent;
        }
        else if (type == typeof(PointLightComponent))
        {
            return ObjectType.PointLightComponent;
        }
        else if (type == typeof(SpotLightComponent))
        {
            return ObjectType.SpotLightComponent;
        }
        else if (type == typeof(DirectionalLightComponent))
        {
            return ObjectType.DirectionalLightComponent;
        }
        throw new Exception($"Unsupported component type! {type.ToString()}");
    }
}

/// <summary>
/// A "snapshot" of an entity's global positions and rotation.
/// </summary>
public class EntitySnapshot
{
    private bool _matricesInitialized;
    private UnityEngine.Matrix4x4 _toWorld;
    private UnityEngine.Matrix4x4 _toLocal;

    public EntitySnapshot(Entity entity, EntitySnapshot parent = null)
    {
        Entity = entity;
        LocalPosition = entity.Position.toUnityPos();
        LocalRotation = entity.Rotation.toUnity();
        LocalScale = entity.Scale.toUnity();
        Parent = parent;
    }

    public Vector3 LocalPosition { get; }

    public UnityEngine.Quaternion LocalRotation { get; }

    public Vector3 LocalScale { get; }

    public Entity Entity { get; }

    public EntitySnapshot Parent { get; set; }

    public UnityEngine.Matrix4x4 ToWorld
    {
        get
        {
            InitializeMatrices();
            return _toWorld;
        }
    }

    public UnityEngine.Matrix4x4 ToLocal
    {
        get
        {
            InitializeMatrices();
            return _toLocal;
        }
    }

    private void InitializeMatrices()
    {
        if (_matricesInitialized)
        {
            return;
        }

        _toWorld = UnityEngine.Matrix4x4.TRS(LocalPosition, LocalRotation, LocalScale);
        var parent = Parent;
        if (parent != null)
        {
            _toWorld = parent.ToWorld * _toWorld;
        }
        _toLocal = _toWorld.inverse;
        _matricesInitialized = true;
    }
}
