// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// A class used to hold application wide settings that are automatically saved to disk.
    /// </summary>
	[MixedRealityExtensionService(SupportedPlatforms.WindowsStandalone|SupportedPlatforms.MacStandalone|SupportedPlatforms.LinuxStandalone|SupportedPlatforms.WindowsUniversal|SupportedPlatforms.WindowsEditor|SupportedPlatforms.Android|SupportedPlatforms.MacEditor|SupportedPlatforms.LinuxEditor|SupportedPlatforms.IOS|SupportedPlatforms.Web|SupportedPlatforms.Lumin)]
	public class AppSettingsService : BaseExtensionService, IAppSettingsService, IMixedRealityExtensionService
    {
        private bool _invalidated = false;
        private AppSettings _settings = new AppSettings();

        #region Constructors
        public AppSettingsService(string name,  uint priority,  BaseMixedRealityProfile profile) : base(name, priority, profile) 
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
        #endregion IAppSetingsService Events

        #region IAppSettingsService Properties
        /// <summary>
        /// Get or set the focus highlight settings.
        /// </summary>
        public RemoteFocusHighlightSettings FocusHighlightSettings
        {
            get => _settings.FocusHighlightSettings;
            set => _settings.FocusHighlightSettings = value;
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
        #endregion IAppSettingsService Methods

        #region Private Methods
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
                var msg = $"Failled to save app settings to '{DefaultAppSettingsFile}'. Exception:";
                AppServices.AppNotificationService.RaiseNotification($"{msg} {ex.Message}", AppNotificationType.Error);
                Debug.LogWarning($"{msg} {ex.ToString()}");
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
                var msg = $"Failled to load app settings from '{DefaultAppSettingsFile}'. Exception:";
                AppServices.AppNotificationService.RaiseNotification($"{msg} {ex.Message}", AppNotificationType.Error);
                Debug.LogWarning($"{msg} {ex.ToString()}");
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
        }
        #endregion Private Struct
    }
}
