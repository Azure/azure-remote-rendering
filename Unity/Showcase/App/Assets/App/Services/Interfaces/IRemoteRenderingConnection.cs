// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    public interface IRemoteRenderingConnection
    {
        /// <summary>
        /// Event raised when the connections status changes
        /// </summary>
        event ConnectionStatusChangedEventHandler ConnectionStatusChanged;

        /// <summary>
        /// Get the connection status
        /// </summary>
        ConnectionStatus ConnectionStatus { get; }

        /// <summary>
        /// Get the last connection error
        /// </summary>
        Result ConnectionError { get; }

        /// <summary>
        /// Connect to the remote rendering machine.
        /// </summary>
        /// <returns>True if connection succeeded</returns>
        Task<bool> Connect();

        /// <summary>
        /// Disconnect from the remote rendering machine.
        /// </summary>
        Task Disconnect();
    }
}
