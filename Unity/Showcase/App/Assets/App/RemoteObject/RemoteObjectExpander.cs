// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.Azure.RemoteRendering.Unity;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Physics;
using Microsoft.MixedReality.Toolkit.Utilities;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>
/// On pointer down events, this behavior will automatically create game objects for the focused ARR entity. This 
/// object creation is referred to as "expansion". During the entity expansion, the "Expanded Prefab" will become the
/// entity game object's parent. Finally, once the "expanded" entity losses focus, its game object and "Expanded
/// Prefab" parent will be destroyed.
///
/// For example:
/// 
/// (Model's ARR Entity Hierarchy Before Focus)       (Model's Game Object Hierarchy Before Focus)
///
///                  Root                                                Root
///                   |
///                  / \
///                 A   B
///                /
///               C
///
/// (Model's ARR Entity Hierarchy During "C" Focus)    (Model's Game Object Hierarchy During "C" Focus)
///
///                  Root                                                Root
///                   |                                                   |
///                  / \                                                 / 
///                 A   B                                               A   
///                /                                                   /
///          C (Focused)                                   C using "Expanded Prefab" (Focused)
///
/// (Model's ARR Entity Hierarchy After Focus)         (Model's Game Object Hierarchy After Focus)
///
///                  Root                                                Root
///                   |
///                  / \
///                 A   B
///                /
///               C
///
/// Note, the "Expanded Prefab" allows for easy declartion of custom MonoBehavior components, such as a MRTK 
/// manipulation component, on child components without having create game objects for all ARR entities.
/// </summary>
public class RemoteObjectExpander : InputSystemGlobalHandlerListener, IMixedRealityPointerHandler, IMixedRealitySourceStateHandler, IMixedRealitySourcePoseHandler
{
    /// <summary>
    /// The set of entity game objects that were created outside of this class.
    /// </summary>
    private HashSet<Entity> _initialEntitiesWithGameObjects = new HashSet<Entity>();

    /// <summary>
    /// This pose change will be forwarded to the expanded object piece, so to ensure that the `ObjectManipulator` functions correctly.
    /// </summary>
    private SourcePoseEventData<MixedRealityPose> _lastPoseChangeEventData;

    /// <summary>
    /// A cache of the pointers' expanded objects.
    /// </summary>
    private Dictionary<IMixedRealityPointer, PointerObjectExpander> _pointerObjects
        = new Dictionary<IMixedRealityPointer, PointerObjectExpander>();

    /// <summary>
    /// A cache of pending expander objects to destroy. Destroying is delayed by one frame to allow
    /// attached scripts respond to lost pointers.
    /// </summary>
    private List<PointerObjectExpander> _pointerObjectsPendingDestroy
        = new List<PointerObjectExpander>();

    #region Serialized Fields
    [Header("Expander Settings")]

    [SerializeField]
    [Tooltip("The prefab to attach to the Remote Entity's game object.")]
    private GameObject expandPrefab = null;

    /// <summary>
    /// The prefab to attach to the Remote Entity's game object.
    /// </summary>
    public GameObject ExpandPrefab
    {
        get => expandPrefab;
        set => expandPrefab = value;
    }

    [Header("Events")]

    [SerializeField]
    [Tooltip("Event fired when enabled.")]
    private UnityEvent onEnabled = new UnityEvent();

    /// <summary>
    /// Event fired when enabled.
    /// </summary>
    public UnityEvent OnEnabled
    {
        get => onEnabled;
        set => onEnabled = value;
    }

    [SerializeField]
    [Tooltip("Event fired when disabled.")]
    private UnityEvent onDisabled = new UnityEvent();

    /// <summary>
    /// Event fired when disabled.
    /// </summary>
    public UnityEvent OnDisabled
    {
        get => onDisabled;
        set => onDisabled = value;
    }
    #endregion Serialized Fields

    #region Public Methods
    /// <summary>
    /// Was the given game object created by this expander. 
    /// </summary>
    public bool Expanded(Entity entity)
    {
        if (entity == null || !entity.Valid)
        {
            return false;
        }

        return !_initialEntitiesWithGameObjects.Contains(entity);
    }
    #endregion Public Methods

    #region MonoBehavior Methods
    /// <summary>
    /// Call update on each of the expanded pointer objects.
    /// </summary>
    private void LateUpdate()
    {
        foreach (var entry in _pointerObjects)
        {
            entry.Value.LateUpdate();
        }

        foreach (var entry in _pointerObjectsPendingDestroy)
        {
            entry.Dispose();
        }
        _pointerObjectsPendingDestroy.Clear();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        onEnabled?.Invoke();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        onDisabled?.Invoke();
    }
    #endregion MonoBehavior Methods

    #region IMixedRealityPointerHandler
    public void OnPointerClicked(MixedRealityPointerEventData eventData)
    {
    }

    public void OnPointerDown(MixedRealityPointerEventData eventData)
    {
        FocusDetails focusDetails;
        if (CoreServices.InputSystem.FocusProvider.TryGetFocusDetails(eventData.Pointer, out focusDetails) &&
            focusDetails.Object != null &&
            focusDetails.Object.transform != null &&
            focusDetails.Object.transform.IsChildOf(transform)) 
        {
            GetOrCreateExpander(eventData.Pointer).Initialize(_lastPoseChangeEventData, eventData);
        }
    }

    public void OnPointerDragged(MixedRealityPointerEventData eventData)
    {
    }

    public void OnPointerUp(MixedRealityPointerEventData eventData)
    {
    }
    #endregion IMixedRealityPointerHandler

    #region IMixedRealitySourcePoseHandler
    /// <summary>
    /// Raised when the source pose tracking state is changed.
    /// </summary>
    public void OnSourcePoseChanged(SourcePoseEventData<TrackingState> eventData) { }

    /// <summary>
    /// Raised when the source position is changed.
    /// </summary>
    public void OnSourcePoseChanged(SourcePoseEventData<Vector2> eventData) { }

    /// <summary>
    /// Raised when the source position is changed.
    /// </summary>
    public void OnSourcePoseChanged(SourcePoseEventData<Vector3> eventData) { }

    /// <summary>
    /// Raised when the source rotation is changed.
    /// </summary>
    public void OnSourcePoseChanged(SourcePoseEventData<UnityEngine.Quaternion> eventData) { }

    /// <summary>
    /// Raised when the source pose is changed.
    /// </summary>
    public void OnSourcePoseChanged(SourcePoseEventData<MixedRealityPose> eventData)
    {
        _lastPoseChangeEventData = eventData;
    }
    #endregion IMixedRealitySourcePoseHandler

    #region IMixedRealitySourceStateHandler
    public void OnSourceDetected(SourceStateEventData eventData)
    {
    }

    /// <summary>
    /// When an input source is lost, dispose the pointers' object expanders.
    /// </summary>
    public void OnSourceLost(SourceStateEventData eventData)
    {
        foreach (var pointer in eventData.InputSource.Pointers)
        {
            OnPointerLost(pointer);
        }
    }
    #endregion IMixedRealitySourceStateHandler

    #region InputSystemGlobalHandlerListener
    /// <summary>
    /// Register to global input source changes.
    /// </summary>
    protected override void RegisterHandlers()
    {
        CoreServices.InputSystem?.RegisterHandler<IMixedRealityPointerHandler>(this);
        CoreServices.InputSystem?.RegisterHandler<IMixedRealitySourceStateHandler>(this);
    }

    /// <summary>
    /// Unregister from global input source changes.
    /// </summary>
    protected override void UnregisterHandlers()
    {
        CoreServices.InputSystem?.UnregisterHandler<IMixedRealityPointerHandler>(this);
        CoreServices.InputSystem?.UnregisterHandler<IMixedRealitySourceStateHandler>(this);
    }
    #endregion InputSystemGlobalHandlerListener

    #region Private Methods
    private void InitializeStartingEntitiesOnce()
    {
        if (_initialEntitiesWithGameObjects.Count > 0)
        {
            return;
        }

        var syncObjects = GetComponentsInChildren<RemoteEntitySyncObject>();
        foreach (var syncObject in syncObjects)
        {
            if (syncObject.Entity != null)
            {
                _initialEntitiesWithGameObjects.Add(syncObject.Entity);
            }
        }
    }

    /// <summary>
    /// Get the object expander for the given pointer.
    /// </summary>
    private PointerObjectExpander GetOrCreateExpander(IMixedRealityPointer pointer)
    {
        if (pointer == null)
        {
            return null;
        }

        this.InitializeStartingEntitiesOnce();

        PointerObjectExpander result = null;
        if (!_pointerObjects.TryGetValue(pointer, out result))
        {
            _pointerObjects[pointer] = result = new PointerObjectExpander(this)
            {
                ExpandedPrefab = expandPrefab
            };
        }
        return result;
    }

    /// <summary>
    /// The given pointer was lost.
    /// </summary>
    private void OnPointerLost(IMixedRealityPointer pointer)
    {
        if (pointer == null)
        {
            return;
        }

        PointerObjectExpander result;
        if (_pointerObjects.TryGetValue(pointer, out result))
        {
            _pointerObjects.Remove(pointer);
            _pointerObjectsPendingDestroy.Add(result);
        }
    }
    #endregion Private Methods

    #region Private Classes
    /// <summary>
    /// A class that will expand a pointer's remote target to a game object.
    /// </summary>
    private class PointerObjectExpander
    {
        private ProxyObject _proxyObject = null;
        private IRemotePointerResult _remoteFocusData = null;

        /// <summary>
        /// Create an new object expander for a pointer.
        /// </summary>
        public PointerObjectExpander(RemoteObjectExpander owner)
        {
            Owner = owner;
        }

        /// <summary>
        /// The owner of the object expander
        /// </summary>
        public RemoteObjectExpander Owner { get; }

        /// <summary>
        /// The prefab to attach to the Remote Entity's game object.
        /// </summary>
        public GameObject ExpandedPrefab { get; set; }

        /// <summary>
        /// Is the a pointer attached to this proxy object.
        /// </summary>
        public bool Active { get => _remoteFocusData != null; }

        /// <summary>
        /// Release all dependencies of this object, and destroy the current proxy object.
        /// </summary>
        public void Dispose()
        {
            _remoteFocusData = null;
            DestroyProxyObject();
        }

        /// <summary>
        /// Update the proxy object
        /// </summary>
        public void LateUpdate()
        {
            // When lost focus, destroy proxy object
            if (!IsParentFocused())
            {
                DestroyProxyObject();
            }
        }

        /// <summary>
        /// Initialize, expand the pointer remote target to a game object. Then forward the given events to the new object.
        /// </summary>
        public void Initialize(SourcePoseEventData<MixedRealityPose> poseEventData, MixedRealityPointerEventData pointerEventData)
        {
            UpdateProxyObject(pointerEventData.Pointer);

            // Forwarding the event is done later in the frame by DelayPointerDown component on the proxy object
            if (_proxyObject != null)
            {
                var eventProxy = _proxyObject.EnsureComponent<DelayedEventProxy>();
                eventProxy.Fire(poseEventData);
                eventProxy.Fire(pointerEventData);
            }
        }

        /// <summary>
        /// Get or create a proxy object for the given Remote Entity.
        /// </summary>
        private ProxyObject GetOrCreateProxyObject(Entity entity, GameObject proxyPrefab)
        {
            if (entity == null || !entity.Valid) 
            {
                return null;
            }

            ProxyObject proxyObject = GetProxyObject(entity);
            if (proxyObject == null)
            {
                GameObject proxyGameObject;
                if (proxyPrefab == null)
                {
                    proxyGameObject = new GameObject();
                    // avoid scripts from running when adding components
                    proxyGameObject.SetActive(false);
                }
                else
                {
                    // avoid scripts from running right when instantiated and when adding components
                    proxyPrefab.SetActive(false);
                    proxyGameObject = Instantiate(proxyPrefab);
                }

                // Attach proxy after binding to sync object
                proxyObject = proxyGameObject.EnsureComponent<ProxyObject>();
                proxyObject.Owner = Owner;
                proxyObject.Attach(entity);

                // Enable after configuring proxy
                proxyObject.gameObject.SetActive(true);
            }
            return proxyObject;
        }

        /// <summary>
        /// Get a proxy object for the given Remote Entity.
        /// </summary>
        private ProxyObject GetProxyObject(Entity entity)
        {
            if (entity == null || !entity.Valid)
            {
                return null;
            }

            ProxyObject proxyObject = null;
            GameObject gameObject = entity.GetExistingGameObject();
            if (gameObject != null)
            {
                proxyObject = gameObject.GetComponent<ProxyObject>();
            }

            if (proxyObject != null)
            {
                proxyObject.Attach(entity);
            }

            return proxyObject;
        }

        /// <summary>
        /// Destroy the given proxy object if nothing else is using it.
        /// </summary>
        private void DestroyProxyObject()
        {
            if (_proxyObject == null)
            {
                return;
            }

            _proxyObject.Release();
            _proxyObject = null;
        }

        /// <summary>
        /// Attempt to find or create a proxy object for the remote entity focused by the event data's pointer.
        /// </summary>
        private void UpdateProxyObject(IMixedRealityPointer pointer, bool allowCreate = true)
        {
            IRemoteFocusProvider remoteFocusProvider = AppServices.RemoteFocusProvider;
            IRemotePointerResult remoteResult = remoteFocusProvider?.GetRemoteResult(pointer);
            if (remoteResult == null || !remoteResult.IsTargetValid)
            {
                _remoteFocusData = null;
                DestroyProxyObject();
                return;
            }

            // Get or create a new proxy object
            if (_proxyObject == null ||
                _proxyObject.Entity != remoteResult.TargetEntity)
            {
                DestroyProxyObject();
                _proxyObject = allowCreate ?
                    GetOrCreateProxyObject(remoteResult.TargetEntity, ExpandedPrefab) :
                    GetProxyObject(remoteResult.TargetEntity);
            }

            // Save focusing object. This will be used to determine when the proxy object has lost focus
            _remoteFocusData = remoteResult;

            // If a proxy object was created, change focus this object
            if (_proxyObject != null)
            {
                remoteFocusProvider.TryFocusingChild(remoteResult.Pointer, _proxyObject.RemoteSyncObject.gameObject);
            }
        }

        /// <summary>
        /// Is the current object still focused.
        /// </summary>
        private bool IsParentFocused()
        {
            Entity entity = _proxyObject?.RemoteSyncObject?.Entity;
            if (entity == null || !entity.Valid)
            {
                return false;
            }

            Entity focused = _remoteFocusData?.TargetEntity;
            if (focused == null || !focused.Valid)
            {
                return false;
            }

            return entity.IsChildOf(focused);
        }
    }

    /// <summary>
    /// This manages the life time of the entity's game object that was created during "expansion".
    /// </summary>
    private class ProxyObject : MonoBehaviour
    {
        /// <summary>
        /// The expander owner
        /// </summary>
        public RemoteObjectExpander Owner { get; set; }

        /// <summary>
        /// How many pointers are referencing this
        /// </summary>
        public int RefCount { get; private set; }

        /// <summary>
        /// Get the entity
        /// </summary>
        public Entity Entity { get; private set; }

        /// <summary>
        /// The remote object this is controlling
        /// </summary>
        public RemoteEntitySyncObject RemoteSyncObject { get; private set; }

        private void Update()
        {
            if (RemoteSyncObject != null)
            {
                RemoteSyncObject.SyncToRemote();
            }

            if (RefCount == 0)
            {
                DestroySelf();
            }
        }

        private void OnDestroy()
        {
            DestroyEmptyParents();
        }

        /// <summary>
        /// Attach an entity to this object
        /// </summary>
        /// <param name="entity"></param>
        public void Attach(Entity entity)
        {
            if (Entity != null && Entity != entity)
            {
                Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  "Attempting to attach a new entity to proxy that already has an entity");
                return;
            }

            RefCount++;

            if (Entity == null && entity.Valid)
            {
                Entity = entity;

                // Copy name
                name = entity.Name;

                // Attach proxy to the entity's parent
                if (entity.Parent != null && entity.Parent.Valid)
                {
                    transform.SetParent(entity.Parent.GetOrCreateGameObject(UnityCreationMode.DoNotCreateUnityComponents).transform);
                }

                // Move this to the entity's position
                transform.localPosition = entity.Position.toUnityPos();
                transform.localRotation = entity.Rotation.toUnity();
                transform.localScale = entity.Scale.toUnity();

                // Now destroy the old entity's game object
                GameObject existingObject = entity.GetExistingGameObject();
                if (existingObject != null)
                {
                    existingObject.GetComponent<RemoteEntitySyncObject>().Unbind();
                    Destroy(existingObject);
                }

                // Bind this game object to the entity
                entity.BindToUnityGameObject(gameObject);
                RemoteSyncObject = GetComponent<RemoteEntitySyncObject>();
            }
        }

        /// <summary>
        /// Release the reference to the proxy object, and return remaining ref count
        /// </summary>
        public int Release()
        {
            RefCount--;
            if (RefCount < 0)
            {
                Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  "Proxy object released too many times.");
                RefCount = 0;
            }
            return RefCount;
        }

        /// <summary>
        /// Start to destroy this ProxyObject component and its game object,
        /// without destroying the remote Entity.
        /// </summary>
        private void DestroySelf()
        {
            // Unbind this game bind from the entity. We don't want deleting this local object to
            // delete the remote object.
            if (RemoteSyncObject != null)
            {
                RemoteSyncObject.Unbind();
                RemoteSyncObject = null;
            }

            // Fix-up children's parent to point to a new "default" game object, since we're destroying 
            // this game object. Note, the children's old parent (this game object) is being destroyed
            // because this game object can have numerous other components we no longer need or want.
            // For example, the ProxyObject's game object could also have a ManipulationHandler
            // attached.
            int children = transform.childCount;
            if (Entity != null && Entity.Valid && children > 0)
            {
                Transform newParent = Entity.GetOrCreateGameObject(UnityCreationMode.DoNotCreateUnityComponents).transform;
                for (int i = children - 1; i > 0; i--)
                {
                    transform.GetChild(i).SetParent(newParent, true);
                }
            }

            // Don't delete remote object just yet, OnDestroy will handle this.
            Destroy(gameObject);
        }

        /// <summary>
        /// Destroy an parent objects that no longer have any "active" children.
        /// </summary>
        private void DestroyEmptyParents()
        {
            if (Entity == null || !Entity.Valid) 
            {
                return; 
            }

            // Find the topmost game object that can be deleted. This is a parent game object
            // with a valid entity, was expanded by the "owner", has only one local child (which
            // is in the process of being deleted), and is not being used as expanded ProxyObject.
            Entity currentEntity = Entity;
            GameObject parentEntityGameObject;
            GameObject deleteThis = null;
            while (currentEntity.Parent != null &&
                currentEntity.Parent.Valid &&
                Owner.Expanded(currentEntity.Parent) &&
                TryGetExistingGameObject(currentEntity.Parent, out parentEntityGameObject) &&
                parentEntityGameObject.transform.childCount <= 1 &&
                GetProxyObjectRefCount(parentEntityGameObject) <= 0)
            {
                currentEntity = currentEntity.Parent;
                deleteThis = parentEntityGameObject;
            }

            // Now delete the local game object, without deleting the remote entity
            if (deleteThis != null)
            {
                deleteThis.GetComponent<RemoteEntitySyncObject>()?.Unbind();
                Destroy(deleteThis);
            }
        }

        /// <summary>
        /// Try to get an existing ARR local game object.
        /// </summary>
        private static bool TryGetExistingGameObject(Entity entity, out GameObject entityGameObject)
        {
            entityGameObject = entity.GetExistingGameObject();
            return entityGameObject != null;
        }

        /// <summary>
        /// Check if the given game object is also a proxy object. If its return its ref count.
        /// </summary>
        private static int GetProxyObjectRefCount(GameObject entityGameObject)
        {
            var proxyObject = entityGameObject?.GetComponent<ProxyObject>();
            return (proxyObject != null) ? proxyObject.RefCount : 0;
        }
    }

    /// <summary>
    /// Handle forwarding events to proxy object late in the frame. This is done because during `OnPointerDown` and 
    /// `OnSourcePoseChanged` the proxy object's `ObjectManipular::Start()` hasn't been called in all cases.
    /// So we wait to forward events to prevent jumpy behavior of the moved pieces, and to ensure rotation works.
    /// </summary>
    private class DelayedEventProxy : MonoBehaviour
    {
        private MixedRealityPointerEventData _pendingPointerDown = null;
        private SourcePoseEventData<MixedRealityPose> _pendingSourcePoseChanged = null;
        private bool _started = false;

        public void Start()
        {
            _started = true;

            if (_pendingSourcePoseChanged != null)
            {
                Fire(_pendingSourcePoseChanged);
                _pendingSourcePoseChanged = null;
            }

            if (_pendingPointerDown != null)
            {
                Fire(_pendingPointerDown);
                _pendingPointerDown = null;
            }
        }

        public void Fire(SourcePoseEventData<MixedRealityPose> sourcePoseChangedEventData)
        {
            if (_started && sourcePoseChangedEventData != null)
            {
                RouteEventToCurrentObject(sourcePoseChangedEventData, OnSourcePoseChangedEventHandler);
                _pendingSourcePoseChanged = null;
            }
            else
            {
                _pendingSourcePoseChanged = sourcePoseChangedEventData;
            }
        }

        public void Fire(MixedRealityPointerEventData pointerDownEventData)
        {
            if (_started && pointerDownEventData != null)
            {
                RouteEventToCurrentObject(pointerDownEventData, OnPointerDownEventHandler);
                _pendingPointerDown = null;
            }
            else
            {
                _pendingPointerDown = pointerDownEventData;
            }
        }

        private static readonly ExecuteEvents.EventFunction<IMixedRealitySourcePoseHandler> OnSourcePoseChangedEventHandler =
            delegate (IMixedRealitySourcePoseHandler handler, BaseEventData eventData)
            {
                var casted = ExecuteEvents.ValidateEventData<SourcePoseEventData<MixedRealityPose>>(eventData);
                handler.OnSourcePoseChanged(casted);
            };

        private static readonly ExecuteEvents.EventFunction<IMixedRealityPointerHandler> OnPointerDownEventHandler =
            delegate (IMixedRealityPointerHandler handler, BaseEventData eventData)
            {
                if (eventData != null)
                {
                    var casted = ExecuteEvents.ValidateEventData<MixedRealityPointerEventData>(eventData);
                    handler.OnPointerDown(casted);
                }
            };

        private void RouteEventToCurrentObject<T>(BaseEventData eventData, ExecuteEvents.EventFunction<T> eventFunction) where T : IEventSystemHandler
        {
            ExecuteEvents.Execute(gameObject, eventData, eventFunction);
        }
    }
    #endregion Private Classes
}
