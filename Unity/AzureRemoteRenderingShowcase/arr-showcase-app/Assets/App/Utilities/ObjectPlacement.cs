// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.Physics;
using Microsoft.MixedReality.Toolkit.WindowsMixedReality.SpatialAwareness;
using Microsoft.MixedReality.Toolkit.Utilities;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using Microsoft.MixedReality.Toolkit.SpatialAwareness;

/// <summary>
/// This behavior helps place an object via the MRTK's primary pointer.
/// </summary>
public class ObjectPlacement : InputSystemGlobalHandlerListener, IMixedRealityPointerHandler
{
    private Vector3 _placementPosition;
    private Quaternion _placementRotation;
    private IMixedRealityFocusProvider _focusProvider;
    private IMixedRealityPointer _pointerOverride;
    private IMixedRealityPointer _lastUsedPointer;
    private TaskCompletionSource<bool> _placementFinished;
    private IPointerStateVisibilityOverride[] _currentVisibilityOverrides;
    private const float _maxSnapDistance = 1000.0f;

    #region Serialized Fields
    [Header("General Settings")]

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
    #endregion Public Properties

    #region Public Methods
    /// <summary>
    /// Start the placement process.
    /// </summary>
    public Task StartPlacement()
    {
        StopOtherPlacements();

        if (_placementFinished == null)
        {
            _placementFinished = new TaskCompletionSource<bool>();
        }

        DisposePointerVisibilityOverrides();
        _currentVisibilityOverrides = new IPointerStateVisibilityOverride[]
        {
            AppServices.PointerStateService.HidePointer(PointerType.HandGrab),
            AppServices.PointerStateService.HidePointer(PointerType.HandPoke),
            AppServices.PointerStateService.ShowPointer(PointerType.HandRay)
        };

        SetSpatialMeshVisibility(true);
        _placementPosition = transform.position;
        _placementRotation = transform.rotation;
        InPlacement = true;
        RegisterHandlers();
        UpdatePlacementVisualActiveState();

        onPlacing?.Invoke();
        return _placementFinished.Task;
    }

    /// <summary>
    /// Stop the placement process.
    /// </summary>
    public void StopPlacement()
    {
        DisposePointerVisibilityOverrides();
        UnregisterHandlers();
        SetSpatialMeshVisibility(false);

        InPlacement = false;
        _lastUsedPointer = null;
        UpdatePlacementVisualActiveState();

        if (_placementFinished != null)
        {
            _placementFinished.TrySetResult(true);
            _placementFinished = null;
        }

        onPlaced?.Invoke();
    }
    #endregion Public Methods

    #region MonoBehavior Methods
    private void Awake()
    {
        EnsureSpatialAwarenessSystem();
    }

    protected override void Start()
    {
        base.Start();
        _focusProvider = CoreServices.InputSystem?.FocusProvider;
        UpdatePlacementVisualActiveState();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        SetInitialPosition();
    }

    private void Update()
    {
        if (!InPlacement)
        {
            return;
        }

        IMixedRealityPointer usePointer = (_pointerOverride == null) ? _focusProvider?.PrimaryPointer : _pointerOverride;
        if (usePointer == null || usePointer.BaseCursor == null || !usePointer.BaseCursor.IsVisible)
        {
            return;
        }

        // Save last used pointer
        _lastUsedPointer = usePointer;

        // Use the pointer's last hit target. If there's no target fallback to a default distance
        Vector3 newPlacementPosition;
        if (TryFindPlacementPosition(_lastUsedPointer, out newPlacementPosition))
        {
            _placementPosition = newPlacementPosition;
        }

        // If requested, rotate object towards the main camera
        if (faceTowards)
        {
            Vector3 mainCameraPosition = CameraCache.Main.transform.position;
            Vector3 mainCameraToObject = _placementPosition - mainCameraPosition;
            var direction = new Vector3(mainCameraToObject.x, 0, mainCameraToObject.z);
            _placementRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        // Smooth transform to the goal
        UpdateTransformToGoal(transform);

        // Only move placeholder, if it wasn't already moved
        if (placeholder != null && !placeholder.IsChildOf(transform))
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
    /// Cancel this object's visibility override requests
    /// </summary>
    private void DisposePointerVisibilityOverrides()
    {
        if (_currentVisibilityOverrides != null)
        {
            foreach (IPointerStateVisibilityOverride visibilityOverride in _currentVisibilityOverrides)
            {
                visibilityOverride.Dispose();
            }
            _currentVisibilityOverrides = null;
        }
    }

    /// <summary>
    /// Stop all other placement operations
    /// </summary>
    private void StopOtherPlacements()
    {
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

        var camera = CameraCache.Main.transform;
        var direction = camera.TransformDirection(directionFromHead.normalized).normalized;
        transform.position = camera.position + (direction * distanceFromHead);
    }

    /// <summary>
    /// Find the best placement position given a pointer object. This calculates the position based on the pointer's
    /// first intersection that is not part of this game object's hierarchy. If there is no such intersection, the
    /// returned position is the 'default distance' from the pointer's start position.
    /// </summary>
    /// <param name="pointer"></param>
    /// <returns></returns>
    private bool TryFindPlacementPosition(IMixedRealityPointer pointer, out Vector3 position)
    {
        position = Vector3.zero;
        bool hasResult = false;
        if (pointer == null ||
            pointer.Rays == null ||
            pointer.Rays.Length == 0)
        {
            return hasResult;
        }

        RayStep pointerRay = pointer.Rays[0];
        FocusDetails focusDetails;

        // Attempt to use the focus provider to find a hit position
        if (CoreServices.InputSystem.FocusProvider.TryGetFocusDetails(pointer, out focusDetails) &&
            focusDetails.Object != null)
        {
            // Only use focus provider result if target is not part of this hierarchy, otherwise
            // try to find a hit object that is "behind" this object.
            if (!focusDetails.Object.transform.IsChildOf(transform))
            {
                position = focusDetails.Point;
                hasResult = true;
            }
            else
            {
                var hits = Physics.RaycastAll(pointer.Rays[0], this.maxDistance);
                int hitsCount = hits?.Length ?? 0;
                for (int i = 0; i < hitsCount && !hasResult; i++)
                {
                    var currentHit = hits[i];
                    if (currentHit.collider != null &&
                        !currentHit.collider.transform.IsChildOf(transform))
                    {
                        position = currentHit.point;
                        hasResult = true;
                    }
                }
            }
        }

        // Fallback to the default distance, if nothing can be focused.
        if (!hasResult)
        {
            position = pointerRay.Origin + (pointerRay.Direction * this.defaultDistance);
            hasResult = true;
        }

        // Cap the distance of the object at this max distance.
        if ((hasResult) &&
            (this.maxDistance > 0) &&
            (position - pointerRay.Origin).sqrMagnitude > (this.maxDistance * this.maxDistance))
        {
            position = pointerRay.Origin + (pointerRay.Direction * this.maxDistance);
        }

        return hasResult;
    }

    /// <summary>
    /// Updates all object orientations to the goal orientation for this solver, with smoothing accounted for (smoothing may be off)
    /// </summary>
    private void UpdateTransformToGoal(Transform target)
    {
        if (enableSmoothing)
        {
            Vector3 pos = target.position;
            Quaternion rot = target.rotation;

            pos = SmoothTo(pos, _placementPosition, Time.deltaTime, moveLerpTime);
            if (faceTowards)
            {
                rot = SmoothTo(rot, _placementRotation, Time.deltaTime, rotateLerpTime);
            }

            target.position = pos;
            target.rotation = rot;
        }
        else
        {
            target.position = _placementPosition;
            if (faceTowards)
            {
                target.rotation = _placementRotation;
            }
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

        if (CoreServices.SpatialAwarenessSystem == null)
        {
            return;
        }

        if (visible)
        {
            CoreServices.SpatialAwarenessSystem.Enable();
            CoreServices.SpatialAwarenessSystem.ResumeObservers<WindowsMixedRealitySpatialMeshObserver>();
        }
        else
        {
            CoreServices.SpatialAwarenessSystem.SuspendObservers<WindowsMixedRealitySpatialMeshObserver>();
            CoreServices.SpatialAwarenessSystem.Disable();
        }

        CoreServices.SpatialAwarenessSystem.SpatialAwarenessObjectParent?.SetActive(visible); 
    }

    /// <summary>
    /// Ensure spatial awareness system has been created.
    /// </summary>
    private void EnsureSpatialAwarenessSystem()
    {
        if (CoreServices.SpatialAwarenessSystem == null)
        {
            object[] args = { MixedRealityToolkit.Instance.ActiveProfile.SpatialAwarenessSystemProfile };
            if (!MixedRealityToolkit.Instance.RegisterService<IMixedRealitySpatialAwarenessSystem>(
                 MixedRealityToolkit.Instance.ActiveProfile.SpatialAwarenessSystemSystemType, args: args) && CoreServices.SpatialAwarenessSystem != null)
            {
                Debug.LogError("Failed to start the Spatial Awareness System!");
            }
        }
    }
    #endregion Private Methods
}
