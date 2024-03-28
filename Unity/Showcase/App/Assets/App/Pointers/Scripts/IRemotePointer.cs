// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.MixedReality.Toolkit.Input;

namespace Microsoft.Showcase.App.Pointers
{
    /// <summary>
    /// Additional Azure Remote Rendering specifc data for pointers.
    /// </summary>
    public interface IRemotePointer : IMixedRealityPointer
    {
        /// <summary>
        /// The currently focused remote <see cref="Entity"/>.
        /// </summary>
        Entity FocusEntityTarget { get; }
    }
}
