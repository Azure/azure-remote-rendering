// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// Load in a service profile configuration from a deployed appx file or the app's local state directory
    /// </summary>
    public class SharingServiceProfileLoader
    {
        /// <summary>
        /// Attempt to load the profile from the Override File Path. 
        /// </summary>
        /// <param name="fallback">The fallback data to use if no file is found.</param>
        /// <returns>The loaded configuration</returns>
        public static async Task<SharingServiceProfile> Load(SharingServiceProfile fallback = null)
        {
            ServiceConfigurationFile file = new ServiceConfigurationFile();

            // load in the installed file 
            ServiceConfigurationFile.FileData fileData = await file.LoadMerged();
            fallback = CreateProfile(fileData, fallback);

            return fallback;
        }

        /// <summary>
        /// Create a profile from the given file data, or using the fallback data.
        /// </summary>
        private static SharingServiceProfile CreateProfile(ServiceConfigurationFile.FileData fileData, SharingServiceProfile fallback)
        {
            SharingServiceProfile result;
            if (fallback == null)
            {
                result = ScriptableObject.CreateInstance<SharingServiceProfile>();
            }
            else
            {
                result = Object.Instantiate(fallback);
            }

            if (fileData == null)
            {
                return result;
            }

            var sharing = fileData.Sharing;
            if (sharing != null)
            {
                if (sharing.ShouldSerializeProvider())
                {
                    result.Provider = sharing.Provider;
                }

                if (sharing.ShouldSerializeRoomNameFormat())
                {
                    result.RoomNameFormat = sharing.RoomNameFormat;
                }

                if (sharing.ShouldSerializePrivateRoomNameFormat())
                {
                    result.PrivateRoomNameFormat = sharing.PrivateRoomNameFormat;
                }

                if (sharing.ShouldSerializeVerboseLogging())
                {
                    result.VerboseLogging = sharing.VerboseLogging.Value;
                }

                if (sharing.ShouldSerializePhotonRealtimeId())
                {
                    result.PhotonRealtimeId = sharing.PhotonRealtimeId;
                }

                if (sharing.ShouldSerializePhotonVoiceId())
                {
                    result.PhotonVoiceId = sharing.PhotonVoiceId;
                }

                if (sharing.ShouldSerializePhotonAvatarPrefabName())
                {
                    var prefabObject = Resources.Load(sharing.PhotonAvatarPrefabName) as GameObject;
                    if (prefabObject != null)
                    {
                        result.PhotonAvatarPrefab = prefabObject;
                    }
                }
            }

            return result;
        }
    }
}

