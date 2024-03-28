// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using Unity.Profiling;

using Microsoft.Azure.RemoteRendering;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Physics;
using Microsoft.MixedReality.Toolkit.Teleport;

namespace Microsoft.Showcase.App.Pointers
{
    /// <summary>
    /// Custom teleportation pointer to also cast against remote rendered content.
    /// </summary>
    public class RemoteParabolicTeleportPointer : ParabolicTeleportPointer
    {
        private RemotePointerCaster remotePointerCaster = new RemotePointerCaster();
        private Entity focusEntityTarget = null;

        /// <inheritdoc/>
        public Entity FocusEntityTarget => focusEntityTarget;

        private static readonly ProfilerMarker OnPreSceneQueryPerfMarker = new ProfilerMarker("[Showcase] RemoteShellHandRayPointer.OnPreSceneQuery");

        /// <inheritdoc/>
        public override void OnPreSceneQuery()
        {
            using (OnPreSceneQueryPerfMarker.Auto())
            {
                base.OnPreSceneQuery();
                remotePointerCaster.Update(PointerName, SceneQueryType, PrioritizedLayerMasksOverride, Rays);
            }
        }

        private static readonly ProfilerMarker OnSceneQueryPerfMarker = new ProfilerMarker("[Showcase] RemoteShellHandRayPointer.OnSceneQuery");

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
