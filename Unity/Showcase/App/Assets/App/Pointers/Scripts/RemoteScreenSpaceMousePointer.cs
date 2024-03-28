// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using Unity.Profiling;

using Microsoft.Azure.RemoteRendering;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Physics;

using UInput = UnityEngine.Input;

namespace Microsoft.Showcase.App.Pointers
{
    /// <summary>
    /// Custom mouse pointer to also cast against remote rendered content.
    /// </summary>
    public class RemoteScreenSpaceMousePointer : ScreenSpaceMousePointer, IRemotePointer
    {
        private RemotePointerCaster remotePointerCaster = new RemotePointerCaster();
        private RayStep[] remoteRays = new RayStep[1];
        private Entity focusEntityTarget = null;

        /// <inheritdoc/>
        public Entity FocusEntityTarget => focusEntityTarget;

        private static readonly ProfilerMarker OnPreSceneQueryPerfMarker = new ProfilerMarker("[Showcase] RemoteScreenSpaceMousePointer.OnPreSceneQuery");

        /// <inheritdoc/>
        public override void OnPreSceneQuery()
        {
            using (OnPreSceneQueryPerfMarker.Auto())
            {
                if (UInput.mousePosition.x < 0 ||
                    UInput.mousePosition.y < 0 ||
                    UInput.mousePosition.x > Screen.width ||
                    UInput.mousePosition.y > Screen.height)
                {
                    return;
                }

                base.OnPreSceneQuery();

                // The mouse pointer ray length is Mathf.MaxValue, which creates an unusable terminus.
                remoteRays[0] = new RayStep(Rays[0].Origin, Rays[0].GetPoint(Mathf.Min(PointerExtent, Rays[0].Length)));

                remotePointerCaster.Update(PointerName, SceneQueryType, PrioritizedLayerMasksOverride, remoteRays);
            }
        }

        private static readonly ProfilerMarker OnSceneQueryPerfMarker = new ProfilerMarker("[Showcase] RemoteScreenSpaceMousePointer.OnSceneQuery");

        /// <inheritdoc/>
        public override bool OnSceneQuery(LayerMask[] prioritizedLayerMasks, bool focusIndividualCompoundCollider, out MixedRealityRaycastHit hitInfo, out RayStep ray, out int rayStepIndex)
        {
            using (OnSceneQueryPerfMarker.Auto())
            {
                bool localResult = base.OnSceneQuery(prioritizedLayerMasks, focusIndividualCompoundCollider, out hitInfo, out ray, out rayStepIndex);
                bool remoteResult = remotePointerCaster.OnSceneQuery(localResult, prioritizedLayerMasks, ref hitInfo, ref ray, ref rayStepIndex, out focusEntityTarget);

                return localResult || remoteResult;
            }
        }

        protected override void OnEnable()
        {
            remotePointerCaster.Start();
            focusEntityTarget = null;
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            remotePointerCaster.Stop();
            focusEntityTarget = null;
            base.OnDisable();
        }
    }
}
