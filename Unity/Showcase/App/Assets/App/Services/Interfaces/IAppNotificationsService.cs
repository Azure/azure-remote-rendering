// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;

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

        /// <summary>
        /// Get if a dialog is opened
        /// </summary>
        bool IsDialogOpen { get; }

        void RaiseNotification(String message, AppNotificationType type);

        /// <summary>
        /// Show a new app dialog with the given message.
        /// </summary>
        Task<AppDialog.AppDialogResult> ShowDialog(DialogOptions options);

        /// <summary>
        /// Register a dialog that is hosted by the app's menu. Dialogs that request to be
        /// shown in the app menu will use this.
        /// </summary>
        void RegisterMenuDialog(AppDialog appDialog);
    }

    /// <summary>
    /// Options for app notification dialog
    /// </summary>

    public struct DialogOptions
    {
        /// <summary>
        /// The dialog title
        /// </summary>
        public string Title;

        /// <summary>
        /// The dialog message
        /// </summary>
        public string Message;

        /// <summary>
        /// Get or set the buttons to show
        /// </summary>
        public AppDialog.AppDialogButtons Buttons;

        /// <summary>
        /// Get or set the dialog's location
        /// </summary>
        public AppDialog.AppDialogLocation Location;

        /// <summary>
        /// The text to display on the ok button.  Empty or null will use the default text.
        /// </summary>
        public string OKLabel;

        /// <summary>
        /// The text to display on the no button.  Empty or null will use the default text.
        /// </summary>
        public string NoLabel;

        /// <summary>
        /// The text to display on the cancel button.  Empty or null will use the default text.
        /// </summary>
        public string CancelLabel;
    }
}
