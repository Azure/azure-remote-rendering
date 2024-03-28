// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit;

namespace Microsoft.Showcase.App.Pointers
{
    /// <summary>
    /// Custom (far interaction) GGV pointer to also provide the remote cast results from <see cref="RemoteGazeProvider"/>.
    /// </summary>
    public class RemoteGGVPointer : GGVPointer, IRemotePointer
    {
        private IMixedRealityGazeProvider gazeProvider;

        /// <inheritdoc/>
        public Entity FocusEntityTarget => (gazeProvider.GazePointer is IRemotePointer remotePointer) ? remotePointer.FocusEntityTarget : null;

        /// <summary>
        /// The gaze pointer provided by <see cref="RemoteGazeProvider"/>. Used by
        /// <see cref="RemoteObjectExpander.UpdateProxyObject"/> to override the focus target
        /// on both pointers.
        /// </summary>
        public IMixedRealityPointer GazePointer => gazeProvider.GazePointer;

        protected override void OnEnable()
        {
            base.OnEnable();
            gazeProvider = CoreServices.InputSystem.GazeProvider;
        }
    }
}
