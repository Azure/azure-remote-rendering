// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// Load in a service profile configuration from a deployed appx file or the app's local state directory
    /// </summary>
    public static class RemoteRenderingServiceProfileLoader
    {
        /// <summary>
        /// Attempt to load the profile from the Override File Path. 
        /// </summary>
        /// <param name="fallback"></param>
        /// <returns></returns>
        public static async Task<RemoteRenderingServiceProfile> Load(RemoteRenderingServiceProfile fallback = null)
        {
            ServiceConfigurationFile file = new ServiceConfigurationFile();

            // load in the installed file 
            ServiceConfigurationFile.FileData deployedFile = await file.LoadDeployed();
            fallback = CreateProfile(deployedFile, fallback);

            // load in overrides
            ServiceConfigurationFile.FileData overrideFile = await file.LoadOverrides();
            fallback = CreateProfile(overrideFile, fallback);

            return fallback;
        }

        private static RemoteRenderingServiceProfile CreateProfile(ServiceConfigurationFile.FileData fileData, RemoteRenderingServiceProfile fallback)
        {
            RemoteRenderingServiceProfile result = null;
            if (fallback == null)
            {
                result = fallback = ScriptableObject.CreateInstance<RemoteRenderingServiceProfile>();
            }
            else
            {
                result = ScriptableObject.Instantiate(fallback);
            }

            if (fileData == null)
            {
                return result;
            }

            var sessionData = fileData.Session;
            if (sessionData != null)
            {
                if (sessionData.ShouldSerializeSize())
                {
                    result.Size = sessionData.Size;
                }

                if (sessionData.ShouldSerializeSessionOverride())
                {
                    result.SessionOverride = sessionData.SessionOverride;
                }

                if (sessionData.ShouldSerializeUnsafeSizeOverride())
                {
                    result.UnsafeSizeOverride = sessionData.UnsafeSizeOverride;
                }

                if (sessionData.ShouldSerializeMaxLeaseTime())
                {
                    result.MaxLeaseTime = sessionData.MaxLeaseTime;
                }

                if (sessionData.ShouldSerializeAutoReconnectRate())
                {
                    result.AutoReconnectRate = sessionData.AutoReconnectRate;
                }

                result.AutoRenewLease = sessionData.AutoRenewLease;
                result.AutoReconnect = sessionData.AutoReconnect;
            }

            var accountData = fileData.Account;
            if (accountData != null)
            {
                // Copy all or nothing from remote rendering account credentials
                if (accountData.ShouldSerializeAccountId() &&
                    accountData.ShouldSerializeAccountKey())
                {
                    result.AccountId = accountData.AccountId;
                    result.AccountKey = accountData.AccountKey;
                }

                if (accountData.ShouldSerializeAccountDomains())
                {
                    result.AccountDomains = accountData.AccountDomains;
                }

                if (accountData.ShouldSerializeAccountDomainLabels())
                {
                    result.AccountDomainLabels = accountData.AccountDomainLabels;
                }
            }

            var storageData = fileData.Storage;
            if (storageData != null)
            {
                // Copy all or nothing from storage account credentials
                if (storageData.ShouldSerializeStorageAccountName() &&
                    storageData.ShouldSerializeStorageAccountKey())
                {
                    result.StorageAccountName = storageData.StorageAccountName;
                    result.StorageAccountKey = storageData.StorageAccountKey;
                }

                if (storageData.ShouldSerializeStorageModelContainer())
                {
                    result.StorageModelContainer = storageData.StorageModelContainer;
                }
            }

            return result;
        }
    }
}
