// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering.Unity;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Physics;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;

/// <summary>
/// A specialized sphere pointer that is able to handle both local and remote near grab actions. This is different than
/// the default Mixed Reality Toolkit (MRTK) Sphere Pointer, in that it doesn't perform sphere casts for remote 
/// objects. Instead this pointer creates five rays per hand; thumb to index, thumb to middle, thumb to ring, thumb to
/// pinky, and pointer position to last hit point. These rays are then used during the remote ray casts executed by the 
/// IRemoteFocusProvider class.
///
/// In order for this sphere pointer to function correctly, it requires that the app's focus provider implements 
/// the IRemoteFocusProvider class.
/// </summary>
public class RemoteSpherePointer : BaseControllerPointer, IRemoteSpherePointer, IMixedRealityNearPointer
{
    private SceneQueryType raycastMode = SceneQueryType.SphereOverlap;
    private IMixedRealityHand hand = null;
    private RayStep[] focusedRays = null;
    private RayStep[] unfocusedRays = null;

    /// <inheritdoc />
    public override SceneQueryType SceneQueryType { get { return raycastMode; } set { raycastMode = value; } }

    [SerializeField]
    [Min(0.0f)]
    [Tooltip("Additional distance on top of sphere cast radius when pointer is considered 'near' an object and far interaction will turn off")]
    private float nearObjectMargin = 0.2f;

    /// <summary>
    /// Additional distance on top of<see cref="BaseControllerPointer.SphereCastRadius"/> when pointer is considered 'near' an object and far interaction will turn off.
    /// </summary>
    /// <remarks>
    /// This creates a dead zone in which far interaction is disabled before objects become grabbable.
    /// </remarks>
    public float NearObjectMargin => nearObjectMargin;

    /// <summary>
    /// Distance at which the pointer is considered "near" an object.
    /// </summary>
    /// <remarks>
    /// Sum of <see cref="BaseControllerPointer.SphereCastRadius"/> and <see cref="NearObjectMargin"/>. Entering the <see cref="NearObjectRadius"/> disables far interaction.
    /// </remarks>
    public float NearObjectRadius => SphereCastRadius + NearObjectMargin;

    [SerializeField]
    [Tooltip("The LayerMasks, in prioritized order, that are used to determine the grabbable objects. Remember to also add NearInteractionGrabbable! Only collidables with NearInteractionGrabbable will raise events.")]
    private LayerMask[] grabLayerMasks = { UnityEngine.Physics.DefaultRaycastLayers };

    /// <summary>
    /// The LayerMasks, in prioritized order, that are used to determine the grabbable objects.
    /// </summary>
    /// <remarks>
    /// Only [NearInteractionGrabbables](xref:Microsoft.MixedReality.Toolkit.Input.NearInteractionGrabbable) in one of the LayerMasks will raise events.
    /// </remarks>
    public LayerMask[] GrabLayerMasks => grabLayerMasks;

    [SerializeField]
    [Tooltip("Specify whether queries for grabbable objects hit triggers.")]
    protected QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.UseGlobal;
    /// <summary>
    /// Specify whether queries for grabbable objects hit triggers.
    /// </summary>
    public QueryTriggerInteraction TriggerInteraction => triggerInteraction;

    [SerializeField]
    [Tooltip("Maximum number of colliders that can be detected in a scene query.")]
    [Min(1)]
    private int sceneQueryBufferSize = 64;
    /// <summary>
    /// Maximum number of colliders that can be detected in a scene query.
    /// </summary>
    public int SceneQueryBufferSize => sceneQueryBufferSize;


    private SpherePointerQueryInfo queryBufferNearObjectRadius;
    private SpherePointerQueryInfo queryBufferInteractionRadius;

    /// <summary>
    /// Test if the pointer is near any collider that's both on a grabbable layer mask, and has a NearInteractionGrabbable.
    /// Uses SphereCastRadius + NearObjectMargin to determine if near an object.
    /// </summary>
    /// <returns>True if the pointer is near any collider that's both on a grabbable layer mask, and has a NearInteractionGrabbable.</returns>
    public bool IsNearObject
    {
        get
        {
            return IsNearRemoteGrabbable || queryBufferNearObjectRadius.ContainsGrabbable();
        }
    }

    /// <summary>
    /// Get or set if this pointer is near a remote grabbable object.
    /// </summary>
    public bool IsNearRemoteGrabbable { get; set; }

    /// <summary>
    /// Get or set the most recent remote pointer result.
    /// </summary>
    public IRemotePointerResult RemoteResult { get; set; }

    /// <summary>
    /// Test if the pointer is within the grabbable radius of collider that's both on a grabbable layer mask, and has a NearInteractionGrabbable.
    /// Uses SphereCastRadius to determine if near an object.
    /// Note: if focus on pointer is locked, will always return true.
    /// </summary>
    /// <returns>True if the pointer is within the grabbable radius of collider that's both on a grabbable layer mask, and has a NearInteractionGrabbable.</returns>
    public override bool IsInteractionEnabled
    {
        get
        {
            if (IsFocusLocked)
            {
                return true;
            }
            return base.IsInteractionEnabled &&
                queryBufferInteractionRadius.ContainsGrabbable() || IsNearRemoteGrabbable;
        }
    }

    private void Awake()
    {
        queryBufferNearObjectRadius = new SpherePointerQueryInfo(sceneQueryBufferSize, NearObjectRadius);
        queryBufferInteractionRadius = new SpherePointerQueryInfo(sceneQueryBufferSize, SphereCastRadius);
    }

    /// <inheritdoc />
    public override void OnPreSceneQuery()
    {
        if (hand == null)
        {
            hand = Controller as IMixedRealityHand;
        }

        //
        // Update query buffer which pointer position
        //

        Vector3 pointerPosition;
        if (TryGetNearGraspPoint(out pointerPosition))
        {
            var layerMasks = PrioritizedLayerMasksOverride ?? GrabLayerMasks;
            for (int i = 0; i < layerMasks.Length; i++)
            {
                if (queryBufferNearObjectRadius.TryUpdateQueryBufferForLayerMask(layerMasks[i], pointerPosition, triggerInteraction))
                {
                    break;
                }
            }

            for (int i = 0; i < layerMasks.Length; i++)
            {
                if (queryBufferInteractionRadius.TryUpdateQueryBufferForLayerMask(layerMasks[i], pointerPosition, triggerInteraction))
                {
                    break;
                }
            }
        }

        //
        // Update Rays with rays 
        //

        // Create a rays cache, size base on how many rays will be created.
        // Also there are more rays used when there is a focused item.
        int focusedRayCount = 4;
        int unfocusedRayCount = focusedRayCount - 1;
        int currentRay = 0;

        if (focusedRays == null || focusedRays.Length != focusedRayCount)
        {
            focusedRays = new RayStep[focusedRayCount];
        }

        if (unfocusedRays == null || unfocusedRays.Length != unfocusedRayCount)
        {
            unfocusedRays = new RayStep[unfocusedRayCount];
        }

        // Was the last result focusing a remote target?
        bool hadRemoteFocus = RemoteResult != null && RemoteResult.IsTargetValid;
        Rays = hadRemoteFocus ? focusedRays : unfocusedRays;

        // First try creating web from thumb to other fingers. If that fails just use pointer position, and shoot rays along the axes.
        float distance = SphereCastRadius + SphereCastRadius;
        if (hand != null)
        {
            // Only query connection between the thumb and three fingers, so to reduce latency.
            MixedRealityPose pinkyTip;
            MixedRealityPose ringTip;
            MixedRealityPose indexPose;
            MixedRealityPose thumbPose;

            if ((hand.TryGetJoint(TrackedHandJoint.ThumbTip, out thumbPose)) &&
                (hand.TryGetJoint(TrackedHandJoint.RingTip, out ringTip)) &&
                (hand.TryGetJoint(TrackedHandJoint.PinkyTip, out pinkyTip)) &&
                (hand.TryGetJoint(TrackedHandJoint.IndexTip, out indexPose)))
            {
                // Could improve this if "RayStep" had an update function that took a position and direction.
                var start = thumbPose.Position;
                var end = start + ((indexPose.Position - start).normalized * distance);
                Rays[currentRay++].UpdateRayStep(ref start, ref end);

                end = start + ((ringTip.Position - start).normalized * distance);
                Rays[currentRay++].UpdateRayStep(ref start, ref end);

                end = start + ((pinkyTip.Position - start).normalized * distance);
                Rays[currentRay++].UpdateRayStep(ref start, ref end);
            }
        }
        else
        {
            Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "The RemoteSphere pointer couldn't use hand joints. The results of this pointer may not be accurate.");

            // Could improve this if "RayStep" had an update function that took a position and direction.
            var end = pointerPosition + (Vector3.up * distance);
            Rays[currentRay++].UpdateRayStep(ref pointerPosition, ref end);

            end = pointerPosition + (Vector3.right * distance);
            Rays[currentRay++].UpdateRayStep(ref pointerPosition, ref end);

            end = pointerPosition + (Vector3.forward * distance);
            Rays[currentRay++].UpdateRayStep(ref pointerPosition, ref end);
        }

        // If there was a previous target, try aimming for that target now
        if (hadRemoteFocus)
        {
            // Could improve this if "RayStep" had an update function that took a position and direction.
            var end = pointerPosition + ((RemoteResult.RemoteResult.HitPosition.toUnityPos() - pointerPosition).normalized * distance);
            Rays[currentRay++].UpdateRayStep(ref pointerPosition, ref end);
        }
    }

    /// <summary>
    /// Gets the position of where grasp happens
    /// For IMixedRealityHand it's the average of index and thumb.
    /// For any other IMixedRealityController, return just the pointer origin
    /// </summary>
    public bool TryGetNearGraspPoint(out Vector3 result)
    {
        // If controller is of kind IMixedRealityHand, return average of index and thumb
        if (hand != null)
        { 
            hand.TryGetJoint(TrackedHandJoint.IndexTip, out MixedRealityPose index);
            if (index != null)
            {
                hand.TryGetJoint(TrackedHandJoint.ThumbTip, out MixedRealityPose thumb);
                if (thumb != null)
                {
                    result = 0.5f * (index.Position + thumb.Position);
                    return true;
                }
            }
        }
        else
        {
            result = Position;
            return true;
        }

        result = Vector3.zero;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetDistanceToNearestSurface(out float distance)
    {
        var focusProvider = CoreServices.InputSystem?.FocusProvider;
        if (focusProvider != null)
        {
            FocusDetails focusDetails;
            if (focusProvider.TryGetFocusDetails(this, out focusDetails))
            {
                distance = focusDetails.RayDistance;
                return true;
            }
        }

        distance = 0.0f;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetNormalToNearestSurface(out Vector3 normal)
    {
        var focusProvider = CoreServices.InputSystem?.FocusProvider;
        if (focusProvider != null)
        {
            FocusDetails focusDetails;
            if (focusProvider.TryGetFocusDetails(this, out focusDetails))
            {
                normal = focusDetails.Normal;
                return true;
            }
        }

        normal = Vector3.forward;
        return false;
    }

    /// <summary>
    /// Helper class for storing and managing near grabbables close to a point
    /// </summary>
    private class SpherePointerQueryInfo
    {
        /// <summary>
        /// How many colliders are near the point from the latest call to TryUpdateQueryBufferForLayerMask 
        /// </summary>
        private int numColliders;

        /// <summary>
        /// Fixed-length array used to store physics queries
        /// </summary>
        private Collider[] queryBuffer;

        /// <summary>
        /// Distance for performing queries.
        /// </summary>
        private float queryRadius;

        /// <summary>
        /// The grabbable near the QueryRadius. 
        /// </summary>
        private NearInteractionGrabbable grabbable;

        public SpherePointerQueryInfo(int bufferSize, float radius)
        {
            numColliders = 0;
            queryBuffer = new Collider[bufferSize];
            queryRadius = radius;
        }

        public bool TryUpdateQueryBufferForLayerMask(LayerMask layerMask, Vector3 pointerPosition, QueryTriggerInteraction triggerInteraction)
        {
            grabbable = null;
            numColliders = UnityEngine.Physics.OverlapSphereNonAlloc(
                pointerPosition,
                queryRadius,
                queryBuffer,
                layerMask,
                triggerInteraction);

            if (numColliders == queryBuffer.Length)
            {
                Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, $"Maximum number of {numColliders} colliders found in SpherePointer overlap query. Consider increasing the query buffer size in the pointer profile.");
            }

            for (int i = 0; i < numColliders; i++)
            {
                if (grabbable = queryBuffer[i].GetComponent<NearInteractionGrabbable>())
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// Returns true if any of the objects inside QueryBuffer contain a grabbable
        /// </summary>
        public bool ContainsGrabbable()
        {
            return grabbable != null;
        }
    }
}
