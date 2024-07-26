// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;
using Unity.Profiling;

using Microsoft.Azure.RemoteRendering;
using Microsoft.Azure.RemoteRendering.Unity;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Physics;
using Microsoft.MixedReality.Toolkit.Extensions;

namespace Microsoft.Showcase.App.Pointers
{
    /// <summary>
    /// A per pointer caster to handle casting against Azure Remote Rendering content.
    /// It handles raycasts for far pointers, as well as sphere overlaps for near pointers.
    /// Results are smoothed, as long as it hits the same <see cref="GameObject"/>.
    /// </summary>
    /// <remarks>
    /// Due to the async nature of remote rendered content, the caster continuously sends out queries almost every frame
    /// during the pointers "OnPreSceneQuery" call. "OnSceneQuery" only retrieves the latest hit result.
    /// Decoupling the <see cref="Update"/> call from the <see cref="OnSceneQuery"/> call allows for faster and
    /// more responsive results, as there are always casts in-flight.
    /// </remarks>
    public class RemotePointerCaster
    {
        /// <summary>
        /// Minimum differences in distance for one grabbable to be considered closer than the other.
        /// Same as <see cref="SpherePointer.SpherePointerQueryInfo.MIN_DIST_DIFF"/>.
        /// </summary>
        private const float MIN_DIST_DIFF = 1.0e-5f;

        /// <summary>
        /// Interval for casting and smoothing in seconds.
        /// </summary>
        /// <remarks>
        /// Casting still produces some pressure on the GC. To reduce this pressure don't
        /// cast every frame, but only every other one. On Quest devices we use every third
        /// frame for casting, due to its increased target frame rate.
        /// </remarks>
#if UNITY_EDITOR || !UNITY_ANDROID
        private const float CastQueryInterval = 1f / 30f;
#else
        private const float CastQueryInterval = 1f / 24f;
#endif

        /// <summary>
        /// The maximum number of <see cref="RemoteCastData"/> instances to cache.
        /// </summary>
        private const int MaxCastDataCacheSize = 5;

        /// <summary>
        /// Internal data struct to hold and save results from remote casts.
        /// </summary>
        private struct RemoteCastHit
        {
            public Entity HitEntity;
            public GameObject HitObject;
            public Vector3 HitPointOnObject;
            public Vector3 HitNormalOnObject;
            public int PriorityLayerIndex;
            public RayStep Ray;
            public int RayStepIndex;
            public float Distance;
            public float Volume;
        }

        /// <summary>
        /// Internal data struct for pending remote casts, containing reusable list instances.
        /// </summary>
        private struct RemoteCastData
        {
            public Task<RemoteCastHit> PendingTask;
            public List<RayStep> RaySteps;
            public List<LayerMask> PrioritizedLayerMasks;
            public List<Task<RayCastQueryResult>> RaycastsQueries;
            public List<RayCastHit> RaycastsResults;
            public List<Task<SpatialQueryResult>> SpatialQueries;
            public List<MeshComponent> SpatialResults;

            /// <summary>
            /// Returns a <see cref="RemoteCastData"/> with newly instantiated lists.
            /// </summary>
            public static RemoteCastData Create()
            {
                return new RemoteCastData
                {
                    PendingTask = null,
                    RaySteps = new List<RayStep>(),
                    PrioritizedLayerMasks = new List<LayerMask>(),
                    RaycastsQueries = new List<Task<RayCastQueryResult>>(),
                    RaycastsResults = new List<RayCastHit>(),
                    SpatialQueries = new List<Task<SpatialQueryResult>>(),
                    SpatialResults = new List<MeshComponent>(),
                };
            }

            /// <summary>
            /// Reset the internal state as returned by <see cref="Create"/>.
            /// I.e. clears the internal lists.
            /// </summary>
            public void Clear()
            {
                PendingTask = null;
                RaySteps.Clear();
                PrioritizedLayerMasks.Clear();
                RaycastsQueries.Clear();
                RaycastsResults.Clear();
                SpatialQueries.Clear();
                SpatialResults.Clear();
            }
        }

        private bool m_hasLastHit = false;
        private float m_smoothingTimer = 0f;
        private float m_queryTimer = 0f;
        private RemoteCastHit m_smoothedLastHit = default;
        private RemoteCastHit m_currentLastHit = default;
        private RemoteCastHit m_previousLastHit = default;

        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private Queue<RemoteCastData> m_pendingCasts = new Queue<RemoteCastData>();
        private Stack<RemoteCastData> m_castDataCache = new Stack<RemoteCastData>();

        /// <summary>
        /// Is there currently any valid remote hit data available.
        /// Used for <see cref="IMixedRealityNearPointer.IsNearObject"/> and
        /// near pointer <see cref="IMixedRealityPointer.IsInteractionEnabled"/>.
        /// </summary>
        public bool LastHitValid => m_hasLastHit;

        /// <summary>
        /// Are we connected to an Azure Remote Rendering session.
        /// </summary>
        private bool AzureRemoteRenderingConnected
        {
            get { return (AppServices.RemoteRendering?.PrimaryMachine?.Session?.Connection?.ConnectionStatus ?? ConnectionStatus.Disconnected) == ConnectionStatus.Connected; }
        }


        /// <summary>
        /// Called by far pointer when querying the scene to determine which objects it is hitting.
        /// Assumes the <paramref name="hitInfo"/> is already filled with the local query result.
        /// </summary>
        public virtual bool OnSceneQuery(bool localQueryResult, LayerMask[] prioritizedLayerMasks, ref MixedRealityRaycastHit hitInfo, ref RayStep ray, ref int rayStepIndex, out Entity hitEntity)
        {
            if (!m_hasLastHit)
            {
                hitEntity = null;
                return false;
            }

            float remoteDistance = m_smoothedLastHit.Distance;
            int remoteLayerIndex = m_smoothedLastHit.PriorityLayerIndex;
            int localLayerIndex = localQueryResult ? GetLayerMaskPriorityIndex(prioritizedLayerMasks, hitInfo.transform.gameObject.layer) : int.MaxValue;

            if (!localQueryResult || (remoteLayerIndex < localLayerIndex) || ((remoteLayerIndex == localLayerIndex) && (remoteDistance < hitInfo.distance)))
            {
                hitEntity = m_smoothedLastHit.HitEntity;
                hitInfo = default;
                hitInfo.point = m_smoothedLastHit.HitPointOnObject;
                hitInfo.normal = m_smoothedLastHit.HitNormalOnObject;
                hitInfo.distance = m_smoothedLastHit.Distance;
                hitInfo.transform = m_smoothedLastHit.HitObject.transform;
                hitInfo.raycastValid = true;
                ray = m_smoothedLastHit.Ray;
                rayStepIndex = m_smoothedLastHit.RayStepIndex;
                return true;
            }
            else
            {
                hitEntity = null;
                return false;
            }
        }

        /// <summary>
        /// Called by near pointer when querying the scene to determine which objects it is hitting.
        /// Assumes the <paramref name="hitObject"/>, <paramref name="hitPoint"/>, and <paramref name="hitDistance"/> are already filled with the local query result.
        /// </summary>
        public virtual bool OnSceneQuery(bool localQueryResult, LayerMask[] prioritizedLayerMasks, ref GameObject hitObject, ref Vector3 hitPoint, ref float hitDistance, out Entity hitEntity)
        {
            if (!m_hasLastHit)
            {
                hitEntity = null;
                return false;
            }

            float remoteDistance = m_smoothedLastHit.Distance;
            float remoteVolume = m_smoothedLastHit.Volume;
            int remoteLayerIndex = m_smoothedLastHit.PriorityLayerIndex;
            int localLayerIndex = localQueryResult ? GetLayerMaskPriorityIndex(prioritizedLayerMasks, hitObject.layer) : int.MaxValue;
            float localVolume = (localQueryResult && hitObject.TryGetComponent(out Collider localCollider)) ? localCollider.bounds.Transform(hitObject.transform.localToWorldMatrix).Volume() : Mathf.Infinity;

            float distanceDiff = hitDistance - remoteDistance;

            if (!localQueryResult || (remoteLayerIndex < localLayerIndex) || ((remoteLayerIndex == localLayerIndex) && ((distanceDiff > MIN_DIST_DIFF) || ((Math.Abs(distanceDiff) < MIN_DIST_DIFF) && (remoteVolume < localVolume)))))
            {
                hitEntity = m_smoothedLastHit.HitEntity;
                hitObject = m_smoothedLastHit.HitObject;
                hitPoint = m_smoothedLastHit.HitPointOnObject;
                hitDistance = m_smoothedLastHit.Distance;
                return true;
            }
            else
            {
                hitEntity = null;
                return false;
            }
        }

        /// <summary>
        /// Setup the caster to do remote queries.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public void Start()
        {
            m_queryTimer = 0f;
            ClearLastHit();
        }

        private static readonly ProfilerMarker UpdatePerfMarker = new ProfilerMarker("[Showcase] RemotePointerCaster.Update");
        /// <summary>
        /// Update pump for this class. Checks if any in-flight query has completed,
        /// updates the cached result, and sends out new queries.
        /// </summary>
        /// <remarks>
        /// This should be call once per frame.
        /// </remarks>
        public void Update(string pointerName, SceneQueryType sceneQueryType, LayerMask[] prioritizedLayerMasks, RayStep[] rays)
        {
            using (UpdatePerfMarker.Auto())
            {
                // Only do remote queries if we do have a connection.
                if (!AzureRemoteRenderingConnected)
                {
                    ClearPendingCasts();
                    ClearLastHit();
                    return;
                }

                // Check if any pending remote query has completed.
                Task<RemoteCastHit> mostRecentCompletedCast = GetMostRecentCompletedCast();
                if (mostRecentCompletedCast != null)
                {
                    if (mostRecentCompletedCast.IsFaulted)
                    {
                        Debug.LogWarning($"Remote query for '{pointerName}' has failed: {mostRecentCompletedCast.Exception.InnerException}");
                        ClearLastHit();
                    }
                    else
                    {
                        UpdateLastHit(mostRecentCompletedCast.Result);
                        UpdateSmoothing(Time.deltaTime);
                    }
                }
                else
                {
                    // The current hit GameObject could be deleted by some other script, so verify the cached hit.
                    ValidateLastHit();
                    UpdateSmoothing(Time.deltaTime);
                }

                // Check if we're ready to send out a new query.
                if (UpdateQueryTimer(Time.deltaTime, out RemoteCastData castData))
                {
                    // Send out new cast
                    castData.PendingTask = RemoteCastPointer(pointerName, sceneQueryType, castData, prioritizedLayerMasks, rays, cancellationTokenSource.Token);
                    m_pendingCasts.Enqueue(castData);
                }
            }
        }

        /// <summary>
        /// Stop any in-flight remote queries and invalidate any cached results.
        /// </summary>
        /// <remarks>
        /// </remarks>
        public void Stop()
        {
            ClearPendingCasts();
            ClearLastHit();
        }

        private void ValidateLastHit()
        {
            if (m_currentLastHit.HitObject == null)
            {
                ClearLastHit();
            }
        }

        private void ClearLastHit()
        {
            m_hasLastHit = false;
            m_smoothedLastHit = default;
            m_previousLastHit = default;
            m_currentLastHit = default;
        }

        private void UpdateLastHit(RemoteCastHit newLastHit)
        {
            // If the remote raycast didn't hit any GameObject, early out.
            if (newLastHit.HitObject == null)
            {
                ClearLastHit();
                return;
            }

            m_previousLastHit = m_smoothedLastHit;
            m_currentLastHit = newLastHit;
            m_smoothedLastHit = newLastHit;

            // Check if we hit the same object and can actually do smoothing.
            if (m_hasLastHit && m_previousLastHit.HitObject == m_currentLastHit.HitObject)
            {
                m_smoothingTimer = CastQueryInterval;
            }
            else
            {
                m_smoothingTimer = 0f;
            }

            m_hasLastHit = true;
        }

        private void UpdateSmoothing(float deltaTime)
        {
            if (!m_hasLastHit || m_smoothingTimer <= 0f)
            {
                return;
            }

            m_smoothingTimer -= deltaTime;
            float t = m_smoothingTimer / CastQueryInterval;

            RemoteCastHit updatedLastHit = m_smoothedLastHit;
            updatedLastHit.HitPointOnObject = Vector3.Lerp(m_currentLastHit.HitPointOnObject, m_previousLastHit.HitPointOnObject, t);
            updatedLastHit.HitNormalOnObject = Vector3.Slerp(m_currentLastHit.HitNormalOnObject, m_previousLastHit.HitNormalOnObject, t);
            m_smoothedLastHit = updatedLastHit;
        }

        private Task<RemoteCastHit> GetMostRecentCompletedCast()
        {
            // Azure Remote Rendering API queries are usually processed and completed in order.
            // So, we can assume there won't ever be a more recent completed raycast, while a previous is not yet completed.
            // Therefore no need to check the entire queue for a more recent completed task.
            Task<RemoteCastHit> completedTask = null;
            while ((m_pendingCasts.Count > 0) && m_pendingCasts.Peek().PendingTask.IsCompleted)
            {
                RemoteCastData completedCast = m_pendingCasts.Dequeue();
                completedTask = completedCast.PendingTask;

                // Return the raycast data to the cache so it can be reused.
                completedCast.Clear();
                if (m_castDataCache.Count < MaxCastDataCacheSize)
                {
                    m_castDataCache.Push(completedCast);
                }
            }

            return completedTask;
        }

        private bool UpdateQueryTimer(float deltaTime, out RemoteCastData castData)
        {
            m_queryTimer -= Time.deltaTime;
            if (m_queryTimer <= 0f)
            {
                m_queryTimer += CastQueryInterval;

                // Try to fetch from the cache
                if (m_castDataCache.Count > 0)
                {
                    castData = m_castDataCache.Pop();
                }
                else
                {
                    castData = RemoteCastData.Create();
                }

                return true;
            }
            else
            {
                castData = default;
                return false;
            }
        }

        private void ClearPendingCasts()
        {
            if (m_pendingCasts.Count == 0)
            {
                return;
            }

            // Cancel and void any pending raycasting.
            cancellationTokenSource.Cancel();
            cancellationTokenSource = new CancellationTokenSource();

            // Move everything back into the cache.
            foreach (RemoteCastData pendingRaycast in m_pendingCasts)
            {
                // Access the "Exception" property to mark any potential exception as "handled".
                _ = pendingRaycast.PendingTask.Exception?.InnerException;

                pendingRaycast.Clear();
                if (m_castDataCache.Count < MaxCastDataCacheSize)
                {
                    m_castDataCache.Push(pendingRaycast);
                }
            }

            m_pendingCasts.Clear();
        }

        private async Task<RemoteCastHit> RemoteCastPointer(string pointerName, SceneQueryType sceneQueryType, RemoteCastData castData, LayerMask[] originalPrioritizedLayerMasks, RayStep[] rays, CancellationToken cancellationToken)
        {
            switch (sceneQueryType)
            {
                case SceneQueryType.BoxRaycast:
                    Debug.LogWarning($"Box Raycasting Mode not supported for remote casting. Using Simple Raycasting Mode instead.");
                    return await RemoteRaycast(castData, originalPrioritizedLayerMasks, rays, cancellationToken);
                case SceneQueryType.SphereCast:
                    Debug.LogWarning($"Sphere Raycasting Mode not supported for remote casting. Using Simple Raycasting Mode instead.");
                    return await RemoteRaycast(castData, originalPrioritizedLayerMasks, rays, cancellationToken);
                case SceneQueryType.SimpleRaycast:
                    return await RemoteRaycast(castData, originalPrioritizedLayerMasks, rays, cancellationToken);
                case SceneQueryType.SphereOverlap:
                    return await RemoteSphereOverlap(castData, originalPrioritizedLayerMasks, rays, cancellationToken);
                default:
                    string error = $"Invalid raycast mode {sceneQueryType} for {pointerName} pointer.";
                    Debug.LogError(error);
                    throw new NotImplementedException(error);
            }
        }

        private async Task<RemoteCastHit> RemoteRaycast(RemoteCastData castData, LayerMask[] originalPrioritizedLayerMasks, RayStep[] rays, CancellationToken cancellationToken)
        {
            // Reuse cached list instances to prevent GC allocs.
            List<Task<RayCastQueryResult>> apiQueries = castData.RaycastsQueries;
            List<RayStep> raySteps = castData.RaySteps;
            List<LayerMask> prioritizedLayerMasks = castData.PrioritizedLayerMasks;

            // Make copies of arguments, in case the original pointer data changes.
            raySteps.AddRange(rays);
            prioritizedLayerMasks.AddRange(originalPrioritizedLayerMasks);

            // First send out all queries at once.
            for (int i = 0; i < raySteps.Count; i++)
            {
                RayStep step = raySteps[i];
                RayCast ray = new RayCast(step.Origin.toRemotePos(), step.Terminus.toRemotePos(), HitCollectionPolicy.ClosestHits);

                apiQueries.Add(AppServices.RemoteRendering?.PrimaryMachine.Actions.RayCastQueryAsync(ray));
            }

            // Then process their results sequentially.
            float rayStartDistance = 0;
            for (int i = 0; i < apiQueries.Count; i++)
            {
                Task<RayCastQueryResult> pendingQuery = apiQueries[i];

                // Wait for the next query to complete.
                while (!pendingQuery.IsCompleted)
                {
                    await Task.Yield();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                // Check that the query was successful.
                if (pendingQuery.IsFaulted)
                {
                    throw pendingQuery.Exception.InnerException;
                }

                // Retrieve its result.
                List<RayCastHit> hits = castData.RaycastsResults;
                pendingQuery.Result.GetHits(hits);

                // Check that this query actually hit anything.
                if (hits.Count == 0)
                {
                    rayStartDistance += raySteps[i].Length;
                    continue;
                }

                // Evaluate the results.
                GameObject localObject = null;
                int hitIndex = int.MaxValue;
                int hitPriorityIndex = int.MaxValue;

                // Find the closest object on the highest priority layer.
                for (int j = 0; j < hits.Count; j++)
                {
                    // There is a time window when loading models where the model is already present on the remote side
                    // and raycasts are hitting it, but the model entities is not yet present on the local client side.
                    if (!hits[j].HitObject.Valid)
                    {
                        continue;
                    }

                    GameObject gameObject = hits[j].HitObject.GetExistingParentGameObject();
                    if (gameObject == localObject)
                    {
                        continue;
                    }

                    int priorityLayerMaskIndex = GetLayerMaskPriorityIndex(prioritizedLayerMasks, gameObject.layer);
                    if (priorityLayerMaskIndex < hitPriorityIndex)
                    {
                        localObject = gameObject;
                        hitIndex = j;
                        hitPriorityIndex = priorityLayerMaskIndex;

                        if (hitPriorityIndex == 0)
                        {
                            break;
                        }
                    }
                }

                // Prepare and return the found result.
                if (localObject != null)
                {
                    RemoteCastHit result;
                    result.HitEntity = hits[hitIndex].HitObject;
                    result.HitObject = localObject;
                    result.HitPointOnObject = hits[hitIndex].HitPosition.toUnityPos();
                    result.HitNormalOnObject = hits[hitIndex].HitNormal.toUnity();
                    result.PriorityLayerIndex = hitPriorityIndex;
                    result.Ray = raySteps[i];
                    result.RayStepIndex = i;
                    result.Distance = rayStartDistance + (float)hits[hitIndex].DistanceToHit;
                    result.Volume = 0f;
                    return result;
                }
            }

            return default;
        }

        private async Task<RemoteCastHit> RemoteSphereOverlap(RemoteCastData castData, LayerMask[] originalPrioritizedLayerMasks, RayStep[] rays, CancellationToken cancellationToken)
        {
            // Reuse cached list instances to prevent GC allocs.
            List<Task<SpatialQueryResult>> apiQueries = castData.SpatialQueries;
            List<RayStep> raySteps = castData.RaySteps;
            List<LayerMask> prioritizedLayerMasks = castData.PrioritizedLayerMasks;

            // Make copies of arguments, in case the original pointer data changes.
            raySteps.AddRange(rays);
            prioritizedLayerMasks.AddRange(originalPrioritizedLayerMasks);

            // First send out all queries at once.
            for (int i = 0; i < raySteps.Count; i++)
            {
                RayStep step = raySteps[i];
                SpatialQuerySphere sphere;
                sphere.MaxResults = 0;
                sphere.OverlapTestMode = OverlapTestMode.Primitives;
                sphere.Sphere.Center = step.Origin.toRemotePos();
                sphere.Sphere.Radius = step.Terminus.z; // Terminus = Vector3.forward * SphereCastRadius

                apiQueries.Add(AppServices.RemoteRendering?.PrimaryMachine.Actions.SpatialQuerySphereAsync(sphere));
            }

            // Then process their results sequentially.
            for (int i = 0; i < apiQueries.Count; i++)
            {
                Task<SpatialQueryResult> pendingQuery = apiQueries[i];

                // Wait for the next query to complete.
                while (!pendingQuery.IsCompleted)
                {
                    await Task.Yield();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                // Check that the query was successful.
                if (pendingQuery.IsFaulted)
                {
                    throw pendingQuery.Exception.InnerException;
                }

                // Retrieve its result.
                List<MeshComponent> overlaps = castData.SpatialResults;
                pendingQuery.Result.GetOverlaps(overlaps);

                // Check that this query actually hit anything.
                if (overlaps.Count == 0)
                {
                    continue;
                }

                // Evaluate the results.
                GameObject smallest = null;
                Entity smallestEntity = null;
                float smallestDistance = Mathf.Infinity;
                float smallestVolume = Mathf.Infinity;
                int smallestPriorityIndex = int.MaxValue;

                // Find the closest and smallest touched object.
                for (int j = 0; j < overlaps.Count; j++)
                {
                    // There is a time window when loading models where the model is already present on the remote side
                    // and queries are hitting it, but the model entities is not yet present on the local client side.
                    Entity hitObject = overlaps[j].Valid ? overlaps[j].Owner : null;
                    if (hitObject == null || !hitObject.Valid)
                    {
                        continue;
                    }

                    GameObject gameObject = GetParentEntityObjectWithNearInteractionGrabbable(overlaps[j].Owner);
                    if (gameObject == null)
                    {
                        continue;
                    }

                    UnityEngine.Bounds localSpaceBounds = overlaps[j].Mesh.Bounds.toUnity();
                    UnityEngine.Bounds worldSpaceBounds = localSpaceBounds.Transform(hitObject.LocalToGlobalMatrix.toUnity());
                    float distance = Vector3.Distance(raySteps[i].Origin, worldSpaceBounds.ClosestPoint(raySteps[i].Origin));
                    float volume = worldSpaceBounds.Volume();

                    float distanceDiff = smallestDistance - distance;

                    int priorityLayerMaskIndex = GetLayerMaskPriorityIndex(prioritizedLayerMasks, gameObject.layer);
                    if (priorityLayerMaskIndex < smallestPriorityIndex || ((smallestPriorityIndex == priorityLayerMaskIndex) && ((distanceDiff > MIN_DIST_DIFF) || ((Math.Abs(distanceDiff) < MIN_DIST_DIFF) && (volume < smallestVolume)))))
                    {
                        smallest = gameObject;
                        smallestEntity = hitObject;
                        smallestDistance = distance;
                        smallestVolume = volume;
                        smallestPriorityIndex = priorityLayerMaskIndex;
                    }
                }

                // Prepare and return the found result.
                if (smallest != null)
                {
                    RemoteCastHit result;
                    result.HitEntity = smallestEntity;
                    result.HitObject = smallest;
                    result.HitPointOnObject = raySteps[i].Origin;
                    result.HitNormalOnObject = Vector3.zero;
                    result.HitNormalOnObject = Vector3.zero;
                    result.PriorityLayerIndex = smallestPriorityIndex;
                    result.Ray = raySteps[i];
                    result.RayStepIndex = i;
                    result.Distance = smallestDistance;
                    result.Volume = smallestVolume;
                    return result;
                }
            }

            return default;
        }

        private GameObject GetParentEntityObjectWithNearInteractionGrabbable(Entity entity)
        {
            // Start with the closest GameObject in the hierarchy.
            GameObject gameObject = entity.GetExistingParentGameObject();
            UnityEngine.Transform transform = (gameObject != null) ? gameObject.transform : null;

            // Check each transform if it has a "NearInteractionGrabbable".
            while ((transform != null) && (!transform.TryGetComponent<NearInteractionGrabbable>(out _)))
            {
                // If we leave the remote Entity hierarchy, return no finding.
                if (!transform.TryGetComponent<RemoteEntitySyncObject>(out _))
                {
                    return null;
                }

                // Move up the hierarchy.
                transform = transform.parent;
            }

            return (transform != null) ? transform.gameObject : null;
        }

        private static int GetLayerMaskPriorityIndex(LayerMask[] prioritizedLayerMasks, int layer)
        {
            for (int i = 0; i < prioritizedLayerMasks.Length; i++)
            {
                if ((prioritizedLayerMasks[i] & (1 << layer)) != 0)
                {
                    return i;
                }
            }

            return int.MaxValue;
        }

        private static int GetLayerMaskPriorityIndex(List<LayerMask> prioritizedLayerMasks, int layer)
        {
            for (int i = 0; i < prioritizedLayerMasks.Count; i++)
            {
                if ((prioritizedLayerMasks[i] & (1 << layer)) != 0)
                {
                    return i;
                }
            }

            return int.MaxValue;
        }
    }
}
