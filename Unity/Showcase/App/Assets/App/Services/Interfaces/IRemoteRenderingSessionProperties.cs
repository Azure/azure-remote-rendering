// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.RemoteRendering;

/// <summary>
/// A cache of the session properties. This manages updating the cached values.
/// </summary>
namespace Microsoft.MixedReality.Toolkit.Extensions
{
    public interface IRemoteRenderingSessionProperties
    {
        /// <summary>
        /// The current set of properties
        /// </summary>
        RenderingSessionProperties Value { get; }

        /// <summary>
        /// Get the time when the properties were last updated.
        /// </summary>
        DateTime LastUpdated { get; }

        /// <summary>
        /// Attempt to update the cached settings.
        /// </summary>
        Task<RenderingSessionProperties> TryUpdate();
    }
}
