// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

public interface IRemoteRenderingStatusChangedArgs
{
    /// <summary>
    /// The previous status.
    /// </summary>
    RemoteRenderingServiceStatus OldStatus { get; }

    /// <summary>
    /// The new status.
    /// </summary>
    RemoteRenderingServiceStatus NewStatus { get; }
}