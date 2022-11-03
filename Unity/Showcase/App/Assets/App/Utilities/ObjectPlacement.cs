// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Physics;
using Microsoft.MixedReality.Toolkit.Utilities;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// This behavior helps place an object via the MRTK's primary pointer.
/// </summary>
public class ObjectPlacement : InputSystemGlobalHandlerListener, IMixedRealityPointerHandler
{
    private ObjectPlacementHit _placementHit;
    private IMixedRealityFocusProvider _focusProvider;
    private IMixedRealityPointer _pointerOverride;
    private IMixedRealityPointer _lastUsedPointer;
    private TaskCompletionSource<bool> _placementFinished;
    private IPointerStateVisibilityOverride[] _currentVisibilityOverrides;
    private LogHelper<ObjectPlacement> _log = new LogHelper<ObjectPlacement>();
    private ObjectPlacementSnapPoint _snapPoint = null;

    #region Serialized Fields
    [Header("General Settings")]

    [SerializeField]
    [Tooltip("The target to be moved. This will default to this transform if null.")]
    private Transform target;

    /// <summary>
    /// The target to be moved. This will default to this transform if null.
    /// </summary>
    public Transform Target
    {
        get => target;
        set => target = value;
    }

    [SerializeField]
    [Tooltip("The placeholder to move and show while placing object.")]
    private Transform placeholder;

    /// <summary>
    /// The placeholder to move and show while placing object.
    /// </summary>
    public Transform Placeholder
    {
        get => placeholder;
        set => placeholder = value;
    }

    [SerializeField]
    [Tooltip("Should object and/or placement face towards user while in placement mode.")]
    public bool faceTowards = false;

    /// <summary>
    /// Should object and/or placement face towards user while in placement mode.
    /// </summary>
    public bool FaceTowards
    {
        get => faceTowards;
        set => faceTowards = value;
    }

    [SerializeField]
    [Tooltip("Should the spatial mesh be rendered when in placement mode.")]
    private bool showSpatialMesh = false;

    /// <summary>
    /// Should the spatial mesh be rendered when in placement mode.
    /// </summary>
    public bool ShowSpatialMesh
    {
        get => showSpatialMesh;
        set => showSpatialMesh = value;
    }
    [SerializeField]
    [Tooltip("The physic layer mask for  the spatial mesh. This is used along side with 'Show Spatial Mesh'.")]
    private LayerMask meshPhysicsLayerMask = 0;

    /// <summary>
    /// The physic layer mask for  the spatial mesh. This is used along side with 'Show Spatial Mesh'.
    /// </summary>
    public LayerMask MeshPhysicsLayerMask
    {
        get => meshPhysicsLayerMask;
        set => meshPhysicsLayerMask = value;
    }

    [SerializeField]
    [Tooltip("The distance of placement if there is no hit target. If zero or less, this is ignored.")]
    private float defaultDistance = 0.0f;

    /// <summary>
    /// The distance of placement if there is no hit target. If zero or less, this is ignored."
    /// </summary>
    public float DefaultDistance
    {
        get => defaultDistance;
        set => defaultDistance = value;
    }

    [SerializeField]
    [Tooltip("The max distance of placement if there is no hit target. If zero or less, this is ignored.")]
    private float maxDistance = 0.0f;

    /// <summary>
    /// The distance of placement if there is no hit target. If zero or less, this is ignored."
    /// </summary>
    public float MaxDistance
    {
        get => maxDistance;
        set => maxDistance = value;
    }

    [Header("Initial Position")]

    [SerializeField]
    [Tooltip("The distance the object should be placed from the main camera. If zero or less, this field is ignored.")]
    private float distanceFromHead = 0;

    /// <summary>
    /// The distance the object should be placed from the main camera. If zero or less, this field is ignored.
    /// </summary>
    public float DistanceFromHead
    {
        get => distanceFromHead;
        set => distanceFromHead = value;
    }

    [SerializeField]
    [Tooltip("The direction the object should be placed from the main camera. This field is used along side 'Distance From Head'.")]
    private Vector3 directionFromHead = Vector3.forward;

    /// <summary>
    /// The direction the object should be placed from the main camera. This field is used along side 'Distance From Head'.
    /// </summary>
    public Vector3 DirectionFromHead
    {
        get => directionFromHead;
        set => directionFromHead = value;
    }

    [Header("Smoothing Settings")]

    [SerializeField]
    [Tooltip("If 0, the position will update immediately.  Otherwise, the greater this attribute the slower the position updates")]
    private float moveLerpTime = 0.1f;

    /// <summary>
    /// If 0, the position will update immediately.  Otherwise, the greater this attribute the slower the position updates
    /// </summary>
    public float MoveLerpTime
    {
        get => moveLerpTime;
        set => moveLerpTime = value;
    }

    [SerializeField]
    [Tooltip("If 0, the rotation will update immediately.  Otherwise, the greater this attribute the slower the rotation updates")]
    private float rotateLerpTime = 0.1f;

    /// <summary>
    /// If 0, the rotation will update immediately.  Otherwise, the greater this attribute the slower the rotation updates")]
    /// </summary>
    public float RotateLerpTime
    {
        get => rotateLerpTime;
        set => rotateLerpTime = value;
    }

    [SerializeField]
    [Tooltip("If true, updates are smoothed to the target. Otherwise, they are snapped to the target")]
    private bool enableSmoothing = true;

    /// <summary>
    /// If true, updates are smoothed to the target. Otherwise, they are snapped to the target
    /// </summary>
    public bool EnableSmoothing
    {
        get => enableSmoothing;
        set => enableSmoothing = value;
    }

    [Header("Events")]

    [SerializeField]
    [Tooltip("Event raised when object placing has started.")]
    private UnityEvent onPlacing = new UnityEvent();

    /// <summary>
    /// Event raised when object placing has started.
    /// </summary>
    public UnityEvent OnPlacing
    {
        get => onPlacing;
        set => onPlacing = value;
    }

    [SerializeField]
    [Tooltip("Event raised once object is placed.")]
    private UnityEvent onPlaced = new UnityEvent();

    /// <summary>
    /// Event raised once object is placed.
    /// </summary>
    public UnityEvent OnPlaced
    {
        get => onPlaced;
        set => onPlaced = value;
    }
    #endregion Serialized Fields

    #region Public Properties
    /// <summary>
    /// Get if system is currently in placement
    /// </summary>
    public bool InPlacement { get; private set; }

    /// <summary>
    /// The last snap point being used. If null, then no snap point is used.
    /// </summary>
    public ObjectPlacementSnapPoint SnapPoint
    {
        get => _snapPoint;

        private set
        {
            if (_snapPoint != value)
            {
                _snapPoint?.Unsnap();
                _snapPoint = value;
                _snapPoint?.Snap();
            }
        }
    }
    #endregion Public Properties

    #region Public Methods
    /// <summary>
    /// Start the placement process.
    /// </summary>
    public Task StartPlacement()
    {
        _log.LogVerbose("StartPlacement() Entered ({0})", name);

        SetSpatialMeshVisibility(true);
        StopOtherPlacements();
        SetInitialPosition();

        if (_placementFinished == null)
        {
            _placementFinished = new TaskCompletionSource<bool>();
        }

        DisposePointerVisibilityOverrides();
        _currentVisibilityOverrides = new IPointerStateVisibilityOverride[]
        {
            AppServices.PointerStateService.ShowPointer(PointerType.HandRay),
            AppServices.PointerStateService.HidePointer(PointerType.HandGrab),
            AppServices.PointerStateService.HidePointer(PointerType.HandPoke)
        };

        bool wasInPlacement = InPlacement;

        _placementHit = new ObjectPlacementHit() { pose = new Pose(target.position, target.rotation) };
        InPlacement = true;
        RegisterHandlers();
        UpdatePlacementVisualActiveState();

        if (!wasInPlacement)
        {
            onPlacing?.Invoke();
        }

        _log.LogVerbose("StartPlacement() Exitting ({0})", name);
        return _placementFinished.Task;
    }

    /// <summary>
    /// Stop the placement process.
    /// </summary>
    public void StopPlacement()
    {
        _log.LogVerbose("StopPlacement() Entered ({0})", name);

        DisposePointerVisibilityOverrides();
        UnregisterHandlers();
        SetSpatialMeshVisibility(false);

        bool wasInPlacement = InPlacement;

        InPlacement = false;
        _lastUsedPointer = null;
        UpdatePlacementVisualActiveState();

        if (wasInPlacement)
        {
            SnapPoint?.Select();
        }

        if (_placementFinished != null)
        {
            _placementFinished.TrySetResult(true);
            _placementFinished = null;
        }

        if (wasInPlacement)
        {
            onPlaced?.Invoke();
        }

        SnapPoint = null;
        _log.LogVerbose("StopPlacement() Exitting ({0})", name);
    }
    #endregion Public Methods

    #region MonoBehavior Methods
    protected override void Start()
    {
        base.Start();
        _focusProvider = CoreServices.InputSystem?.FocusProvider;

        
        UpdatePlacementVisualActiveState();
    }

    protected override void OnEnable()
    {
        if (target == null)
        {
            target = transform;
        }

        base.OnEnable();
    }

    private void Update()
    {
        if (!InPlacement)
        {
            return;
        }

        // Update placement pose
        UpdatePoseViaPointer();

        // Smooth transform to the goal
        UpdateTransformToGoal(target);

        // Only move placeholder, if it wasn't already moved
        if (placeholder != null && !placeholder.IsChildOf(target))
        {
            UpdateTransformToGoal(placeholder);
        }
    }

    protected override void OnDisable()
    {
        if (InPlacement)
        {
            StopPlacement();
        }
    }
    #endregion MonoBehavior Methods

    #region IMixedRealityPointerHandler
    public void OnPointerClicked(MixedRealityPointerEventData eventData)
    {
        if (InPlacement)
        {
            StopPlacement();
        }

        _pointerOverride = null;
    }

    public void OnPointerDown(MixedRealityPointerEventData eventData)
    {
        // Prevent picking up a new primary pointer during a click
        _pointerOverride = _lastUsedPointer;
    }

    public void OnPointerDragged(MixedRealityPointerEventData eventData)
    {
    }

    public void OnPointerUp(MixedRealityPointerEventData eventData)
    {
    }
    #endregion IMixedRealityPointerHandler

    #region InputSystemGlobalHandlerListener
    /// <summary>
    /// Register to global input source changes.
    /// </summary>
    protected override void RegisterHandlers()
    {
        if (InPlacement)
        {
            CoreServices.InputSystem?.RegisterHandler<IMixedRealityPointerHandler>(this);
        }
    }

    /// <summary>
    /// Unregister from global input source changes.
    /// </summary>
    protected override void UnregisterHandlers()
    {
        if (InPlacement)
        {
            CoreServices.InputSystem?.UnregisterHandler<IMixedRealityPointerHandler>(this);
        }
    }
    #endregion InputSystemGlobalHandlerListener

    #region Private Methods    
    /// <summary>
    /// Update the pose via the current pointer.
    /// </summary>
    private void UpdatePoseViaPointer()
    {
        IMixedRealityPointer usePointer = _pointerOverride ?? _focusProvider?.PrimaryPointer;
        bool? isVisible = usePointer?.BaseCursor?.IsVisible;
        if (usePointer == null || usePointer.BaseCursor == null || (!isVisible ?? true))
        {
            return;
        }

        // Save last used pointer
        _lastUsedPointer = usePointer;

        // Use the pointer's last hit target. If there's no target fallback to a default distance
        TryFindPlacementPose(_lastUsedPointer, ref _placementHit);
        SnapPoint = TryFindPlacementSnapPoint(ref _placementHit);
    }

    /// <summary>
    /// Cancel this object's visibility override requests
    /// </summary>
    private void DisposePointerVisibilityOverrides()
    {
        _log.LogVerbose("DisposePointerVisibilityOverrides() Entered ({0})", name);

        if (_currentVisibilityOverrides != null)
        {
            foreach (IPointerStateVisibilityOverride visibilityOverride in _currentVisibilityOverrides)
            {
                visibilityOverride.Dispose();
            }
            _currentVisibilityOverrides = null;
        }

        _log.LogVerbose("DisposePointerVisibilityOverrides() Exitting ({0})", name);
    }

    /// <summary>
    /// Stop all other placement operations
    /// </summary>
    private void StopOtherPlacements()
    {
        _log.LogVerbose("StopOtherPlacements() Entered ({0})", name);

        var allPlacements = Resources.FindObjectsOfTypeAll<ObjectPlacement>();
        if (allPlacements != null)
        {
            foreach (var placement in allPlacements)
            {
                if (placement.InPlacement && placement != this)
                {
                    placement.StopPlacement();
                }
            }
        }

        _log.LogVerbose("StopOtherPlacements() Exitting ({0})", name);
    }

    /// <summary>
    /// Set the initial position the transform
    /// </summary>
    private void SetInitialPosition()
    {
        if (distanceFromHead <= 0 || CameraCache.Main == null)
        {
            return;
        }

        _log.LogVerbose("SetInitialPosition() Entered ({0})", name);
        var camera = CameraCache.Main.transform;
        if (camera != null)
        {
            var direction = camera.TransformDirection(directionFromHead.normalized).normalized;
            target.position = camera.position + (direction * distanceFromHead);
        }
        _log.LogVerbose("SetInitialPosition() Exitting ({0})", name);
    }

    /// <summary>
    /// Find the best placement pose given a pointer object. This calculates the pose based on the pointer's
    /// first intersection that is not part of this game object's hierarchy. If there is no such intersection, the
    /// returned pose is the 'default distance' from the pointer's start pose.
    /// </summary>
    private bool TryFindPlacementPose(IMixedRealityPointer pointer, ref ObjectPlacementHit hit)
    {
        hit.valid = false;
        hit.transform = null;

        if (pointer == null ||
            pointer.Rays == null ||
            pointer.Rays.Length == 0)
        {
            _log.LogVerbose("TryFindPlacementPosition() Failed. No Pointer ({0})", name);
            return hit.valid;
        }

        // Perform a new raycast, using the component defined mask.
        RayStep pointerRay = pointer.Rays[0];
        var hits = Physics.RaycastAll(pointerRay, this.maxDistance, MeshPhysicsLayerMask.value);
        int hitsCount = hits?.Length ?? 0;
        float distance = float.MaxValue;
        for (int i = 0; i < hitsCount; i++)
        {
            var currentHit = hits[i];
            if (currentHit.collider != null && 
                ValidHitTarget(currentHit.collider.transform) &&
                currentHit.distance < distance)
            {
                distance = currentHit.distance;
                hit.pose.position = currentHit.point;
                hit.transform = currentHit.collider.transform;
                hit.valid = true;
            }
        }

        // Fallback to the default distance, if nothing can be focused.
        if (!hit.valid)
        {
            hit.pose.position = pointerRay.Origin + (pointerRay.Direction * this.defaultDistance);
            hit.valid = true;
        }

        // Cap the distance of the object at this max distance.
        if ((hit.valid) &&
            (this.maxDistance > 0) &&
            (hit.pose.position - pointerRay.Origin).sqrMagnitude > (this.maxDistance * this.maxDistance))
        {
            hit.pose.position = pointerRay.Origin + (pointerRay.Direction * this.maxDistance);
        }

        // If requested, rotate object towards the main camera
        if (faceTowards)
        {
            Vector3 mainCameraPosition = CameraCache.Main.transform.position;
            Vector3 mainCameraToObject = hit.pose.position - mainCameraPosition;
            var direction = new Vector3(mainCameraToObject.x, 0, mainCameraToObject.z);
            hit.pose.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
        else
        {
            hit.pose.rotation = target.rotation;
        }

        return hit.valid;
    }

    private ObjectPlacementSnapPoint TryFindPlacementSnapPoint(ref ObjectPlacementHit hit)
    {
        ObjectPlacementSnapPoint snapPoint = null;
        if (hit.transform != null)
        {
            snapPoint = hit.transform.GetComponentInParent<ObjectPlacementSnapPoint>();
        }

        if (snapPoint != null)
        {
            hit.pose.position = snapPoint.transform.position;
            hit.pose.rotation = snapPoint.transform.rotation;
        }

        return snapPoint;
    }

    private bool ValidHitTarget(Transform hitTarget)
    {
        return (hitTarget.GetComponentInParent<ObjectPlacementSnapPoint>() != null) ||
            (!hitTarget.IsChildOf(target));
    }

    /// <summary>
    /// Updates all object orientations to the goal orientation for this solver, with smoothing accounted for (smoothing may be off)
    /// </summary>
    private void UpdateTransformToGoal(Transform target)
    {
        if (enableSmoothing)
        {
            target.position = SmoothTo(target.position, _placementHit.pose.position, Time.deltaTime, moveLerpTime);
            target.rotation = SmoothTo(target.rotation, _placementHit.pose.rotation, Time.deltaTime, rotateLerpTime);
        }
        else
        {
            target.position = _placementHit.pose.position;
            target.rotation = _placementHit.pose.rotation;
        }
    }

    /// <summary>
    /// Lerps Vector3 source to goal.
    /// </summary>
    /// <remarks>
    /// Handles lerpTime of 0.
    /// </remarks>
    private static Vector3 SmoothTo(Vector3 source, Vector3 goal, float deltaTime, float lerpTime)
    {
        return Vector3.Lerp(source, goal, lerpTime.Equals(0.0f) ? 1f : deltaTime / lerpTime);
    }

    /// <summary>
    /// Slerps Quaternion source to goal, handles lerpTime of 0
    /// </summary>
    private static Quaternion SmoothTo(Quaternion source, Quaternion goal, float deltaTime, float lerpTime)
    {
        return Quaternion.Slerp(source, goal, lerpTime.Equals(0.0f) ? 1f : deltaTime / lerpTime);
    }

    /// <summary>
    /// Show or hide placement visual as needed.
    /// </summary>
    private void UpdatePlacementVisualActiveState()
    {
        if (placeholder != null && 
            placeholder.gameObject != null)
        {
            placeholder.gameObject.SetActive(InPlacement);
        }
    }

    /// <summary>
    /// Show or hide the spatial mesh.
    /// </summary> 
    private void SetSpatialMeshVisibility(bool visible)
    {
        // Ignore visible requests if behavior is not setup to show the mesh.
        if (!showSpatialMesh)
        {
            return;
        }

        SpatialMeshObserverHelper.SetState(new SpatialMeshObserverHelperState()
        {
            active = visible,
            visible = visible,
            ignoreRaycasts = !visible
        });
    }
    #endregion Private Methods

    #region Private Struct
    private struct ObjectPlacementHit
    {
        public Pose pose;
        public Transform transform;
        public bool valid;
    }

    #endregion Private Struct
}
