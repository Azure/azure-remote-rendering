// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering.Unity;
using Microsoft.MixedReality.Toolkit.Physics;
using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Input
{
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
    public class RemoteSpherePointer : SpherePointer, IRemoteSpherePointer
    {
        private RemoteSpherePointerLogic remoteLogic = new RemoteSpherePointerLogic();
        private LogHelper<RemoteSpherePointer> log = new LogHelper<RemoteSpherePointer>();

        //public override SceneQueryType SceneQueryType
        //{
        //    get => SceneQueryType.SimpleRaycast;
        //    set
        //    {
        //        if (value != SceneQueryType.SimpleRaycast)
        //        {
        //            log.LogWarning("Pointer does not support query type '{0}'", value);
        //        }
        //    }
        //}

        /// <summary>
        /// Test if the pointer is near any collider that's both on a grabbable layer mask, and has a NearInteractionGrabbable.
        /// Uses SphereCastRadius + NearObjectMargin to determine if near an object within the sector angle
        /// Also returns true of any grabbable objects are within SphereCastRadius even if they aren't within the sector angle
        /// Ignores bounds handlers for the IsNearObject check.
        /// </summary>
        /// <returns>True if the pointer is near any collider that's both on a grabbable layer mask, and has a NearInteractionGrabbable.</returns>
        /// <remarks>This has been updated from the original SpherePointer to handle remote objects.</remarks>
        public override bool IsNearObject => remoteLogic.IsNearRemoteGrabbable || base.IsNearObject;

        /// <summary>
        /// Test if the pointer is within the grabbable radius of collider that's both on a grabbable layer mask, and has a NearInteractionGrabbable.
        /// Uses SphereCastRadius to determine if near an object.
        /// Note: if focus on pointer is locked, will always return true.
        /// </summary>
        /// <returns>True if the pointer is within the grabbable radius of collider that's both on a grabbable layer mask, and has a NearInteractionGrabbable.</returns>
        /// <remarks>This has been updated from the original SpherePointer to handle remote objects.</remarks>
        public override bool IsInteractionEnabled => remoteLogic.IsNearRemoteGrabbable || base.IsInteractionEnabled;

        /// <summary>
        /// Get or set if this pointer is near a remote grabbable object.
        /// </summary>
        public bool IsNearRemoteGrabbable
        {
            get => remoteLogic.IsNearRemoteGrabbable;
            set => remoteLogic.IsNearRemoteGrabbable = value;
        }

        /// <summary>
        /// Get or set the most recent remote pointer result.
        /// </summary>
        public IRemotePointerResult RemoteResult
        {
            get => remoteLogic.RemoteResult;
            set => remoteLogic.RemoteResult = value;
        }

        /// <inheritdoc />
        /// PreSceneQuery here is only concerned with updating the IsNearObject flag by updating queryBufferNearObjectRadius
        public override void OnPreSceneQuery()
        {
			base.OnPreSceneQuery();
            if (TryGetNearGraspPoint(out Vector3 pointerPosition))
            {
                Rays = remoteLogic.UpdateRays(Controller, pointerPosition, SphereCastRadius);
            }
        }

        //// Returns the hit values cached by the queryBuffer during the prescene query step, otherwise runs base logic to obtain remote focus.
        //public override bool OnSceneQuery(LayerMask[] prioritizedLayerMasks, bool focusIndividualCompoundCollider, out GameObject hitObject, out Vector3 hitPoint, out float hitDistance)
        //{
        //    // First try using sphere pointers cache "local collider" hits
        //    if (base.OnSceneQuery(prioritizedLayerMasks, focusIndividualCompoundCollider, out hitObject, out hitPoint, out hitDistance))
        //    {
        //        return true;
        //    }

        //    // Next perfom ray casts
        //    MixedRealityRaycastHit hitInfo = new MixedRealityRaycastHit();
        //    bool querySuccessful = OnSceneQuery(prioritizedLayerMasks, focusIndividualCompoundCollider, out hitInfo, out _, out _);
            
        //    hitObject = focusIndividualCompoundCollider ? hitInfo.collider.gameObject : hitInfo.transform.gameObject;
        //    hitPoint = hitInfo.point;
        //    hitDistance = hitInfo.distance;

        //    return querySuccessful;
        //}

        private class RemoteSpherePointerLogic
        {
            private RayStep[] focusedRays = null;
            private RayStep[] unfocusedRays = null;

            /// <summary>
            /// Get or set if this pointer is near a remote grabbable object.
            /// </summary>
            public bool IsNearRemoteGrabbable { get; set; }

            /// <summary>
            /// Get or set the most recent remote pointer result.
            /// </summary>
            public IRemotePointerResult RemoteResult { get; set; }

            /// <summary>
            /// Get the rays for this pointer
            /// </summary>
            public RayStep[] Rays { get; private set; }

            /// <summary>
            /// Invoked during a pre-scene query so to update Rays
            /// </summary>
            public RayStep[] UpdateRays(IMixedRealityController controller, Vector3 pointerPosition, float sphereCastRadius)
            {
                var hand = controller as IMixedRealityHand;

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
                float distance = sphereCastRadius + sphereCastRadius;
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
                    Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}", "The RemoteSphere pointer couldn't use hand joints. The results of this pointer may not be accurate.");

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

                return Rays;
            }
        }
    }
}
