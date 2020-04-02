// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Microsoft.Azure.RemoteRendering;
using Microsoft.Azure.RemoteRendering.Unity;

using Remote = Microsoft.Azure.RemoteRendering;

public class RemoteObjectExplode : MonoBehaviour
{
    [Tooltip("The max distance a piece can fly out from the center")]
    [UnityEngine.Range(0.0f, 5.0f)]
    public float Distance = 0.0f;

    [Tooltip("The smoothing value on animation")]
    public float Smoothing = .75f;

    [Tooltip("Should the exploding auto start")]
    public bool AutoStart = false;

    private IList<ExplodeData> animationData;
    private float lastDistance = 0f;
    private Task<bool> animationUpdateTask = Task.FromResult(true);

    public void Start()
    {
        DisableExplosion = !AutoStart;
    }

    public void StartExplode()
    {
        lastDistance = 0;
        DisableExplosion = false;
    }

    public bool DisableExplosion { get; set; } = true;

    private void Update()
    {
        bool animationUpdateCompleted = 
            (animationUpdateTask != null && animationUpdateTask.IsCompleted);

        if (animationUpdateCompleted)
        {
            bool doneAnimating = animationUpdateTask.Result;

            float newDistance = DisableExplosion ? 0 : Distance;
            if (lastDistance != newDistance)
            {
                if (doneAnimating)
                {
                    if (lastDistance == 0 && newDistance > 0)
                    {
                        CreateAnimationData(newDistance);
                        doneAnimating = false;
                    }
                    else if (newDistance > 0)
                    {
                        doneAnimating = false;
                    }
                    else if (newDistance == 0 && animationData != null)
                    {
                        doneAnimating = false;
                    }
                }  

                animationUpdateTask = Task.FromResult(doneAnimating);
                lastDistance = newDistance;
            }

            if (!doneAnimating && animationData != null)
            {
                animationUpdateTask = UpdateExplode(animationData, newDistance, Smoothing);
            }
        }
    }

    private async void CreateAnimationData(float explodeDistance)
    {
        animationData = null;

        if (explodeDistance <= 0)
        {
            return;
        }

        var root = CaptureSyncObject();

        var data = CaptureRemoteMeshes(root);
        if (data == null)
        {
            return;
        }

        List<Task<AABB3D>> boundsTasks = new List<Task<AABB3D>>();
        foreach (var explodeData in data)
        {
            // TODO....probably can compute bounds locally
            boundsTasks.Add(explodeData.Transform.Entity.QueryWorldBoundsAsync().AsTask());
        }

        AABB3D[] allAABBBounds = await Task.WhenAll(boundsTasks);
        Bounds parentBounds = default(Bounds);

        // Calculate total bounds and center positions of pieces
        for (int i = 0; i < allAABBBounds.Length; i++)
        {
            Bounds currentBounds = allAABBBounds[i].toUnity();
            data[i].Center = currentBounds.center;

            if (i == 0)
            {
                parentBounds = currentBounds;
            }
            else
            {
                parentBounds.Encapsulate(currentBounds);
            }
        }

        // Calculate target positions, and convert start and target to local space
        float maxDistanceFromCenter = parentBounds.extents.magnitude;
        for (int i = 0; i < allAABBBounds.Length; i++)
        {
            ExplodeData currentData = data[i];

            Vector3 directionFromCenter = currentData.Center - parentBounds.center;
            float distanceFromCenter = directionFromCenter.magnitude;

            currentData.CurrentLocal = currentData.StartLocal;
            currentData.ExplosionRatio = maxDistanceFromCenter <= 0 || distanceFromCenter <= 0 ? 0 : distanceFromCenter / maxDistanceFromCenter;
            currentData.Direction = distanceFromCenter <= 0 ? Vector3.zero : directionFromCenter;
        }

        // Commit explosion data
        if (animationData == null)
        {
            animationData = data;
        }
    }

    private Task<bool> UpdateExplode(IList<ExplodeData> items, float explodeDistance, float smoothing)
    {
        TaskCompletionSource<bool> source = new TaskCompletionSource<bool>();
        StartCoroutine(UpdateExplodeImpl(items, explodeDistance, smoothing, result => source.TrySetResult(result)));
        return source.Task;
    }

    private System.Collections.IEnumerator UpdateExplodeImpl(IList<ExplodeData> items, float explodeDistance, float smoothing, Action<bool> result)
    { 
        if (items == null)
        {
            result(false);
            yield break;
        }

        DateTime start = DateTime.Now;
        TimeSpan maxTime = TimeSpan.FromSeconds(0.1);
        bool done = true;
        if (explodeDistance == 0)
        {
            foreach (var data in items)
            {
                data.Transform.Entity.Position = data.StartLocal.toRemotePos();
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
                Vector3 velocity = data.Velocity;
                data.MaxDistance = explodeDistance;
                data.CurrentLocal = Vector3.SmoothDamp(data.CurrentLocal, data.TargetLocal, ref velocity, smoothing, float.MaxValue, deltaTime);
                data.Transform.Entity.Position = data.CurrentLocal.toRemotePos();
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

    private RemoteEntitySyncObject CaptureSyncObject()
    {
        return GetComponentInChildren<RemoteEntitySyncObject>();
    }

    private IList<ExplodeData> CaptureRemoteMeshes(RemoteEntitySyncObject syncObject)
    {     
        Entity rootEntity = null;
        if (syncObject != null)
        {
            rootEntity = syncObject.Entity;
        }

        EntityTransform entityTransform = null;
        if (rootEntity != null)
        {
            entityTransform = rootEntity.CreateTransformSnapshot();
        }

        return ToExplodeData(entityTransform);
    }

    private IList<ExplodeData> ToExplodeData(EntityTransform entityTransform)
    {
        List<ExplodeData> remoteMeshes = new List<ExplodeData>();
        ToExplodeDataImpl(entityTransform, remoteMeshes);
        return remoteMeshes;
    }

    private void ToExplodeDataImpl(EntityTransform entityTransform, IList<ExplodeData> list)
    {
        if (entityTransform == null)
        {
            return;
        }

        foreach (var child in entityTransform.Children)
        {
            ToExplodeDataImpl(child, list);
        }

        if (entityTransform.Entity.FindComponentOfType<Remote.MeshComponent>() != null)
        {
            list.Add(new ExplodeData(entityTransform));
        };
    }

    private class ExplodeData
    {
        public ExplodeData(EntityTransform transform)
        {
            Transform = transform;
            StartLocal = transform.Entity.Position.toUnityPos();
            UpdateTarget();
        }

        private float explosionRatio = 0.0f;
        public float ExplosionRatio
        {
            get => explosionRatio;
            set
            {
                if (explosionRatio != value)
                {
                    explosionRatio = value;
                    UpdateTarget();
                }
            }
        }

        private float maxDistance = 0.0f;
        public float MaxDistance
        {
            get => maxDistance;
            set
            {
                if (maxDistance != value)
                {
                    maxDistance = value;
                    UpdateTarget();
                }
            }
        }

        private Vector3 direction = Vector3.zero;
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
                    UpdateTarget();
                }
            }
        }

        public Vector3 Velocity { get; set; }

        public EntityTransform Transform { get; private set; }

        public Vector3 TargetLocal { get; private set; }

        private Vector3 target;
        public Vector3 Target
        {
            get => target;

            private set
            {
                target = value;
                TargetLocal = ToParentSpace(ref target);
            }
        }

        private Vector3 currentLocal;
        public Vector3 CurrentLocal
        {
            get => currentLocal;

            set
            {
                currentLocal = value;
            }
        }

        private Vector3 startLocal;
        public Vector3 StartLocal
        {
            get => startLocal;

            set
            {
                startLocal = value;
                Start = ToWorldSpace(ref startLocal);
            }
        }

        private Vector3 start;
        public Vector3 Start
        {
            get => start;

            set
            {
                if (start != value)
                {
                    start = value;
                    UpdateTarget();
                }
            }
        }

        public Vector3 CenterLocal { get; private set; }

        private Vector3 center;
        public Vector3 Center
        {
            get => center;

            set
            {
                center = value;
                CenterLocal = ToParentSpace(ref center);
            }
        }

        private Vector3 ToParentSpace(ref Vector3 point)
        {
            if (Transform?.Parent == null)
            {
                return point;
            }

            return Transform.Parent.ToLocal.MultiplyPoint(point);
        }

        private Vector3 ToWorldSpace(ref Vector3 point)
        {
            if (Transform?.Parent == null)
            {
                return point;
            }

            return Transform.Parent.ToWorld.MultiplyPoint(point);
        }

        private void UpdateTarget()
        {
            float targetDistance = MaxDistance * ExplosionRatio;
            if (targetDistance <= 0 || Direction == Vector3.zero)
            {
                Target = Start;
            }
            else
            {
                Target = Start + (Direction * targetDistance);
            }
        }
    }
}
