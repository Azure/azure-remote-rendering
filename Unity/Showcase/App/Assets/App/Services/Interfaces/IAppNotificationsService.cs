// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.MixedReality.Toolkit;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// The interface helps relaying messages to the notification bar via a central mechanism.
    /// </summary>
	public interface IAppNotificationService : IMixedRealityExtensionService
    {
        /// <summary>
        /// Event raised on notification
        /// </summary>
        event EventHandler<IAppNotificationRaisedData> NotificationRaised;

        void RaiseNotification(String message, AppNotificationType type);
    }
}
