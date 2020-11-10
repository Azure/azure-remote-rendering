// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;

namespace Microsoft.MixedReality.Toolkit.Input
{
    /// <summary>
    /// This represents a pointer ray cast performed on the Azure Remote Rendering service.
    /// </summary>
    public interface IRemotePointerResult
    {
        /// <summary>
        /// The source of the pointer ray cast.
        /// </summary>
        IMixedRealityPointer Pointer { get; }

        /// <summary>
        /// The Azure Remote Rendering ray cast hit.
        /// </summary>
        RayCastHit RemoteResult { get; }

        /// <summary>
        /// The remote Entity that was hit.
        /// </summary>
        Entity TargetEntity { get; }

        /// <summary>
        /// If true, the pointer ray cast hit a valid remote object.
        /// </summary>
        bool IsTargetValid { get; }
    }
}
