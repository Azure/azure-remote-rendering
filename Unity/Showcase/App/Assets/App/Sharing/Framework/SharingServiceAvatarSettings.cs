// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// Hold various avatar settings used by the sharing service
    /// </summary>
    public struct SharingServiceAvatarSettings
    {
        /// <summary>
        /// Should the current local user's avatar be rendered.
        /// </summary>
        public bool ShowCurrent;

        /// <summary>
        /// Should users in the same physical space have their avatars rendered
        /// </summary>
        public bool ShowCoLocated;

        /// <summary>
        /// Should users in a different same physical space have their avatars rendered
        /// </summary>
        public bool ShowRemote;

        /// <summary>
        /// Should avatar debug joints be drawn
        /// </summary>
        public bool ShowDebugJoints;

        /// <summary>
        /// Should avatar namesplates be shown
        /// </summary>
        public bool ShowNamePlates;

        /// <summary>
        /// Create default settings struct
        /// </summary>
        public static SharingServiceAvatarSettings Default
        {
            get
            {
                return new SharingServiceAvatarSettings()
                {
                    ShowCurrent = false,
                    ShowCoLocated = false,
                    ShowRemote = true,
                    ShowDebugJoints = false,
                    ShowNamePlates = true,
                };
            }
        }

        /// <summary>
        /// Hide all avatar components
        /// </summary>
        public static SharingServiceAvatarSettings HideAll
        {
            get
            {
                return new SharingServiceAvatarSettings()
                {
                    ShowCurrent = false,
                    ShowCoLocated = false,
                    ShowRemote = false,
                    ShowDebugJoints = false,
                    ShowNamePlates = false,
                };
            }
        }
    }
}