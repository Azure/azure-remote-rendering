// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    /// <summary>
    /// Photon related helpers related to the supported Photon features
    /// </summary>
    public class PhotonFeatureSupport
    {
        private static bool _hasVoiceBinaries = false;
        private static PlatformType _type = PlatformType.Unknown;
        private static LogHelper<PhotonFeatureSupport> _logger = new LogHelper<PhotonFeatureSupport>();

        static PhotonFeatureSupport()
        {
            _hasVoiceBinaries = Type.GetType("Photon.Voice.VoiceClient, Assembly-CSharp") != null ||
                Type.GetType("Photon.Voice.VoiceClient, Assembly-CSharp-firstpass") != null ||
                Type.GetType("Photon.Voice.VoiceClient, PhotonVoice.API") != null;
        }

        private static PlatformType Platform
        {
            get
            {
                if (_type == PlatformType.Unknown)
                {
                    bool is64Bit;
                    try
                    {
                        is64Bit = Environment.Is64BitOperatingSystem;
                    }
                    catch
                    {
                        // This throws on 32 bit OSes
                        is64Bit = false;
                    }

                    switch (Application.platform)
                    {
                        case RuntimePlatform.WindowsEditor:
                        case RuntimePlatform.WindowsPlayer:
                            _type = is64Bit ? PlatformType.Windows_Amd64 : PlatformType.Windows_x86;
                            break;

                        case RuntimePlatform.WSAPlayerARM:
                            _type = is64Bit ? PlatformType.UniversalWindows_x64 : PlatformType.UniversalWindows_Arm;
                            break;

                        case RuntimePlatform.WSAPlayerX86:
                            _type = PlatformType.UniversalWindows_x86;
                            break;

                        case RuntimePlatform.WSAPlayerX64:
                            _type = PlatformType.UniversalWindows_x64;
                            break;

                        default:
                            _logger.LogWarning("Unknown platform found ({0})", Application.platform);
                            break;
                    }
                }

                return _type;
            }
        }

        /// <summary>
        /// Is Photon Voice APIs supported
        /// </summary>
        public static bool HasVoice
        {
            get
            {
                if (!_hasVoiceBinaries)
                {
                    return false;
                }
                else
                {
                    // Photon Voice native components don't support ARM64 for free.
                    // If you have the paid version of Photon Voice, remove this check
                    return Platform != PlatformType.UniversalWindows_Arm64;
                }
            }
        }

        /// <summary>
        /// Get the app type
        /// </summary>
        private enum PlatformType
        {
            Unknown,
            UniversalWindows_Arm,
            UniversalWindows_Arm64,
            UniversalWindows_x86,
            UniversalWindows_x64,
            Windows_x86,
            Windows_Amd64
        }
    }
}
