// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using Unity.Profiling;

using Microsoft.Azure.RemoteRendering;
using Microsoft.MixedReality.Toolkit.Input;

namespace Microsoft.Showcase.App.Pointers
{
    /// <summary>
    /// Custom (near interaction) sphere pointer to also cast against remote rendered content.
    /// </summary>
    public class RemoteSpherePointer : SpherePointer, IRemotePointer
    {
        private RemotePointerCaster remotePointerCaster = new RemotePointerCaster();
        private Entity focusEntityTarget = null;

        /// <inheritdoc/>
        public override bool IsNearObject => base.IsNearObject || remotePointerCaster.LastHitValid;
        /// <inheritdoc/>
        public override bool IsInteractionEnabled => base.IsInteractionEnabled || remotePointerCaster.LastHitValid;
        /// <inheritdoc/>
        public Entity FocusEntityTarget => focusEntityTarget;

        private static readonly ProfilerMarker OnPreSceneQueryPerfMarker = new ProfilerMarker("[Showcase] RemoteSpherePointer.OnPreSceneQuery");

        /// <inheritdoc/>
        public override void OnPreSceneQuery()
        {
            using(OnPreSceneQueryPerfMarker.Auto())
            {
                base.OnPreSceneQuery();
                remotePointerCaster.Update(PointerName, SceneQueryType, PrioritizedLayerMasksOverride, Rays);
            }
        }

        private static readonly ProfilerMarker OnSceneQueryPerfMarker = new ProfilerMarker("[Showcase] RemoteSpherePointer.OnSceneQuery");

        /// <inheritdoc/>
        public override bool OnSceneQuery(LayerMask[] prioritizedLayerMasks, bool focusIndividualCompoundCollider, out GameObject hitObject, out Vector3 hitPoint, out float hitDistance)
        {
            using(OnSceneQueryPerfMarker.Auto())
            {
                bool localResult = base.OnSceneQuery(prioritizedLayerMasks, focusIndividualCompoundCollider, out hitObject, out hitPoint, out hitDistance);
                bool remoteResult = remotePointerCaster.OnSceneQuery(localResult, prioritizedLayerMasks, ref hitObject, ref hitPoint, ref hitDistance, out focusEntityTarget);

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
