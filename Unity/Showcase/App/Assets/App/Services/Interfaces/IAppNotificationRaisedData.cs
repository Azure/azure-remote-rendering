// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

public enum AppNotificationType
{
    Info,
    Warning,
    Error,
}

public interface IAppNotificationRaisedData
{
    /// <summary>
    /// The message.
    /// </summary>
    String Message { get; }

    /// <summary>
    /// The message type.
    /// </summary>
    AppNotificationType Type { get; }
}
