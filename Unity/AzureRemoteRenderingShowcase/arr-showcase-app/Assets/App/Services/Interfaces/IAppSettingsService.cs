// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

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
        /// Get or set the focus highlight settings.
        /// </summary>
        RemoteFocusHighlightSettings FocusHighlightSettings { get; set; }

        /// <summary>
        /// Invalidate settings so the settings are saved to a file on disk.
        /// </summary>
        void Invalidate();

        /// <summary>
        /// Reload settings from the settings file.
        /// </summary>
        void Reload();
    }
}
