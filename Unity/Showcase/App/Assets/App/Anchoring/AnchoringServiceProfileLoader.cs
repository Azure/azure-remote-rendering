// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.


using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// Load in a service profile configurations from a deployed appx file or the app's local state directory
    /// </summary>
    public class AnchoringServiceProfileLoader
    {
        /// <summary>
        /// Attempt to load the profile from the Override File Path. 
        /// </summary>
        /// <param name="fallback">The fallback data to use if no file is found.</param>
        /// <returns>The loaded configuration</returns>
        public static async Task<AnchoringServiceProfile> Load(AnchoringServiceProfile fallback = null)
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
        private static AnchoringServiceProfile CreateProfile(ServiceConfigurationFile.FileData fileData, AnchoringServiceProfile fallback)
        {
            AnchoringServiceProfile result;
            if (fallback == null)
            {
                result = ScriptableObject.CreateInstance<AnchoringServiceProfile>();
            }
            else
            {
                result = Object.Instantiate(fallback);
            }

            if (fileData == null)
            {
                return result;
            }

            var anchorSettings = fileData.Anchor;
            if (anchorSettings != null)
            {
                if (anchorSettings.ShouldSerializeAnchorAccountId() &&
                    anchorSettings.ShouldSerializeAnchorAccountKey())
                {
                    result.AnchorAccountId = anchorSettings.AnchorAccountId;
                    result.AnchorAccountKey = anchorSettings.AnchorAccountKey;
                }

                if (anchorSettings.ShouldSerializeAnchorAccountDomain())
                {
                    result.AnchorAccountDomain = anchorSettings.AnchorAccountDomain;
                }
            }

            return result;
        }
    }
}

