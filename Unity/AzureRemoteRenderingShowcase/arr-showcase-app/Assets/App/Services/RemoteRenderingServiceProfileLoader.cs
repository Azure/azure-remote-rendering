// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// Load in a service profile configurations from a deployed appx file or the app's local state directory
    /// </summary>
    public static class RemoteRenderingServiceProfileLoader
    {
        /// <summary>
        /// Get the default file path of the deployed account file.
        /// </summary>
        public static string DefaultDeployedFilePath
        {
            get
            {
#if !UNITY_EDITOR && WINDOWS_UWP
                return $"ms-appx:///Data/StreamingAssets/arr.account.xml";
#else
                return $"{Application.streamingAssetsPath}/arr.account.xml";
#endif
            }
        }

        /// <summary>
        /// Get the default file path of the override file.
        /// </summary>
        public static string DefaultOverrideFilePath
        {
            get
            {
                return $"{Application.persistentDataPath}/arr.overrides.xml";
            }
        }

        /// <summary>
        /// Attempt to load the profile from the Override File Path. 
        /// </summary>
        /// <param name="fallback"></param>
        /// <returns></returns>
        public static async Task<RemoteRenderingServiceProfile> Load(RemoteRenderingServiceProfile fallback = null)
        {
            // load in the installed file 
            RemoteRenderingServiceProfileFileData deployedFile = await TryLoadFromDeployedFile();
            fallback = CreateProfile(deployedFile, fallback);

            // load in overrides
            RemoteRenderingServiceProfileFileData overrideFile = await TryLoadFromOverrideFile();
            fallback = CreateProfile(overrideFile, fallback);

            return fallback;
        }

        /// <summary>
        /// Attempt to save the profile to the Override File Path. 
        /// </summary>
        /// <param name="fallback"></param>
        /// <returns></returns>
        public static async Task Save(RemoteRenderingServiceProfile data)
        {
            if (data == null) 
            {
                return;
            }

            try
            {
                await LocalStorageHelper.Save(DefaultOverrideFilePath, CreateFileData(data));
            }
            catch (Exception ex)
            {
                Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}",  $"Failed to save override file. Reason: {ex.Message}");
            }
        }

        private static RemoteRenderingServiceProfile CreateProfile(RemoteRenderingServiceProfileFileData fileData, RemoteRenderingServiceProfile fallback)
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

        private static RemoteRenderingServiceProfileFileData CreateFileData(RemoteRenderingServiceProfile profile)
        {
            bool destroyProfile = false;
            RemoteRenderingServiceProfileFileData result = new RemoteRenderingServiceProfileFileData();
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<RemoteRenderingServiceProfile>();
                destroyProfile = true;
            }

            var sessionData = result.Session = new RemoteRenderingServiceSessionData();
            sessionData.MaxLeaseTime = profile.MaxLeaseTime;
            sessionData.AutoRenewLease = profile.AutoRenewLease;
            sessionData.AutoReconnect = profile.AutoReconnect;
            sessionData.AutoReconnectRate = profile.AutoReconnectRate;
            sessionData.Size = profile.Size;
            sessionData.UnsafeSizeOverride = profile.UnsafeSizeOverride;
            sessionData.SessionOverride = profile.SessionOverride;

            if (profile.AccountDomains?.Length > 0 ||
                !string.IsNullOrEmpty(profile.AccountId) ||
                !string.IsNullOrEmpty(profile.AccountKey))
            {
                var accountData = result.Account = new RemoteRenderingServiceAccountData();
                accountData.AccountDomains = profile.AccountDomains;
                accountData.AccountId = profile.AccountId;
                accountData.AccountKey = profile.AccountKey;
            }

            if (!string.IsNullOrEmpty(profile.StorageAccountName) ||
                !string.IsNullOrEmpty(profile.StorageAccountKey) ||
                !string.IsNullOrEmpty(profile.StorageModelContainer))
            {
                var storageData = result.Storage = new RemoteRenderingServiceStorageAccountData();
                storageData.StorageAccountName = profile.StorageAccountName;
                storageData.StorageAccountKey = profile.StorageAccountKey;
                storageData.StorageModelContainer = profile.StorageModelContainer;
            }

            if (destroyProfile)
            {
                ScriptableObject.DestroyImmediate(profile);
            }

            return result;
        }

        private static async Task<RemoteRenderingServiceProfileFileData> TryLoadFromOverrideFile()
        {
            RemoteRenderingServiceProfileFileData fileData = null;
            try
            {
                fileData = await LocalStorageHelper.Load<RemoteRenderingServiceProfileFileData>(DefaultOverrideFilePath);
            }
            catch (Exception ex)
            {
                Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}",  $"Failed to load data from override file '{DefaultOverrideFilePath}'. Reason: {ex.Message}");
            }

            return fileData;
        }

        private static async Task<RemoteRenderingServiceProfileFileData> TryLoadFromDeployedFile()
        {
            RemoteRenderingServiceProfileFileData fileData = null;
            try
            {
                fileData = await LocalStorageHelper.Load<RemoteRenderingServiceProfileFileData>(DefaultDeployedFilePath);
            }
            catch (Exception ex)
            {
                Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}",  $"Failed to load data from account file '{DefaultDeployedFilePath}'. Reason: {ex.Message}");
            }

            return fileData;
        }

        /// <summary>
        /// The file data class.
        /// </summary>
        [Serializable]
        [XmlRoot(ElementName = "Configuration")]
        public class RemoteRenderingServiceProfileFileData
        {
            public RemoteRenderingServiceAccountData Account;
            public RemoteRenderingServiceStorageAccountData Storage;
            public RemoteRenderingServiceSessionData Session;

            public bool ShouldSerializeAccount()
            {
                return Account != null &&
                    (Account.ShouldSerializeAccountDomains() ||
                     Account.ShouldSerializeAccountId() ||
                     Account.ShouldSerializeAccountKey());
            }

            public bool ShouldSerializeStorage()
            {
                return Storage != null &&
                    (!string.IsNullOrEmpty(Storage.StorageAccountName) ||
                     !string.IsNullOrEmpty(Storage.StorageAccountKey) ||
                     !string.IsNullOrEmpty(Storage.StorageModelContainer));
            }
        }

        [Serializable]
        public class RemoteRenderingServiceSessionData
        {
            [Tooltip("The preferred session size.")]
            public RenderingSessionVmSize Size = RenderingSessionVmSize.None;

            [Tooltip("Either a session guid or a session host name. If specified, the app will attempt to connect to this session. If a session guid is used, the location must be set accordingly.")]
            public string SessionOverride = null;

            [Tooltip("A size override to use instead of the enum value. This is unsafe, and should be avoided.")]
            public string UnsafeSizeOverride = null;

            [Tooltip("The default lease time, in seconds, of the ARR session. If *auto renew lease* is false or the app is disconnected, the session will expire after this time.")]
            public float MaxLeaseTime = 30 * 60;

            [Tooltip("If true and the app is connected, the app will attempt to extend the ARR session lease before it expires. ")]
            public bool AutoRenewLease = true;

            [Tooltip("If true, the app will attempt to auto reconnect after a disconnection. ")]
            public bool AutoReconnect = true;

            [Tooltip("The rate, in seconds, in which the app will attempt to reconnect after a disconnection.")]
            public float AutoReconnectRate = 15.0f;

            public bool ShouldSerializeSize() { return Size != RenderingSessionVmSize.None; }

            public bool ShouldSerializeSessionOverride() { return !string.IsNullOrEmpty(SessionOverride); }

            public bool ShouldSerializeUnsafeSizeOverride() { return !string.IsNullOrEmpty(UnsafeSizeOverride); }

            public bool ShouldSerializeMaxLeaseTime() { return MaxLeaseTime > 0; }

            public bool ShouldSerializeAutoReconnectRate() { return AutoReconnectRate > 0; }
        }

        [Serializable]
        public class RemoteRenderingServiceAccountData
        {
            [Tooltip("The list Azure remote rendering account domain supported by this account. The first entry is the perferred one.")]
            [XmlArrayItem("AccountDomain")]
            public string[] AccountDomains;

            [Tooltip("The default Azure remote rendering account id to use.")]
            public string AccountId;

            [Tooltip("The default Azure remote rendering account key to use.")]
            public string AccountKey;

            public bool ShouldSerializeAccountDomains() { return AccountDomains != null && AccountDomains.Length > 0; }

            public bool ShouldSerializeAccountId()
            {
                Guid id = Guid.Empty;
                return Guid.TryParse(AccountId, out id) && id != Guid.Empty; 
            }

            public bool ShouldSerializeAccountKey() { return !string.IsNullOrEmpty(AccountKey); }
        }

        [Serializable]
        public class RemoteRenderingServiceStorageAccountData
        {
            [Tooltip("The default Azure storage account id to use.")]
            public string StorageAccountName;

            [Tooltip("The default Azure storage account key to use.")]
            public string StorageAccountKey;

            [Tooltip("The default Azure storage container to read models from.")]
            public string StorageModelContainer;

            public bool ShouldSerializeStorageAccountName() { return !string.IsNullOrEmpty(StorageAccountName); }

            public bool ShouldSerializeStorageAccountKey() { return !string.IsNullOrEmpty(StorageAccountKey); }

            public bool ShouldSerializeStorageModelContainer() { return !string.IsNullOrEmpty(StorageModelContainer); }
        }
    }
}
