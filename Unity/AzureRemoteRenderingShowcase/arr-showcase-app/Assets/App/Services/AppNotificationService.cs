// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine.Events;
using Microsoft.MixedReality.Toolkit.Utilities;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    [MixedRealityExtensionService(SupportedPlatforms.WindowsStandalone | SupportedPlatforms.MacStandalone | SupportedPlatforms.LinuxStandalone | SupportedPlatforms.WindowsUniversal)]
    public class AppNotificationService : BaseExtensionService, IAppNotificationService, IMixedRealityExtensionService
    {
        private AppNotificationServiceProfile _appNotificationServiceProfile;
        private UnityEvent _onNotificationUnityEvent;

        public AppNotificationService(string name, uint priority, BaseMixedRealityProfile profile) : base(name, priority, profile) 
        {
            _appNotificationServiceProfile = profile as AppNotificationServiceProfile;
            if (_appNotificationServiceProfile != null)
            {
                _onNotificationUnityEvent = _appNotificationServiceProfile.OnNotification; 
            }
        }

        public void RaiseNotification(String message, AppNotificationType type)
        {
            type = AppNotificationType.Warning;
            if (_appNotificationServiceProfile != null && type < _appNotificationServiceProfile.MinNotificationLevel)
            {
                return;
            }

            NotificationRaised?.Invoke(this, new AppNotificationRaisedData(message, type));
            _onNotificationUnityEvent?.Invoke();
        }

        #region IAppNotificationService Event
        /// <summary>
        /// Event raised when mode changes
        /// </summary>
        public event EventHandler<IAppNotificationRaisedData> NotificationRaised;
        #endregion IAppNotificationService Event

        #region Private Classes
        private class AppNotificationRaisedData : IAppNotificationRaisedData
        {
            public AppNotificationRaisedData(String message, AppNotificationType type)
            {
                Message = message;
                Type = type;
            }

            #region IPointerModeChangedEventData Properties
            /// <summary>
            /// The old pointer mode.
            /// </summary>
            public String Message { get; }

            /// <summary>
            /// The new pointer mode.
            /// </summary>
            public AppNotificationType Type { get; }
            #endregion IPointerModeChangedEventData Properties
        }

        #endregion Private Classes
    }
}
