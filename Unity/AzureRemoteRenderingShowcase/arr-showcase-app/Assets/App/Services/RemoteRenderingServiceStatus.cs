// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

public enum RemoteRenderingServiceStatus 
{
    Unknown,
    NoSession,
    SessionConstruction,
    SessionStarting,
    SessionStopped,
    SessionExpired,
    SessionError,
    SessionReadyAndDisconnected,
    SessionReadyAndConnecting,
    SessionReadyAndConnected,
    SessionReadyAndConnectionError
}

public static class RemoteRenderingServiceStatusExtensions
{
    public static bool IsSessionReady(this RemoteRenderingServiceStatus status)
    {
        return status == RemoteRenderingServiceStatus.SessionReadyAndConnected ||
            status == RemoteRenderingServiceStatus.SessionReadyAndConnecting ||
            status == RemoteRenderingServiceStatus.SessionReadyAndConnectionError ||
            status == RemoteRenderingServiceStatus.SessionReadyAndDisconnected;
    }
}

