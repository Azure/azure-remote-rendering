// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    public delegate void RemoteRenderingConnectionEvent(IRemoteRenderingService service, RemoteRenderingConnectionEventArgs args);

    public class RemoteRenderingConnectionEventArgs
    {
        public RenderingSession Session { get; private set; }

        public ConnectionStatus ConnectionStatus { get; private set; }

        public RemoteRenderingConnectionEventArgs(
             RenderingSession session,
             ConnectionStatus connectionStatus)
        {
            Session = session;
            ConnectionStatus = connectionStatus;
        }
    }
}
