// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// A class used to hold application wide settings that are automatically saved to disk.
    /// </summary>
	public interface IAppSettingsService : IMixedRealityExtensionService
	{
        /// <summary>
        /// Event raised when the settings have changed.
        /// </summary>
        event EventHandler SettingsChanged;

        /// <summary>
        /// Event raised when the user instance color has changed.
        /// </summary>
        event Action<IAppSettingsService, Color> InstanceUserColorChanged;

        /// <summary>
        /// Get or set the focus highlight settings.
        /// </summary>
        RemoteFocusHighlightSettings FocusHighlightSettings { get; set; }

        /// <summary>
        /// Get or set the user's instance. This setting is not save to file.
        /// </summary>
        Color InstanceUserColor { get; set; }

        /// <summary>
        /// Request to hide the app's user interface (e.g. the menu). To request a show, call cancel on the hide operation.
        /// </summary>
        AppSettingsHideOperation HideInterface();

        /// <summary>
        /// returns the registered MainMenu as a specified type
        /// </summary>
        /// <returns></returns>
        T GetMainMenu<T>();

        /// <summary>
        /// Function to register the main user interface for the app
        /// </summary>
        void RegisterMainMenu(IMainMenu mainMenu);

        /// <summary>
        /// Add an user interface item that can be hidden.
        /// </summary>
        void AddInterface(GameObject uiElement);

        /// <summary>
        /// Invalidate settings so the settings are saved to a file on disk.
        /// </summary>
        void Invalidate();

        /// <summary>
        /// Reload settings from the settings file.
        /// </summary>
        void Reload();

        /// <summary>
        /// Clear the user's instance color
        /// </summary>
        void ClearInstanceUserColor();
    }
}
