// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    [MixedRealityExtensionService(SupportedPlatforms.WindowsStandalone | SupportedPlatforms.MacStandalone | SupportedPlatforms.LinuxStandalone | SupportedPlatforms.WindowsUniversal)]
    public class AppNotificationService : BaseExtensionService, IAppNotificationService, IMixedRealityExtensionService
    {
        private AppNotificationServiceProfile _appNotificationServiceProfile;
        private DialogInformation _currentDialog;
        private Queue<DialogInformation> _pendingDialogs = new Queue<DialogInformation>();
        private LogHelper<AppNotificationService> _log = new LogHelper<AppNotificationService>();
        private AudioClip _onNotificationAudioClip;
        private AudioSource _onNotificationAudioSource;
        private AppDialog _menuDialog;
        private bool _pendingNotificationSound;


        public AppNotificationService(string name, uint priority, BaseMixedRealityProfile profile) : base(name, priority, profile) 
        {
            _appNotificationServiceProfile = profile as AppNotificationServiceProfile;
            if (_appNotificationServiceProfile != null)
            {
                _onNotificationAudioClip = _appNotificationServiceProfile.NotificationClip; 
            }
        }

        #region BaseExtensionService Functions
        public override void Initialize()
        {
            base.Initialize();

            if (Application.isPlaying)
            {
                GameObject audioSourceGameObject = new GameObject("AppNotificationService AudioSource");
                audioSourceGameObject.transform.SetParent(CameraCache.Main.transform, worldPositionStays: false);
                _onNotificationAudioSource = audioSourceGameObject.AddComponent<AudioSource>();
            }
        }

        public override void Update()
        {
            if (_currentDialog == null && _pendingDialogs.Count > 0)
            {
                ShowDialog(_pendingDialogs.Dequeue());
            }
        }

        public override void LateUpdate()
        {
            if (_pendingNotificationSound)
            {
                _pendingNotificationSound = false;
                PlayNotificationAudioClip();
            }
        }

        public override void Destroy()
        {
            base.Destroy();

            if (_onNotificationAudioSource != null &&
                _onNotificationAudioSource.gameObject != null)
            {
                UnityEngine.Object.Destroy(_onNotificationAudioSource.gameObject);
            }
            _onNotificationAudioSource = null;
        }
        #endregion BaseExtensionService Functions

        #region IAppNotificationService Properties
        /// <summary>
        /// Get if a dialog is opened
        /// </summary>
        public bool IsDialogOpen => _currentDialog != null;
        #endregion IAppNotificationService Properties

        #region IAppNotificationService Functions
        public void RaiseNotification(String message, AppNotificationType type)
        {
            if (_appNotificationServiceProfile != null && type < _appNotificationServiceProfile.MinNotificationLevel)
            {
                return;
            }

            NotificationRaised?.Invoke(this, new AppNotificationRaisedData(message, type));
            QueueNotificationAudioClip();
        }

        /// <summary>
        /// Show a new app dialog with the given message.
        /// </summary>
        public Task<AppDialog.AppDialogResult> ShowDialog(DialogOptions options)
        {
            DialogInformation dialogInformation = new DialogInformation(options);
            _pendingDialogs.Enqueue(dialogInformation);
            return dialogInformation.Result.Task;
        }

        /// <summary>
        /// Register a dialog that is hosted by the app's menu. Dialogs that request to be
        /// shown in the app menu will use this.
        /// </summary>
        public void RegisterMenuDialog(AppDialog appDialog)
        {
            _menuDialog = appDialog;
        }
        #endregion IAppNotificationService Functions

        #region IAppNotificationService Event
        /// <summary>
        /// Event raised when mode changes
        /// </summary>
        public event EventHandler<IAppNotificationRaisedData> NotificationRaised;
        #endregion IAppNotificationService Event

        #region Private Functions
        private void QueueNotificationAudioClip()
        {
            _pendingNotificationSound = true;
        }

        private void PlayNotificationAudioClip()
        {
            if (_onNotificationAudioSource == null || _onNotificationAudioClip == null)
            {
                return;
            }

            _onNotificationAudioSource.PlayOneShot(_onNotificationAudioClip);
        }

        private async void ShowDialog(DialogInformation dialog)
        {
            if (_appNotificationServiceProfile?.DialogPrefab == null || 
                _currentDialog != null ||
                dialog == null)
            {
                return;
            }

            _currentDialog = dialog;

            AppDialog.AppDialogResult result;
            AppDialog appDialog = null;
            try
            {
                appDialog = GetOrCreateAppDialog(dialog.Options);
                appDialog.DestroyOnClose = false;
                appDialog.DialogText.text = dialog.Options.Message;
                appDialog.DialogHeaderText.text = dialog.Options.Title;

                if (dialog.Options.Buttons != AppDialog.AppDialogButtons.None)
                {
                    appDialog.Buttons = dialog.Options.Buttons;
                }
                else
                {
                    appDialog.Buttons = 
                        AppDialog.AppDialogButtons.Ok | 
                        AppDialog.AppDialogButtons.No;
                }

                SetLabel(appDialog.OkButtonText, dialog.Options.OKLabel, "OK");
                SetLabel(appDialog.NoButtonText, dialog.Options.NoLabel, "No");
                SetLabel(appDialog.CancelButtonText, dialog.Options.CancelLabel, "Cancel");

                QueueNotificationAudioClip();
                result = await appDialog.Open();
            }
            catch (Exception ex)
            {
                result = AppDialog.AppDialogResult.Cancel;
                _log.LogError("Failed to show dialog. Exception: {0}", ex);
            }

            if ((appDialog != null) && (appDialog != _menuDialog))
            {
                GameObject.Destroy(appDialog.gameObject);
            }

            _currentDialog.Result.TrySetResult(result);
            _currentDialog = null;
        }

        private AppDialog GetOrCreateAppDialog(DialogOptions options)
        {
            bool isMenuDialog = 
                (options.Location == AppDialog.AppDialogLocation.Menu) ||
                (options.Location == AppDialog.AppDialogLocation.Default && _appNotificationServiceProfile.DialogDefaultLocation == AppDialog.AppDialogLocation.Menu);

            AppDialog result;
            if (isMenuDialog && _menuDialog != null)
            {
                result = _menuDialog;
            }
            else
            {
                var dialogObject = UnityEngine.Object.Instantiate(_appNotificationServiceProfile.DialogPrefab.gameObject);
                result = dialogObject.GetComponent<AppDialog>();
            }

            return result;
        }

        private void SetLabel(TextMeshPro mesh, string label, string fallback)
        {
            if (mesh != null)
            {
                if (string.IsNullOrEmpty(label))
                {
                    mesh.text = fallback;
                }
                else
                {
                    mesh.text = label;
                }
            }
        }
        #endregion Private Functions

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

        /// <summary>
        /// Dialog title, message, and result
        /// </summary>
        private class DialogInformation
        {
            public DialogOptions Options { get; }

            public TaskCompletionSource<AppDialog.AppDialogResult> Result { get; }

            public AppDialog Inner { get; private set; }

            public DialogInformation(DialogOptions options)
            {
                Options = options;
                Result = new TaskCompletionSource<AppDialog.AppDialogResult>();
            }
        }
        #endregion Private Classes
    }
}
