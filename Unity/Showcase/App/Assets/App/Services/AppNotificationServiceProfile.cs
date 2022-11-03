// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    [MixedRealityServiceProfile(typeof(IAppNotificationService))]
    [CreateAssetMenu(fileName = "AppNotificationServiceProfile", menuName = "ARR Showcase/Configuration Profile/App Notification Service")]
    public class AppNotificationServiceProfile : BaseMixedRealityProfile
    {
        [Tooltip("Sound clip that is played when a notification is raised")]
        public AudioClip NotificationClip;

        [Tooltip("Minimum notification level to display in notification bar")]
        public AppNotificationType MinNotificationLevel;

        [Tooltip("The prefab to use for dialogs")]
        public AppDialog DialogPrefab;

        [Tooltip("Specify the default location for dialogs")]
        public AppDialog.AppDialogLocation DialogDefaultLocation = AppDialog.AppDialogLocation.Default;
    }
}
