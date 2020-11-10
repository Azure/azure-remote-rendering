// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;

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
    public static bool IsValid(this RenderingSessionStatus status)
    {
        return status != RenderingSessionStatus.Error &&
            status != RenderingSessionStatus.Expired &&
            status != RenderingSessionStatus.Stopped;
    }
}

