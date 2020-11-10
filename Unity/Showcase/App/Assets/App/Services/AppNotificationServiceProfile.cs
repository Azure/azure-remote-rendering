// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using UnityEngine.Events;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    [MixedRealityServiceProfile(typeof(IAppNotificationService))]
    [CreateAssetMenu(fileName = "AppNotificationServiceProfile", menuName = "MixedRealityToolkit/AppNotificationService Configuration Profile")]
    public class AppNotificationServiceProfile : BaseMixedRealityProfile
    {
        [Tooltip("Event that is invoked when a notification is raised")]
        public UnityEvent OnNotification;

        [Tooltip("Minimum notification level to display in notification bar")]
        public AppNotificationType MinNotificationLevel;
    }
}
