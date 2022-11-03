// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using System;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// Represents a physic remote rendering machine.
    /// </summary>
    public interface IRemoteRenderingMachine : IDisposable
    {
        /// <summary>
        /// The remote rendering actions that can be taken on the remote machine
        /// </summary>
        IRemoteRenderingActions Actions { get; }

        /// <summary>
        /// Details about a remote machine, and it's connection status.
        /// </summary>
        IRemoteRenderingSession Session { get; }

        /// <summary>
        /// Get or set the requested pose mode.
        /// </summary>
        public PoseMode PoseMode { get; set; }
    }
}
