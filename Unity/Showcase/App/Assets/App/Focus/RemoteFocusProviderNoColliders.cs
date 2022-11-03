// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.Azure.RemoteRendering.Unity;
using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.Physics;
using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Input
{
    /// <summary>
    /// This is a specialized Mixed Reality Toolkit (MRTK) focus provider for Azure Remote Rendering (ARR). This 
    /// class will execute both local and remote ray casts, and give focus to closest object, be it either local or
    /// remote.
    /// 
    /// When using this focus provider, focusing behaves as follows:
    ///
    /// (1) For each MRTK pointer asynchronously query for remote focus, only if all previous remote queries have 
    ///     completed.
    /// (2) Execute local focus logic. Note, this will clear any previously focused remote targets, and fire the 
    ///     related focus events.
    /// (3) Compare the last known remote target with the current local target. The closest target becomes the new 
    ///     focus target. This will also fire the related focus events.
    /// (4) Repeat steps 1-4
    ///
    /// Note that the extra layer of 'focus' events may introduce a performance hit, if the application has many or
    /// complex focus handlers.
    ///
    /// Also note that local objects still require an Unity collider in order to obtain focus. While remote object do 
    /// not require an Unity collider. If a remote object has an Unity collider, then the local focus logic may override
    /// the remote focus logic for that remote object.
    ///
    /// To help with debugging the remote ray casts, set _debugRayCasts to true.
    /// </summary>
    public class RemoteFocusProviderNoCollider : BaseCoreSystem, IRemoteFocusProvider, IPointerPreferences
    {
        public RemoteFocusProviderNoCollider(
            MixedRealityInputSystemProfile profile) : base(profile)
        {
            _localFocusProvider = new FocusProvider(profile);
        }

        private readonly FocusProvider _localFocusProvider;
        private IMixedRealityInputSystem _inputSystem;
        private float _lastRemoteRayCast;
        private bool _isQuitting;
        private readonly Dictionary<uint, RemotePointerResult> _remotePointerData = new Dictionary<uint, RemotePointerResult>();
        private readonly List<(IMixedRealityPointer pointer, GameObject oldFocus, GameObject newFocus)> _pendingFocusChanges = new List<(IMixedRealityPointer, GameObject, GameObject)>();
        private readonly Dictionary<GameObject, int> _localFocusExits = new Dictionary<GameObject, int>();
        private readonly RayCastTasks _remoteRayCasts = new RayCastTasks(_raycastMaxCacheSize);

        private static RayCastHit _invalidRemoteResult = new RayCastHit() { HitObject = null };
        private static RayCastHit[] _invalidRemoteResults = new RayCastHit[1] { _invalidRemoteResult };

        /// <summary>
        /// This is the max number of remote ray casts that can executed simultaneously.
        /// </summary>
        private const int _raycastMaxCacheSize = 10;

        #region Public Properties
        /// <summary>
        /// The max rate to perform a remote ray casts
        /// </summary>
        public float RemoteRayCastRate { get; set; } = 1.0f / 30.0f;

        /// <summary>
        /// Get or set if debug ray casts should be drawn within the app.
        /// </summary>
        public bool DebugRayCasts { get; set; } = false;
        #endregion Public Properties

        #region IMixedRealityService
        public override void Initialize()
        {
            Application.quitting += OnApplicationQuit;
            _localFocusProvider.Initialize();
            _inputSystem = CoreServices.InputSystem;
        }

        public override void Destroy()
        {
            Application.quitting -= OnApplicationQuit;
            _localFocusProvider.Destroy();
        }

        public override void Update()
        {
            PreLocalRaycast();
            _localFocusProvider.Update();
            PostLocalRaycast();

            DoRemoteCasts();
        }
        #endregion IMixedRealityService

        #region IMixedRealityFocusProvider
        public float GlobalPointingExtent => ((IMixedRealityFocusProvider)_localFocusProvider).GlobalPointingExtent;

        public LayerMask[] FocusLayerMasks => _localFocusProvider.FocusLayerMasks;

        public Camera UIRaycastCamera => _localFocusProvider.UIRaycastCamera;

        public IMixedRealityPointer PrimaryPointer => _localFocusProvider.PrimaryPointer;

        public uint GenerateNewPointerId()
        {
            return _localFocusProvider.GenerateNewPointerId();
        }

        public GameObject GetFocusedObject(IMixedRealityPointer pointer)
        {
            return _localFocusProvider.GetFocusedObject(pointer);
        }

        public bool IsPointerRegistered(IMixedRealityPointer pointer)
        {
            return _localFocusProvider.IsPointerRegistered(pointer);
        }

        public void OnSourceDetected(SourceStateEventData eventData)
        {
            _localFocusProvider.OnSourceDetected(eventData);
            foreach (var pointer in eventData.InputSource.Pointers)
            {
                GetRemotePointerResult(pointer, true);
            }
        }

        public void OnSourceLost(SourceStateEventData eventData)
        {
            _localFocusProvider.OnSourceLost(eventData);
            foreach (var pointer in eventData.InputSource.Pointers)
            {
                ReleasePointer(pointer);
            }
        }

        public void OnSpeechKeywordRecognized(SpeechEventData eventData)
        {
            _localFocusProvider.OnSpeechKeywordRecognized(eventData);
        }

        public bool RegisterPointer(IMixedRealityPointer pointer)
        {
            bool result = _localFocusProvider.RegisterPointer(pointer);
            GetRemotePointerResult(pointer, true);
            return result;
        }

        public void SubscribeToPrimaryPointerChanged(PrimaryPointerChangedHandler handler, bool invokeHandlerWithCurrentPointer)
        {
            _localFocusProvider.SubscribeToPrimaryPointerChanged(handler, invokeHandlerWithCurrentPointer);
        }

        public bool TryGetFocusDetails(IMixedRealityPointer pointer, out FocusDetails focusDetails)
        {
            return _localFocusProvider.TryGetFocusDetails(pointer, out focusDetails);
        }

        public bool TryOverrideFocusDetails(IMixedRealityPointer pointer, FocusDetails focusDetails)
        {
            return _localFocusProvider.TryOverrideFocusDetails(pointer, focusDetails);
        }

        public bool UnregisterPointer(IMixedRealityPointer pointer)
        {
            var result = _localFocusProvider.UnregisterPointer(pointer);
            ReleasePointer(pointer);
            return result;
        }

        public void UnsubscribeFromPrimaryPointerChanged(PrimaryPointerChangedHandler handler)
        {
            _localFocusProvider.UnsubscribeFromPrimaryPointerChanged(handler);
        }

        IEnumerable<T> IMixedRealityFocusProvider.GetPointers<T>()
        {
            return _localFocusProvider.GetPointers<T>();
        }
        #endregion IMixedRealityFocusProvider

        #region IPointerPreferences
        public PointerBehavior GazePointerBehavior
        {
            get => _localFocusProvider.GazePointerBehavior;
            set => _localFocusProvider.GazePointerBehavior = value;
        }

        public PointerBehavior GetPointerBehavior(IMixedRealityPointer pointer)
        {
            return _localFocusProvider.GetPointerBehavior(pointer);
        }

        PointerBehavior IPointerPreferences.GetPointerBehavior<T>(Handedness handedness, InputSourceType sourceType)
        {
            return _localFocusProvider.GetPointerBehavior<T>(handedness, sourceType);
        }

        void IPointerPreferences.SetPointerBehavior<T>(Handedness handedness, InputSourceType inputType, PointerBehavior pointerBehavior)
        {
            _localFocusProvider.SetPointerBehavior<T>(handedness, inputType, pointerBehavior);
        }
        #endregion IPointerPreferences

        #region IRemoteFocusProvider
        /// <summary>
        /// Get the pointer's remote focus information. This result contains which remote Entity the pointer is currently
        /// focused on.
        /// </summary>
        public IRemotePointerResult GetRemoteResult(IMixedRealityPointer pointer)
        {
            return GetRemotePointerResult(pointer, false);
        }

        /// <summary>
        /// Get the entity from a pointer target. This makes it easier to consume Input events which return game objects.
        /// Upon handling those events, you can use this method to resolve the entity that was focused.
        /// </summary>
        public Entity GetEntity(IMixedRealityPointer pointer, GameObject pointerTarget)
        {
            Entity result = null;
            RemotePointerResult remoteResult = GetRemotePointerResult(pointer, false);

            if (remoteResult != null)
            {
                if (remoteResult.IsTargetValid && pointerTarget == NearestFocusableGameObject(remoteResult.TargetEntity))
                {
                    result = remoteResult.TargetEntity;
                }
                else if (remoteResult.IsPreviousTargetValid && pointerTarget == NearestFocusableGameObject(remoteResult.PreviousTargetEntity))
                {
                    result = remoteResult.PreviousTargetEntity;
                }

                // If the remote ray-cast didn't find an entity, it's possible the entity doesn't have remote mesh
                // colliders. So search for the nearest sync object, and use its entity.
                if (result == null && !remoteResult.IsTargetValid)
                {
                    result = pointerTarget.GetComponentInChildren<RemoteEntitySyncObject>()?.Entity;
                }
            }

            return result;
        }

        /// <summary>
        /// Try switching focus to a child object. This is will fail if the current target is not a parent of the child.
        /// </summary>
        public void TryFocusingChild(IMixedRealityPointer pointer, GameObject childTarget)
        {
            RemotePointerResult remoteResult = GetRemotePointerResult(pointer, false);
            Entity childEntity = childTarget.GetComponentInParent<RemoteEntitySyncObject>()?.Entity;

            if (remoteResult != null)
            {
                remoteResult.TrySettingOverrideTarget(childEntity);
            }
        }
        #endregion IRemoteFocusProvider

        #region Private Methods
        /// <summary>
        /// Listen for application quits, and stop ray casting if application is shutting down.
        /// </summary>
        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        /// <summary>
        /// Get the pointer's remote focus information. This result contains which remote Entity the pointer is currently
        /// focused on.
        /// </summary>
        private RemotePointerResult GetRemotePointerResult(IMixedRealityPointer pointer, bool allowCreate = true)
        {
            if (pointer == null)
            {
                return null;
            }

            RemotePointerResult result = null;
            if (!_remotePointerData.TryGetValue(pointer.PointerId, out result) && allowCreate)
            {
                result = _remotePointerData[pointer.PointerId] = new RemotePointerResult(pointer);
            }
            return result;
        }

        /// <summary>
        /// Dispose or release all remote data associated with the given pointer.
        /// </summary>
        private void ReleasePointer(IMixedRealityPointer pointer)
        {
            if (pointer == null)
            {
                return;
            }

            ReleasePointer(pointer.PointerId);
        }

        /// <summary>
        /// Dispose or release all remote data associated with the given pointer.
        /// </summary>
        private void ReleasePointer(uint pointerId)
        {
            RemotePointerResult data = null;
            if (_remotePointerData.TryGetValue(pointerId, out data))
            {
                data.Dispose();
                _remotePointerData.Remove(pointerId);
            }
        }

        /// <summary>
        /// Prepare remote data to handle local focus changes, and delete invalid remote pointer data objects.
        /// </summary> 
        private void PreLocalRaycast()
        {
            List<uint> toRemove = new List<uint>();
            foreach (var remoteResultEntry in _remotePointerData)
            {
                // If pointer has been deleted, don't update and clean it up
                var remoteResult = remoteResultEntry.Value;
                if (remoteResult == null ||
                    remoteResult.Pointer == null ||
                    remoteResult.IsDisposed)
                {
                    toRemove.Add(remoteResultEntry.Key);
                    continue;
                }

                // Save current target, and clear it
                remoteResult.PreLocalRaycast();
            }

            foreach (uint pointerId in toRemove)
            {
                ReleasePointer(pointerId);
            }
        }

        /// <summary>
        /// Commit remote focus changes after completing local focusing.
        /// </summary>
        private void PostLocalRaycast()
        {
            Debug.Assert(_localFocusExits.Count == 0, "Data should have been cleared earlier");

            foreach (var remoteResultEntry in _remotePointerData)
            {
                var remoteResult = remoteResultEntry.Value;

                // Merge local and remote focus data. If merge fails, stop proccessing pointer.
                FocusDetails oldLocalFocusDetails = default;
                _localFocusProvider.TryGetFocusDetails(remoteResult.Pointer, out oldLocalFocusDetails);
                if (!remoteResult.PostLocalRaycast(oldLocalFocusDetails))
                {
                    continue;
                }

                // Track which local objects have exited local focus
                int totalFocusExits = 0;
                if (oldLocalFocusDetails.Object != null)
                {
                    _localFocusExits.TryGetValue(oldLocalFocusDetails.Object, out totalFocusExits);
                }

                // Override the local providers cache with the remote results
                if (remoteResult.IsTargetValid)
                {
                    if (oldLocalFocusDetails.Object != null)
                    {
                        _localFocusExits[oldLocalFocusDetails.Object] = totalFocusExits + 1;
                    }

                    remoteResult.Pointer.OnPreSceneQuery();
                    remoteResult.Pointer.OnPreCurrentPointerTargetChange();
                    remoteResult.Pointer.IsTargetPositionLockedOnFocusLock = true;
                    _localFocusProvider.TryOverrideFocusDetails(remoteResult.Pointer, remoteResult.RemoteDetails);
                    remoteResult.Pointer.OnPostSceneQuery();

                    _pendingFocusChanges.Add((
                        remoteResult.Pointer,
                        oldLocalFocusDetails.Object,
                        remoteResult.RemoteDetails.Object));
                }
                else if (oldLocalFocusDetails.Object != null)
                {
                    // Prevent focus exited from firing
                    _localFocusExits[oldLocalFocusDetails.Object] = int.MinValue;
                }
            }

            foreach (var focusChange in _pendingFocusChanges)
            {
                // Focus will change every frame when a remote item is focused
                RaiseFocusEvents(focusChange.pointer, focusChange.oldFocus, focusChange.newFocus);
            }

            _pendingFocusChanges.Clear();
            _localFocusExits.Clear();
        }

        /// <summary>
        /// Raises the Focus Events to the Input Manger.
        /// </summary>
        private void RaiseFocusEvents(IMixedRealityPointer pointer, GameObject oldFocused, GameObject newFocused)
        { 
            if (_inputSystem == null)
            {
                return;
            }

            _inputSystem.RaisePreFocusChanged(
                pointer,
                oldFocused,
                newFocused);

            // Fire focus exited if this is the last pointer exiting the object's focus
            int focusExits = 0;
            if (oldFocused != null)
            {
                focusExits = _localFocusExits[oldFocused];
            }
            if (focusExits > 0)
            {
                _localFocusExits[oldFocused] = --focusExits;
                if (focusExits == 0)
                {
                    _inputSystem.RaiseFocusExit(
                        pointer,
                        oldFocused);
                }
            }

            _inputSystem.RaiseFocusEnter(
                pointer,
                newFocused);

            _inputSystem.RaiseFocusChanged(
                pointer,
                oldFocused,
                newFocused);
        }

        /// <summary>
        /// Find the nearest parent object that has been marked as focusable.
        /// </summary>
        /// <param name="entity">
        /// The remote Entity where the search starts.
        /// </param>
        private static GameObject NearestFocusableGameObject(Entity entity)
        {
            GameObject result = null;
            while (entity != null && entity.Valid && result == null)
            {
                result = entity.GetExistingGameObject();
                entity = entity.Parent;
            }

            // Unity overrides the == operator, so 'result' may not really be null.
            // Unity doesn't (and can't) override the Null-conditional operator (?),
            // so we can't also rely on this operator.
            //
            // If Unity claims this is null, because its native object has been
            // destroyed for some reason, force a real null value to be returned so to
            // prevent bugs by the callers of this function.
            // See this Unity blog post for details:
            // https://blogs.unity3d.com/2014/05/16/custom-operator-should-we-keep-it/
            // And this Unity documentation on the Object.operator ==:
            // https://docs.unity3d.com/ScriptReference/Object-operator_eq.html
            if (result == null)
            {
                result = null;
            }

            return result;
        }

        /// <summary>
        /// Execute the remote casts (ray cast or star cast) if all pending ray casts have completed, and client is connected to a remote
        /// rendering session.
        /// </summary>
        private void DoRemoteCasts()
        {
            var machine = AppServices.RemoteRendering?.PrimaryMachine;

            // Exit if there is no connection for doing remote ray casts, and ignore/clear any pending ray casts.
            if (_isQuitting ||
                machine == null ||
                machine.Session.Connection.ConnectionStatus != ConnectionStatus.Connected)
            {
                _remoteRayCasts.Clear();
                return;
            }

            // Throttle remote ray-casts, and don't start another remote ray-cast until old operations have completed.
            float currentTime = Time.time;
            if ((currentTime - _lastRemoteRayCast < RemoteRayCastRate) ||
                (!_remoteRayCasts.IsCompletedOrEmpty()))
            {
                return;
            }

            // Execute a remote ray-cast for all pointers.
            _remoteRayCasts.Clear();
            foreach (var remotePointerData in _remotePointerData)
            {
                // If remote pointer was already updated this frame avoid another update immediately.
                if (!remotePointerData.Value.Staged)
                {
                    _remoteRayCasts.Add(DoRemoteCast(remotePointerData.Value));
                }
            }

            if (_remoteRayCasts.Count > 0)
            {
                _lastRemoteRayCast = currentTime;
            }
        }

        /// <summary>
        /// Execute a remote cast (ray cast or star cast) for the given pointer.
        /// </summary>
        private async Task<RayCastHit[]> DoRemoteCast(RemotePointerResult pointerResult)
        {
            IMixedRealityPointer pointer = pointerResult?.Pointer;
            if (pointer == null)
            {
                return default;
            }

            // If the pointer is locked, keep the focused object the same.
            // This will ensure that we execute events on those objects
            // even if the pointer isn't pointing at them.
            if (pointer.IsFocusLocked && pointer.IsTargetPositionLockedOnFocusLock)
            {
                return default;
            }

            Task<RayCastHit[]> castTask = null;
            switch (pointer.SceneQueryType)
            {
                case SceneQueryType.SimpleRaycast:
                    castTask = DoRaycast(pointerResult);
                    break;

                case SceneQueryType.SphereCast:
                case SceneQueryType.SphereOverlap:
                    castTask = DoMultiRaycast(pointerResult);
                    break;

                default:
                    castTask = Task.FromResult(_invalidRemoteResults);
                    break;
            }

            RayCastHit[] rayCastResults = await castTask;

            if (!(pointerResult.IsDisposed) &&
                (pointer != null) &&
                !(pointer.IsFocusLocked && pointer.IsTargetPositionLockedOnFocusLock))
            {
                pointerResult.StageRemoteData(rayCastResults);
            }

            return rayCastResults;
        }

        /// <summary>
        /// Execute a remote ray cast for the given pointer.
        /// </summary>
        private Task<RayCastHit[]> DoRaycast(RemotePointerResult pointerResult)
        {
            var pointer = pointerResult?.Pointer;
            if (pointer == null ||
                pointer.Rays == null || 
                pointer.Rays.Length == 0)
            {
                return null;
            }

            Vector3 start;
            Vector3 direction;
            float distance;

            if (pointer.Rays.Length == 1)
            {
                RayStep ray = pointer.Rays[0];
                start = ray.Origin;
                direction = ray.Direction;
                distance = (pointer is BaseControllerPointer) ? ((BaseControllerPointer)pointer).PointerExtent : ray.Length;
            }
            else
            {
                RayStep firstStep = pointer.Rays[0];
                RayStep lastStep = pointer.Rays[pointer.Rays.Length - 1];

                start = firstStep.Origin;
                direction = (lastStep.Terminus - start);
                distance = direction.magnitude;
                direction = direction.normalized;
            }

            return DoRemoteRaycast(
                pointerResult,
                start,
                direction,
                distance);
        }

        /// <summary>
        /// Execute a remote star cast (multi-ray cast) for the given pointer.
        /// </summary>
        private async Task<RayCastHit[]> DoMultiRaycast(RemotePointerResult pointerResult)
        {
            var pointer = pointerResult?.Pointer;
            if (pointer == null ||
                pointer.Rays == null)
            {
                return null;
            }
            
            RayCastTasks rayCastTasks = pointerResult.RayCasts;
            rayCastTasks.Clear();

            int rayCount = Math.Min(pointer.Rays.Length, rayCastTasks.MaxSize);
            for (int i = 0; i < rayCount; i++)
            {
                RayStep step = pointer.Rays[i];
                if (step.Length > 0)
                {
                    rayCastTasks.Add(DoRemoteRaycast(pointerResult, step.Origin, step.Direction, step.Length));
                }
            }

            RayCastHit[][] hits = await Task.WhenAll(rayCastTasks);
            return await SortHits(hits);
        }

        /// <summary>
        /// Actually do the work of submitting a ray cast request to the remote session.
        /// </summary>
        private async Task<RayCastHit[]> DoRemoteRaycast(RemotePointerResult pointerResult, Vector3 position, Vector3 direction, float distance)
        {
            RayCast cast = new RayCast(
                position.toRemotePos(),
                direction.toRemoteDir(),
                distance,
                HitCollectionPolicy.ClosestHit);

            if (DebugRayCasts)
            {
                pointerResult.Visualizer.Add(position, direction, distance);
            }

            RayCastHit[] hits = null;
            try
            {
                var result = await AppServices.RemoteRendering.PrimaryMachine.Actions.RayCastQueryAsync(cast);
                hits = result.Hits;
            }
            catch (Exception ex)
            {
                Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}",  $"Failed to execute a remote ray cast ({cast.StartPos.toUnityPos()}) -> ({cast.EndPos.toUnityPos()}). Reason: {ex.Message}");
            }
            return hits;
        }

        /// <summary>
        /// Sort a list of lists of multiple ray cast results. Sort by distances in ascending order. Sorting happens 
        /// on a background thread.
        /// </summary>
        private Task<RayCastHit[]> SortHits(RayCastHit[][] hits)
        {
            return Task.Run(() =>
            {
                if (hits == null)
                {
                    return new RayCastHit[0];
                }

                List<RayCastHit> sortedHits = new List<RayCastHit>();
                foreach (var rayResults in hits)
                {
                    if (rayResults == null)
                    {
                        continue;
                    }

                    foreach (var rayResult in rayResults)
                    {
                        if (rayResult.HitObject != null)
                        {
                            sortedHits.Add(rayResult);
                        }
                    }
                }

                sortedHits.Sort((RayCastHit a, RayCastHit b) =>
                {
                    if (a.DistanceToHit == b.DistanceToHit)
                    {
                        return 0;
                    }
                    else if (a.DistanceToHit < b.DistanceToHit)
                    {
                        return -1;
                    }
                    else
                    {
                        return 1;
                    }
                });

                return sortedHits.ToArray();
            });
        }
        #endregion Private Methods

        #region Private Classes
        /// <summary>
        /// This represents a pointer ray casts performed on the Azure Remote Rendering service.
        /// </summary>
        private class RemotePointerResult : IRemotePointerResult
        {
            private float _lastStagedTime = 0;
            private const uint _rayCastCacheSize = 5;
            private RayCastVisualizer _visualizer = null;
            private InnerData _staged = InnerData.DefaultUncommitted;
            private InnerData _committed = InnerData.DefaultUncommitted;
            private InnerData _override = InnerData.DefaultCommitted;
            private bool _useLocal = true;
            private bool _restoreFocusLockOnce = false;
            private IRemoteSpherePointer _remoteSpherePointer = null;
            private IRemotePointer _remotePointer = null;
            private bool _hidingHandRays = false;
            private FocusDetails _remoteFocusDetails = default;
            private static FocusDetails _emptyFocusDetails = default;

            public RemotePointerResult(IMixedRealityPointer pointer)
            {
                RayCasts = new RayCastTasks(_rayCastCacheSize);
                Pointer = pointer;
                _remotePointer = pointer as IRemotePointer;
                _remoteSpherePointer = pointer as IRemoteSpherePointer;

                if (_remotePointer != null)
                {
                    _remotePointer.RemoteResult = this;
                }

                bool sphereCastPointer = pointer != null && (pointer.SceneQueryType == SceneQueryType.SphereCast || pointer.SceneQueryType == SceneQueryType.SphereOverlap);
                Debug.Assert(!sphereCastPointer || _remoteSpherePointer != null, "The given sphere pointers won't work correctly with remoting, as it doesn't implement IRemoteSpherePointer.");
            }

            /// <summary>
            /// The source of the pointer ray casts.
            /// </summary>
            public IMixedRealityPointer Pointer { get; }

            /// <summary>
            /// Get the latest 'remote' focus details
            /// </summary>
            public FocusDetails RemoteDetails => _useLocal ? _emptyFocusDetails : _remoteFocusDetails;

            /// <summary>
            /// Has this result been disposed of.
            /// </summary>
            public bool IsDisposed { get; private set; }

            /// <summary>
            /// The Azure Remote Rendering ray cast hit.
            /// </summary>
            public RayCastHit RemoteResult => _useLocal ? default : _committed.RemoteResult;

            /// <summary>
            /// Get if remote data has already be staged this frame.
            /// </summary>
            public bool Staged => _lastStagedTime == Time.time;

            /// <summary>
            /// The remote Entity that was hit.
            /// </summary>
            public Entity TargetEntity
            {
                get
                {
                    if (_useLocal || !_committed.IsTargetValid)
                    {
                        return null;
                    }
                    return _committed.TargetEntity;
                }
            }

            /// <summary>
            /// The previously focused remote Entity that was hit.
            /// </summary>
            public Entity PreviousTargetEntity { get; private set; }

            /// <summary>
            /// If true, the pointer ray casts hit a valid remote object.
            /// </summary>
            public bool IsTargetValid { get => TargetEntity != null && TargetEntity.Valid; }

            /// <summary>
            /// If true, the pointer ray casts hit a valid remote object.
            /// </summary>
            public bool IsPreviousTargetValid { get => PreviousTargetEntity != null && PreviousTargetEntity.Valid; }

            /// <summary>
            /// This is a cache of remote ray casts, exposed has a clear-able enumerator. This is helpful when doing 
            /// multiple ray casts every frame, and wanting to avoid allocations when awaiting for the results. 
            /// </summary>
            public RayCastTasks RayCasts { get; private set; }

            /// <summary>
            /// A helper object to aid in visualizing remote ray casts.
            /// </summary>
            public RayCastVisualizer Visualizer
            {
                get
                {
                    if (_visualizer == null)
                    {
                        _visualizer = new RayCastVisualizer(Pointer, _rayCastCacheSize);
                    }
                    return _visualizer;
                }
            }

            /// <summary>
            /// Release the pointer result and its resources
            /// </summary>
            public void Dispose()
            {
                this.IsDisposed = true;
                this._visualizer?.Dispose();
                this._visualizer = null;
                this.PreviousTargetEntity = this.TargetEntity;
                this._useLocal = true;
                this.ReleaseHandRays();

                if (_remotePointer != null && _remotePointer.RemoteResult == this)
                {
                    _remotePointer.RemoteResult = null;
                }
            }

            /// <summary>
            /// Try to stage remote focus data, so it can be committed during the next update.
            /// </summary>
            public void StageRemoteData(RayCastHit[] result)
            {
                if (this.IsDisposed)
                {
                    return;
                }

                Debug.Assert(
                    Time.time > _lastStagedTime,
                    "There was more than one call to StageRemoteData() during the frame update. Check if there are multiple 'in-flight' remote ray casts occuring for this pointer.");

                _lastStagedTime = Time.time;
                if (result == null || result.Length == 0)
                {
                    this._staged.RemoteResult = _invalidRemoteResult;
                }
                else
                {
                    this._staged.RemoteResult = result[0];
                }

                // Copy target entity to our public field
                Entity hitEntity = this._staged.RemoteResult.HitEntity;
                this._staged.TargetEntity = (hitEntity != null && hitEntity.Valid) ? hitEntity : null;

                // Data will be committed during the update loop
                this._staged.Committed = false;

                // Reset the debug visualizer, so it's ready for the next update
                _visualizer?.Reset();
            }

            /// <summary>
            /// Force the hit data from the last remote ray cast to use this override target. This
            /// request is ignored if there is no valid remote hit data, or the override target is
            /// not a child of the current target.
            /// </summary>
            public void TrySettingOverrideTarget(Entity overrideTarget)
            {
                if (_useLocal ||
                    !_committed.IsTargetValid ||
                    overrideTarget == null ||
                    !overrideTarget.Valid ||
                    !overrideTarget.IsChildOf(_committed.TargetEntity))
                {
                    return;
                }

                _override.Committed = false;
                _override.RemoteResult = new RayCastHit()
                {
                    DistanceToHit = _committed.RemoteResult.DistanceToHit,
                    HitNormal = _committed.RemoteResult.HitNormal,
                    HitObject = overrideTarget,
                    HitPosition = _committed.RemoteResult.HitPosition,
                };
                _override.TargetEntity = overrideTarget;
            }

            /// <summary>
            /// Determine if the new remote target has changed, and if so commit the data. The committed data
            /// originates from either the current override target or the staged data. The staged data originates
            /// from the remote ray cast results.
            /// </summary>
            public bool PreLocalRaycast()
            {
                if (this.IsDisposed)
                {
                    return false;
                }

                // If there is an override to commit, temporary release the focus lock
                if (!_override.Committed)
                {
                    _restoreFocusLockOnce = Pointer.IsFocusLocked;
                    Pointer.IsFocusLocked = false;
                }

                // If pointer focused is locked, don't update the committed ray cast data.
                bool success = false;
                if (!Pointer.IsFocusLocked || !Pointer.IsTargetPositionLockedOnFocusLock)
                {
                    this.PreviousTargetEntity = this.TargetEntity;
                    this._useLocal = true;

                    // Commit staged data if not done already
                    if (!_override.Committed)
                    {
                        _committed.RemoteResult = _override.RemoteResult;
                        _committed.TargetEntity = _override.TargetEntity;
                        _committed.Committed = _override.Committed = true;
                    }
                    else if (!_staged.Committed)
                    {
                        _committed.RemoteResult = _staged.RemoteResult;
                        _committed.TargetEntity = _staged.TargetEntity;
                        _committed.Committed = _staged.Committed = true;
                    }

                    if (_committed.IsTargetValid)
                    {
                        HideHandRays();
                    }
                    else
                    {
                        ReleaseHandRays();
                    }

                    success = true;
                }

                return success;
            }

            /// <summary>
            /// After local ray casts have been completed, decide if the remote ray cast result or the local
            /// ray cast result should be exposed to the consumers of this focus provider.
            /// </summary>
            public bool PostLocalRaycast(FocusDetails localResult)
            {
                // If pointer focused is locked, don't update the "use local" flag.
                if (Pointer.IsFocusLocked &&
                    Pointer.IsTargetPositionLockedOnFocusLock)
                {
                    return false;
                }

                // Restore focus lock as needed
                if (_restoreFocusLockOnce)
                {
                    Pointer.IsFocusLocked = true;
                    _restoreFocusLockOnce = false;
                }

                // Decide whether to use the local or remote ray cast result
                if (!Pointer.IsInteractionEnabled)
                {
                    _useLocal = true;
                }
                else if (!_committed.IsTargetValid)
                {
                    _useLocal = true;
                }
                else if (localResult.Object == null)
                {
                    _useLocal = false;
                }
                else if ((float)_committed.RemoteResult.DistanceToHit <= localResult.RayDistance)
                {
                    _useLocal = false;
                }
                else
                {
                    _useLocal = true;
                }

                UpdateFocusDetails();
                return true;
            }

            /// <summary>
            /// Update the current focus details using the remote data, if the remote data is being used.
            /// </summary>
            private void UpdateFocusDetails()
            {
                if (_useLocal)
                {
                    return;
                }

                GameObject target = NearestFocusableGameObject(TargetEntity);
                Transform transform = (target == null) ? null : target.transform;

                RayCastHit currentRemoteHit = RemoteResult;
                float distance = (float)currentRemoteHit.DistanceToHit;
                Vector3 normal = currentRemoteHit.HitNormal.toUnity();
                Vector3 point = currentRemoteHit.HitPosition.toUnityPos();

                _remoteFocusDetails.LastRaycastHit = new MixedRealityRaycastHit()
                {
                    distance = distance,
                    normal = normal,
                    point = point,
                    transform = transform
                };
                _remoteFocusDetails.Normal = normal; 
                _remoteFocusDetails.NormalLocalSpace = (transform == null) ? normal : transform.InverseTransformDirection(normal);
                _remoteFocusDetails.Object = target;
                _remoteFocusDetails.Point = point;
                _remoteFocusDetails.PointLocalSpace = (transform == null) ? point : transform.InverseTransformPoint(point);
                _remoteFocusDetails.RayDistance = distance;
            }

            /// <summary>
            /// Hide hand rays as needed
            /// </summary>
            private void HideHandRays()
            {
                if (_remoteSpherePointer != null && !_hidingHandRays)
                {
                    _remoteSpherePointer.IsNearRemoteGrabbable = true;
                    _hidingHandRays = true;
                }
            }

            /// <summary>
            /// Release hand rays as needed
            /// </summary>
            private void ReleaseHandRays()
            {
                if (_hidingHandRays)
                {
                    _remoteSpherePointer.IsNearRemoteGrabbable = false;
                    _hidingHandRays = false;
                }
            }

            /// <summary>
            /// A struct used to hold both staged and commit remote data 
            /// </summary>
            private struct InnerData
            {
                public static InnerData DefaultCommitted = new InnerData()
                {
                    Committed = true,
                    TargetEntity = null,
                    RemoteResult = _invalidRemoteResult
                };

                public static InnerData DefaultUncommitted = new InnerData()
                {
                    Committed = false,
                    TargetEntity = null,
                    RemoteResult = _invalidRemoteResult
                };

                /// <summary>
                /// Get or set if data has been committed
                /// </summary>
                public bool Committed { get; set; }

                /// <summary>
                /// If true, the pointer ray casts hit a valid remote object.
                /// </summary>
                public bool IsTargetValid
                {
                    get
                    {
                        return TargetEntity != null && TargetEntity.Valid;
                    }
                }

                /// <summary>
                /// The Azure Remote Rendering ray cast hit.
                /// </summary>
                public RayCastHit RemoteResult { get; set; }

                /// <summary>
                /// The remote Entity that was hit.
                /// </summary>
                public Entity TargetEntity { get; set; }
            }
        }

        /// <summary>
        /// This is a cache of remote ray casts, exposed has a clear-able enumerator. This is helpful when doing 
        /// multiple ray casts every frame, and wanting to avoid allocations when awaiting for the results. 
        /// </summary>
        private class RayCastTasks : IEnumerator<Task<RayCastHit[]>>, IEnumerable<Task<RayCastHit[]>>
        {
            private Task<RayCastHit[]>[] cache = null;
            private int current = -1;
            private uint size = 0;

            public RayCastTasks(uint cacheSize)
            {
                cache = new Task<RayCastHit[]>[cacheSize];
                size = 0;
            }

            public uint Count => size;

            public int MaxSize => cache.Length;

            public void Add(Task<RayCastHit[]> value)
            {
                if (size >= cache.Length)
                {
                    Debug.LogAssertion("Failed to add ray cast task to cache. Size limit has been reached.");
                    return;
                }

                cache[size++] = value;
            }

            public Task<RayCastHit[]> GetCurrent()
            {
                if (current < 0 || current > size)
                {
                    return null;
                }

                return cache[current];
            }

            public Task<RayCastHit[]> Current => GetCurrent();

            object IEnumerator.Current => GetCurrent();

            public void Dispose()
            {
                Clear();
            }

            public bool MoveNext()
            {
                current++;
                return current < size;
            }

            public void Reset()
            {
                current = -1;
            }

            public void Clear()
            {
                Reset();
                size = 0;
            }

            public bool IsCompletedOrEmpty()
            {
                bool completed = true;
                for (uint i = 0; i < size; i++)
                {
                    var task = cache[i];
                    completed &=
                        task.Status == TaskStatus.RanToCompletion ||
                        task.Status == TaskStatus.Faulted ||
                        task.Status == TaskStatus.Canceled;
                }
                return completed;
            }

            public IEnumerator<Task<RayCastHit[]>> GetEnumerator()
            {
                Reset();
                return this;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                Reset();
                return this;
            }
        }

        /// <summary>
        /// A helper class to aid in visualizing remote ray casts
        /// </summary>
        private class RayCastVisualizer : IDisposable
        {
            private IMixedRealityPointer pointer = null;
            private GameObject[] debugRays = null;
            private GameObject debugContainer = null;
            private const float debugRayWidth = 0.001f;
            private uint currentDebugRay = 0;
            public uint maxDebugRays = 0;

            public RayCastVisualizer(IMixedRealityPointer pointer, uint maxDebugRays)
            {
                this.pointer = pointer;
                this.maxDebugRays = maxDebugRays;

                debugContainer = new GameObject();
                debugContainer.name = $"Debug Ray {pointer.PointerName}";
                debugRays = new GameObject[maxDebugRays];
                for (int i = 0; i < maxDebugRays; i++)
                {
                    debugRays[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    debugRays[i].transform.SetParent(debugContainer.transform, false);
                    debugRays[i].transform.localScale = new Vector3(debugRayWidth, debugRayWidth, 0.0f);
                    debugRays[i].SetActive(false);
                }
            }

            public void Dispose()
            {
                if (debugContainer != null)
                {
                    GameObject.Destroy(debugContainer);
                }
                debugContainer = null;
                debugRays = null;
            }

            public void Reset()
            {
                currentDebugRay = 0;
            }

            public void Add(Vector3 position, Vector3 direction, float distance)
            {
                if (debugRays == null || currentDebugRay >= debugRays.Length)
                {
                    return;
                }

                if (currentDebugRay == 0)
                {
                    for (int i = 0; i < maxDebugRays; i++)
                    {
                        debugRays[i].SetActive(false);
                    }
                }

                GameObject ray = debugRays[currentDebugRay++];
                Component.Destroy(ray.GetComponent<Collider>());
                ray.transform.position = position + (direction * distance * 0.5f);
                ray.transform.rotation = UnityEngine.Quaternion.LookRotation(direction);
                ray.transform.localScale = new Vector3(debugRayWidth, debugRayWidth, distance);
                ray.SetActive(true);
            }
        }
        #endregion Private Classes
    }
}
