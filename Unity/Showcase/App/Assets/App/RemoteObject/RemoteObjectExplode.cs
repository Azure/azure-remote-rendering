// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.Azure.RemoteRendering.Unity;
using Microsoft.MixedReality.Toolkit;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

using Remote = Microsoft.Azure.RemoteRendering;

public class RemoteObjectExplode : MonoBehaviour
{
    private static LogHelper<RemoteObjectExplode> _log = new LogHelper<RemoteObjectExplode>();
    private IList<ExplodeData> _animationData;
    private Coroutine _explosion;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The max distance a piece can fly out from the center")]
    [Range(0.0f, 5.0f)]
    private float distance = 1.0f;

    /// <summary>
    /// The max distance a piece can fly out from the center.
    /// </summary>
    public float Distance
    {
        get => distance;
        set => distance = value;
    }

    [SerializeField]
    [Tooltip("The smoothing value on animation. For remote rendered content it's recommended to have no smoothing, so the explosion happen instantly. If the explosion is animated with remote rendered content, the frame rate my drop below 60 Hz.")]
    private float smoothing = 0.0f;

    /// <summary>
    /// The smoothing value on animation. For remote rendered content it's recommended to
    /// have no smoothing, so the explosion happen instantly. If the explosion is animated
    /// with remote rendered content, the frame rate my drop below 60 Hz.
    /// </summary>
    public float Smoothing
    {
        get => smoothing;
        set => smoothing = value;
    }

    [Header("Events")]

    [SerializeField]
    [Tooltip("Event raised when the explosion has started.")]
    private UnityEvent explodeStarted = new UnityEvent();

    /// <summary>
    /// Event raised when the explosion has started.
    /// </summary>
    public UnityEvent ExplodedStated => explodeStarted;

    [SerializeField]
    [Tooltip("Event raised when the explosion has completed.")]
    private UnityEvent explodeCompleted = new UnityEvent();

    /// <summary>
    /// Event raised when the explosion has completed.
    /// </summary>
    public UnityEvent ExplodeCompleted => explodeCompleted;
    #endregion Serialize Fields

    #region Public Functions
    /// <summary>
    /// Try to reset the object pieces, and restar the explosion
    /// </summary>
    public void StartExplode()
    {
        if (_explosion != null)
        {
            StopCoroutine(_explosion);
            _explosion = null;
        }

        _explosion = StartCoroutine(PlayExplosion(distance));
    }
    #endregion Public Functions

    #region Private Functions
    private IEnumerator PlayExplosion(float explodeDistance)
    {
        explodeStarted?.Invoke();

        if (_animationData == null)
        {
            _animationData = CreateAnimationData();
        }
        else
        {
            foreach (var data in _animationData)
            {
                data.Reset();
            }
        }

        bool doneAnimating = false;
        while (!doneAnimating)
        {
            yield return UpdateExplode(_animationData, explodeDistance, Smoothing, (bool result) => doneAnimating = result);
            yield return null;
        }

        _explosion = null;
        explodeCompleted?.Invoke();
    }

    private IList<ExplodeData> CreateAnimationData()
    {
        RemoteEntitySyncObject root = CaptureSyncObject();
        if (root == null || !root.IsEntityValid)
        {
            return null;
        }

        RemoteObject remoteObject = root.GetComponentInParent<RemoteObject>();
        IEnumerable<EntitySnapshot> snapshots = remoteObject?.TransformSnapshot;

        return CreateExplodeData(snapshots);
    }

    /// <summary>
    /// Create and initialize explosion data from snapshot data.
    /// </summary>
    private IList<ExplodeData> CreateExplodeData(IEnumerable<EntitySnapshot> snapshots)
    {
        IList<ExplodeData> data = ToExplodeData(snapshots);
        if (data == null || data.Count == 0)
        {
            return null;
        }

        // Calculate the global parent bounds
        UnityEngine.Bounds parentBounds = default;
        for (int i = 0; i < data.Count; i++)
        {
            ExplodeData explodeData = data[i];
            Remote.Bounds localBounds = explodeData.Mesh.Mesh.Bounds;
            Vector3 max = explodeData.Snapshot.ToWorld.MultiplyPoint(localBounds.Max.toUnityPos());
            Vector3 min = explodeData.Snapshot.ToWorld.MultiplyPoint(localBounds.Min.toUnityPos());
            var globalBounds = new UnityEngine.Bounds((min + max) * 0.5f, max - min);

            explodeData.Center = globalBounds.center;
            if (i == 0)
            {
                parentBounds = globalBounds;
            }
            else
            {
                parentBounds.Encapsulate(globalBounds);
            }
        }

        // Calculate target positions, and convert start and target to local space
        float maxDistanceFromCenter = parentBounds.extents.magnitude;
        for (int i = 0; i < data.Count; i++)
        {
            ExplodeData currentData = data[i];

            Vector3 directionFromCenter = currentData.Center - parentBounds.center;
            float distanceFromCenter = directionFromCenter.magnitude;

            currentData.CurrentLocal = currentData.StartLocal;
            currentData.ExplosionRatio = maxDistanceFromCenter <= 0 || distanceFromCenter <= 0 ? 0 : distanceFromCenter / maxDistanceFromCenter;
            currentData.Direction = distanceFromCenter <= 0 ? Vector3.zero : directionFromCenter;
        }

        return data;
    }

    private IEnumerator UpdateExplode(IList<ExplodeData> items, float explodeDistance, float smoothing, Action<bool> result)
    { 
        if (items == null)
        {
            result(false);
            yield break;
        }

        DateTime start = DateTime.Now;
        TimeSpan maxTime = TimeSpan.FromSeconds(0.1);
        bool done = true;
        Entity entity;
        if (explodeDistance == 0)
        {
            foreach (var data in items)
            {
                if (!ValidateAndGetEntity(data, out entity, errorMessage: "Failed to update explosion when distance at zero"))
                {
                    break;
                }

                entity.Position = data.StartLocal.toRemotePos();
                if (maxTime < DateTime.Now - start)
                {
                    yield return null;
                    start = DateTime.Now;
                }
            }
        }
        else
        {
            float deltaTime = Time.deltaTime;
            foreach (var data in items)
            {
                if (!ValidateAndGetEntity(data, out entity, errorMessage: "Failed to update explosion"))
                {
                    break;
                }

                Vector3 velocity = data.Velocity;
                data.MaxDistance = explodeDistance;
                data.Update();
                data.CurrentLocal = Vector3.SmoothDamp(data.CurrentLocal, data.TargetLocal, ref velocity, smoothing, float.MaxValue, deltaTime);

                entity.Position = data.CurrentLocal.toRemotePos();

                done &= (data.CurrentLocal - data.TargetLocal).sqrMagnitude <= 0.000001f;
                data.Velocity = velocity;
                if (maxTime < DateTime.Now - start)
                {
                    yield return null;
                    start = DateTime.Now;
                    deltaTime += Time.deltaTime;
                }
            }
        }

        result(done);
    }

    /// <summary>
    /// Find the nearest remote entity sync object.
    /// </summary>
    /// <returns></returns>
    private RemoteEntitySyncObject CaptureSyncObject()
    {
        return GetComponentInChildren<RemoteEntitySyncObject>();
    }

    /// <summary>
    /// Wrap EntitySnapshots in ExplodeData objects which will contain info on the pieces explosion position.
    /// </summary>
    private IList<ExplodeData> ToExplodeData(IEnumerable<EntitySnapshot> entitySnapshots)
    {
        if (entitySnapshots == null)
        {
            return null;
        }

        List<ExplodeData> explodeData = new List<ExplodeData>();
        foreach (EntitySnapshot snapshot in entitySnapshots)
        {
            if (snapshot.Entity == null)
            {
                _log.LogError("Failed to wrap enity snapshot in explosion data. Entity was null.");
                continue;
            }
            else if (!snapshot.Entity.Valid)
            {
                _log.LogError("Failed to wrap enity snapshot in explosion data. Entity was invalid.");
                continue;
            }

            var mesh = snapshot.Entity.FindComponentOfType<MeshComponent>();
            if (mesh != null)
            {
                explodeData.Add(new ExplodeData(snapshot, mesh));
            };
        }
        return explodeData;
    }

    /// <summary>
    /// Get the entity off of the explode data object, and valid the entity. If entity is not valid, log error message.
    /// </summary>
    /// <returns>
    /// Return true if entiry is valid, false otherwise.
    /// </returns>
    private static bool ValidateAndGetEntity(ExplodeData explodeData, out Entity entity, string errorMessage)
    {
        entity = explodeData.Snapshot.Entity;
        if (entity == null)
        {
            _log.LogError("{0}. Entity was null.");
            return false;

        }
        else if (!entity.Valid)
        {
            _log.LogError("{0}. Entity was invalid.");
            return false;
        }
        return true;
    }
    #endregion Private Functions

    #region Private Classes
    private class ExplodeData
    {
        private Vector3 startLocal = Vector3.negativeInfinity;
        private Vector3 target = Vector3.negativeInfinity;
        private Vector3 targetLocal = Vector3.negativeInfinity;
        private Vector3 direction = Vector3.negativeInfinity;
        private float explosionRatio = 0.0f;
        private float maxDistance = 0.0f;

        public ExplodeData(EntitySnapshot snapshot, MeshComponent mesh)
        {
            Snapshot = snapshot;
            Mesh = mesh;
            startLocal = snapshot.LocalPosition;
            Start = ToWorldSpace(ref startLocal);
        }

        public float ExplosionRatio
        {
            get => explosionRatio;
            set
            {
                if (explosionRatio != value)
                {
                    explosionRatio = value;
                    target = Vector3.negativeInfinity;
                }
            }
        }

        public float MaxDistance
        {
            get => maxDistance;
            set
            {
                if (maxDistance != value)
                {
                    maxDistance = value;
                    target = Vector3.negativeInfinity;
                }
            }
        }

        public Vector3 Direction
        {
            get => direction;
            set
            {
                if (direction != value)
                {
                    if (value == Vector3.zero)
                    {
                        direction = value;
                    }
                    else
                    {
                        direction = value.normalized;
                    }
                    target = Vector3.negativeInfinity;
                }
            }
        }

        public Vector3 StartLocal => startLocal;

        public Vector3 Start { get; }

        public Vector3 Velocity { get; set; }

        public EntitySnapshot Snapshot { get; }

        public MeshComponent Mesh { get; }

        public Vector3 TargetLocal => targetLocal;

        public Vector3 Target => target;

        public Vector3 CurrentLocal { get; set; }

        public Vector3 Center { get; set; }

        public void Update()
        {
            if (target.IsValidVector())
            {
                return;
            }

            float targetDistance = MaxDistance * ExplosionRatio;
            if (targetDistance <= 0 || Direction == Vector3.zero)
            {
                target = Start;
            }
            else
            {
                target = Start + (Direction * targetDistance);
            }

            targetLocal = ToParentSpace(ref target);
        }

        public void Reset()
        {
            Velocity = Vector3.zero;
            CurrentLocal = StartLocal;
        }

        private Vector3 ToParentSpace(ref Vector3 point)
        {
            if (Snapshot?.Parent == null)
            {
                return point;
            }

            return Snapshot.Parent.ToLocal.MultiplyPoint(point);
        }

        private Vector3 ToWorldSpace(ref Vector3 point)
        {
            if (Snapshot?.Parent == null)
            {
                return point;
            }

            return Snapshot.Parent.ToWorld.MultiplyPoint(point);
        }
    }
    #endregion Private Classes
}
