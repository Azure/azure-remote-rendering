// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Input
{
    /// <summary>
    /// A specialized focus provider for Azure Remote Rendering. This will execute both local and remote ray casts,
    /// and give focus either to local and remote objects.
    /// </summary>
    public interface IRemoteFocusProvider : IMixedRealityFocusProvider
    {
        /// <summary>
        /// Get the pointer's remote focus information. This result contains which remote Entity the pointer is currently
        /// focused on.
        /// </summary>
        IRemotePointerResult GetRemoteResult(IMixedRealityPointer pointer);

        /// <summary>
        /// Get the entity from a pointer target. This makes it easier to consume Input events which return game objects.
        /// Upon handling those events, you can use this method to resolve the entity that was focused.
        /// </summary>
        Entity GetEntity(IMixedRealityPointer pointer, GameObject pointerTarget);

        /// <summary>
        /// Try switching focus to a child object. This is will fail if the current target is not a parent of the child.
        /// </summary>
        void TryFocusingChild(IMixedRealityPointer pointer, GameObject childTarget);
    }
}
