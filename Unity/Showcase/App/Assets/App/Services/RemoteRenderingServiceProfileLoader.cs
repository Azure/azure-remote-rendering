// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// Load in a service profile configurations from a deployed appx file or the app's local state directory
    /// </summary>
    public static partial class RemoteRenderingServiceProfileLoader
    {
        /// <summary>
        /// Attempt to load the profile from the Override File Path. 
        /// </summary>
        /// <param name="fallback"></param>
        public static async Task<BaseRemoteRenderingServiceProfile> Load(BaseRemoteRenderingServiceProfile fallback = null)
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

        private static BaseRemoteRenderingServiceProfile CreateProfile(ServiceConfigurationFile.FileData fileData, BaseRemoteRenderingServiceProfile fallback)
        {
            BaseRemoteRenderingServiceProfile result = null;
            if (fallback == null)
            {
                result = fallback = ScriptableObject.CreateInstance<RemoteRenderingServiceDevelopmentProfile>();
            }
            else
            {
                result = ScriptableObject.Instantiate(fallback);
            }

            if (fileData == null)
            {
                return result;
            }
            
            // If an account key and domain is provided, assume a 'development' profile, and ignore AAD authentication.
            bool useDevelopmentProfile = 
                fileData.Account == null || 
                (fileData.Account.ShouldSerializeAccountDomain() && fileData.Account.ShouldSerializeAccountKey());

            if (useDevelopmentProfile)
            {
                result = ScriptableObject.CreateInstance<RemoteRenderingServiceDevelopmentProfile>();
            }
            else
            {
                result = ScriptableObject.CreateInstance<RemoteRenderingServiceProfile>();
            }

            if (fileData.Session != null)
            {
                if (fileData.Session.ShouldSerializeSize())
                {
                    result.Size = fileData.Session.Size;
                }

                if (fileData.Session.ShouldSerializeSessionOverride())
                {
                    result.SessionOverride = fileData.Session.SessionOverride;
                }

                if (fileData.Session.ShouldSerializeUnsafeSizeOverride())
                {
                    result.UnsafeSizeOverride = fileData.Session.UnsafeSizeOverride;
                }

                if (fileData.Session.ShouldSerializeMaxLeaseTime())
                {
                    result.MaxLeaseTime = fileData.Session.MaxLeaseTime;
                }

                if (fileData.Session.ShouldSerializeAutoReconnectRate())
                {
                    result.AutoReconnectRate = fileData.Session.AutoReconnectRate;
                }

                result.AutoRenewLease = fileData.Session.AutoRenewLease;
                result.AutoReconnect = fileData.Session.AutoReconnect;
            }

            if (fileData.Account != null)
            {
                if (result is RemoteRenderingServiceDevelopmentProfile)
                {
                    var devResult = (RemoteRenderingServiceDevelopmentProfile)result;
                    // Copy all or nothing from remote rendering account credentials
                    if (fileData.Account.ShouldSerializeAccountId())
                    {
                        devResult.AccountId = fileData.Account.AccountId;
                        devResult.AccountKey = fileData.Account.AccountKey;
                        devResult.AccountDomain = fileData.Account.AccountDomain;
                    }

                    if (fileData.Account.ShouldSerializeRemoteRenderingDomains())
                    {
                        devResult.RemoteRenderingDomains = fileData.Account.RemoteRenderingDomains;
                    }


                    result = devResult;
                }
                else
                {
                    var relResult = (RemoteRenderingServiceProfile)result;
                    // Copy all or nothing from remote rendering account credentials
                    if (fileData.Account.ShouldSerializeAccountId() &&
                        fileData.Account.ShouldSerializeAppId())
                    {
                        relResult.AccountId = fileData.Account.AccountId;
                        relResult.AppId = fileData.Account.AppId;
                    }

                    if (fileData.Account.ShouldSerializeAuthority())
                    {
                        relResult.Authority = fileData.Account.Authority;
                    }

                    if (fileData.Account.ShouldSerializeTenantId())
                    {
                        relResult.TenantId = fileData.Account.TenantId;
                    }

                    if (fileData.Account.ShouldSerializeReplyUri())
                    {
                        relResult.RedirectURI = fileData.Account.ReplyUri;
                    }

                    if (fileData.Account.ShouldSerializeRemoteRenderingDomains())
                    {
                        relResult.RemoteRenderingDomains = fileData.Account.RemoteRenderingDomains;
                    }

                    if (fileData.Account.ShouldSerializeAccountDomain())
                    {
                        relResult.AccountDomain = fileData.Account.AccountDomain;
                    }

                    result = relResult;
                }
            }

            if (fileData.Storage != null)
            {
                if (result is RemoteRenderingServiceDevelopmentProfile)
                {
                    var devResult = (RemoteRenderingServiceDevelopmentProfile)result;
                    // Copy all or nothing from storage account credentials
                    if (fileData.Storage.ShouldSerializeStorageAccountName() &&
                    fileData.Storage.ShouldSerializeStorageAccountKey())
                    {
                        devResult.StorageAccountName = fileData.Storage.StorageAccountName;
                        devResult.StorageAccountKey = fileData.Storage.StorageAccountKey;
                    }

                    if (fileData.Storage.ShouldSerializeStorageModelContainer())
                    {
                        devResult.StorageModelContainer = fileData.Storage.StorageModelContainer;
                    }

                    result = devResult;
                }
                else
                {
                    var relResult = (RemoteRenderingServiceProfile)result;
                    // Only the account name is used in this scenario
                    if (fileData.Storage.ShouldSerializeStorageAccountName())
                    {
                        relResult.StorageAccountName = fileData.Storage.StorageAccountName;
                    }

                    if (fileData.Storage.ShouldSerializeStorageModelContainer())
                    {
                        relResult.StorageModelContainer = fileData.Storage.StorageModelContainer;
                    }

                    result = relResult;
                }
            }

            return result;
        }
    }
}
