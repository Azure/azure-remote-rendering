// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// A class used to hold application wide settings that are automatically saved to disk.
    /// </summary>
	[MixedRealityExtensionService(SupportedPlatforms.WindowsStandalone | SupportedPlatforms.MacStandalone | SupportedPlatforms.LinuxStandalone | SupportedPlatforms.WindowsUniversal | SupportedPlatforms.WindowsEditor | SupportedPlatforms.Android | SupportedPlatforms.MacEditor | SupportedPlatforms.LinuxEditor | SupportedPlatforms.IOS | SupportedPlatforms.Web | SupportedPlatforms.Lumin)]
    public class AppSettingsService : BaseExtensionService, IAppSettingsService, IMixedRealityExtensionService
    {
        private bool _invalidated = false;
        private AppSettings _settings = new AppSettings();
        private List<WeakReference<GameObject>> _userInterfaces = new List<WeakReference<GameObject>>();
        private int _hideRequests = 0;
        private IMainMenu _mainMenu = null;

        #region Constructors
        public AppSettingsService(string name, uint priority, BaseMixedRealityProfile profile) : base(name, priority, profile)
        {
        }
        #endregion Constructors

        #region Private Properties
        /// <summary>
        /// Get the default file path of the settings file.
        /// </summary>
        private static string DefaultAppSettingsFile
        {
            get
            {
                return $"{Application.persistentDataPath}/app.settings.xml";
            }
        }
        #endregion Private Properties

        #region IMixedRealityExtensionService Methods
        public override async void Initialize()
        {
            if (!await Load())
            {
                // write default settings.
                Invalidate();
            }
        }

        public override void LateUpdate()
        {
            if (_invalidated)
            {
                _invalidated = false;
                Save();
            }
        }
        #endregion IMixedRealityExtensionService Methods

        #region IAppSettingService Events
        /// <summary>
        /// Event raised when the settings have changed.
        /// </summary>
        public event EventHandler SettingsChanged;

        /// <summary>
        /// Event raised when the user instance color has changed.
        /// </summary>
        public event Action<IAppSettingsService, Color> InstanceUserColorChanged;
        #endregion IAppSettingsService Events

        #region IAppSettingsService Properties
        /// <summary>
        /// Get or set the focus highlight settings.
        /// </summary>
        public RemoteFocusHighlightSettings FocusHighlightSettings
        {
            get => _settings.FocusHighlightSettings;
            set => _settings.FocusHighlightSettings = value;
        }

        /// <summary>
        /// Get or set the user's instance. This setting is not save to file.
        /// </summary>
        public Color InstanceUserColor
        {
            get => _settings.InstanceUserColor;

            set
            {
                if (_settings.InstanceUserColor != value)
                {
                    _settings.InstanceUserColor = value;
                    InstanceUserColorChanged?.Invoke(this, value);
                }
            }
        }
        #endregion  IAppSettingsService Properties

        #region IAppSettingsService Methods
        /// <summary>
        /// Invalidate settings so the settings are saved to a file on disk.
        /// </summary>
        public void Invalidate()
        {
            _invalidated = true;
            SettingsChanged?.Invoke(this, null);
        }

        /// <summary>
        /// Reload settings from the settings file.
        /// </summary>
        public async void Reload()
        {
            await Load();
        }

        /// <summary>
        /// Request to hide the app's user interface (e.g. the menu). To request a show, call cancel on the hide operation.
        /// </summary>
        public AppSettingsHideOperation HideInterface()
        {
            return RequestHide();
        }

        /// <summary>
        /// Add an user interface item that can be hidden.
        /// </summary>
        public void AddInterface(GameObject uiElement)
        {        
            lock (_userInterfaces)
            {
                _userInterfaces.Add(new WeakReference<GameObject>(uiElement));
            }
        }

        /// <summary>
        /// Clear the user's instance color
        /// </summary>
        public void ClearInstanceUserColor()
        {
            InstanceUserColor = new Color(0, 0, 0, 0);
        }


        public T GetMainMenu<T>()
        {
            T result = default(T);

            if (_mainMenu != null && _mainMenu is T)
            {
                result = (T)_mainMenu;
            }

            return result;
        }

        /// <summary>
        /// register the main ui point in the application.
        /// </summary>
        public void RegisterMainMenu(IMainMenu mainMenu)
        {
            if (_mainMenu != null)
            {
                var msg = $"Registering a new mainMenu for the application. old:'{_mainMenu.Name}, new:{mainMenu.Name}'.";

                Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}", msg);
            }

            _mainMenu = mainMenu;
        }
        #endregion IAppSettingsService Methods

        #region Private Methods
        /// <summary>
        /// Handle hide interface request.
        /// </summary>
        private AppSettingsHideOperation RequestHide()
        {
            AppSettingsHideOperation appSettingsHideOperation = new AppSettingsHideOperation(RequestShow);
            if (Interlocked.Increment(ref _hideRequests) == 1)
            {
                ShowHideInterfaces(false);
            }
            return appSettingsHideOperation;
        }

        /// <summary>
        /// Handle show interface request.
        /// </summary>
        private void RequestShow()
        {
            int value = Interlocked.Decrement(ref _hideRequests);
            if (value < 0)
            {
                value = Interlocked.Increment(ref _hideRequests);
            }

            if (value <= 0)
            {
                ShowHideInterfaces(true);
            }
        }

        /// <summary>
        /// Show or hide interfaces
        /// </summary>
        private void ShowHideInterfaces(bool show)
        {
            lock (_userInterfaces)
            {
                for (int i = _userInterfaces.Count - 1; i >= 0; i--)
                {
                    GameObject uiElement = null;
                    if (_userInterfaces[i].TryGetTarget(out uiElement))
                    {
                        uiElement.SetActive(show);
                    }
                    else
                    {
                        _userInterfaces.RemoveAt(i);
                    }
                }
            }
        }

        /// <summary>
        /// Save the settings.
        /// </summary>
        private async void Save()
        {
            try
            {
                await LocalStorageHelper.Save<AppSettings>(DefaultAppSettingsFile, _settings);
            }
            catch (Exception ex)
            {
                var msg = $"Failed to save app settings to '{DefaultAppSettingsFile}'. Reason: {ex.Message}";
                AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Warning);
                Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}",  msg);
            }
        }

        private async Task<bool> Load()
        {
            bool loaded = false;
            AppSettings fileSettings = null;
            try
            {
                fileSettings = await LocalStorageHelper.Load<AppSettings>(DefaultAppSettingsFile);
            }
            catch (Exception ex)
            {
                var msg = $"Failed to load app settings from '{DefaultAppSettingsFile}'. Reason: {ex.Message}";
                AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Warning);
                Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}",  msg);
            }

            if (fileSettings != null)
            {
                loaded = true;
                _settings = fileSettings;
                SettingsChanged?.Invoke(this, null);
            }

            return loaded;
        }
        #endregion Private Methods

        #region Private Struct
        /// <summary>
        /// A struct for holding current application settings
        /// </summary>
        [Serializable]
        public class AppSettings
        {
            /// <summary>
            /// The settings for the focus highlighting.
            /// </summary>
            public RemoteFocusHighlightSettings FocusHighlightSettings = RemoteFocusHighlightSettings.Default;

            /// <summary>
            /// The user's instance color.  This is not saved to file
            /// </summary>
            public Color InstanceUserColor = new Color(0, 0, 0, 0);

            /// <summary>
            /// Do not serialize user's instance color
            /// </summary>
            public bool ShouldSerializeInstanceUserColor() { return false; }
        }
        #endregion Private Struct
    }
}
