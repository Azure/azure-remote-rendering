// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using System;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary> 
    /// Details about a remote machine, and it's connection status.
    /// </summary>
    public interface IRemoteRenderingSession 
    {
        /// <summary>
        /// Get the session id
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Get the last message
        /// </summary>
        string Message { get; }

        /// <summary>
        /// Get the location of the session.
        /// </summary>
        string Location { get; }

        /// <summary>
        /// Get the domain of the session.
        /// </summary>
        string Domain { get; }

        /// <summary>
        /// Get the session status
        /// </summary>
        RenderingSessionStatus Status { get; }

        /// <summary>
        /// Get a formatted status message
        /// </summary>
        string StatusMessage { get; }

        /// <summary>
        /// Get the session size
        /// </summary>
        RenderingSessionVmSize Size { get; }

        /// <summary>
        /// Get the session elapsed time
        /// </summary>
        TimeSpan ElapsedTime { get; }

        /// <summary>
        /// Get the session lease time
        /// </summary>
        TimeSpan MaxLeaseTime { get; }

        /// <summary>
        /// Get the session expiration time in UTC
        /// </summary>
        DateTime Expiration { get; }

        /// <summary>
        /// Get the connection operations for this session
        /// </summary>
        IRemoteRenderingConnection Connection { get; }

        /// <summary>
        /// Sync the session properties from the cloud.
        /// </summary>
        Task UpdateProperties();

        /// <summary>
        /// Renew the session so the expiration time is later
        /// </summary>
        Task<bool> Renew(TimeSpan increment);

        /// <summary>
        /// Stop the session
        /// </summary>
        Task Stop();

        /// <summary>
        /// Open a web portal to an ARR inspector window
        /// </summary>
        Task OpenWebInspector();
    }
}
