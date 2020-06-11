// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.MixedReality.Toolkit.UI;
using System;
using System.Threading.Tasks;
using System.Xml.Serialization;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    public class ServiceConfigurationFile
    {
        private static object _fileDataLock = new object();
        private static TaskCompletionSource<FileData> _mergedFileData = null;
        private static TaskCompletionSource<FileData> _deployedFileData = null;
        private static TaskCompletionSource<FileData> _overrideFileData = null;

        #region Public Properties
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
        #endregion Public Properties

        #region Public Functions
        public Task<FileData> LoadMerged()
        {
            StartLoad();
            return _mergedFileData.Task;
        }

        public Task<FileData> LoadOverrides()
        {
            StartLoad();
            return _overrideFileData.Task;
        }

        public Task<FileData> LoadDeployed()
        {
            StartLoad();
            return _deployedFileData.Task;
        }
        #endregion Public Functions

        #region Private Functions
        private static async void StartLoad()
        {
            lock (_fileDataLock)
            {
                if (_mergedFileData != null &&
                    _overrideFileData != null &&
                    _deployedFileData != null)
                {
                    return;
                }

                _mergedFileData = new TaskCompletionSource<FileData>();
                _overrideFileData = new TaskCompletionSource<FileData>();
                _deployedFileData = new TaskCompletionSource<FileData>();
            }

            Task<FileData> overrideFileTask = TryLoadFromOverrideFile();
            Task<FileData> deployedFileTask = TryLoadFromDeployedFile();

            try
            {
                await Task.WhenAll(overrideFileTask, deployedFileTask);
            }
            catch (Exception ex)
            {
                Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}", $"Failed to load service configuration files. Reason: {ex.Message}.");
            }

            FileData overrideFile = overrideFileTask.Result;
            FileData deployedFile = deployedFileTask.Result;
            FileData mergedFile = MergeFileData(overrideFile, deployedFile);

            _mergedFileData.TrySetResult(mergedFile);
            _overrideFileData.TrySetResult(overrideFile);
            _deployedFileData.TrySetResult(deployedFile);
        }

        private static FileData MergeFileData(FileData primary, FileData secondary)
        {
            FileData merged = new FileData();

            if (primary != null && primary.ShouldSerializeAccount())
            {
                merged.Account = primary.Account.Copy();
            }
            else if (secondary != null && secondary.ShouldSerializeAccount())
            {
                merged.Account = secondary.Account.Copy();
            }

            if (primary != null && primary.ShouldSerializeStorage())
            {
                merged.Storage = primary.Storage.Copy();
            }
            else if (secondary != null && secondary.ShouldSerializeStorage())
            {
                merged.Storage = secondary.Storage.Copy();
            }

            if (primary != null && primary.ShouldSerializeSession())
            {
                merged.Session = primary.Session.Copy();
            }
            else if (secondary != null && secondary.ShouldSerializeSession())
            {
                merged.Session = secondary.Session.Copy();
            }

            if (primary != null && primary.ShouldSerializeSharing())
            {
                merged.Sharing = primary.Sharing.Copy();
            }
            else if (secondary != null && secondary.ShouldSerializeSharing())
            {
                merged.Sharing = secondary.Sharing.Copy();
            }

            if (primary != null && primary.ShouldSerializeAnchor())
            {
                merged.Anchor = primary.Anchor.Copy();
            }
            else if (secondary != null && secondary.ShouldSerializeAnchor())
            {
                merged.Anchor = secondary.Anchor.Copy();
            }


            return merged;
        }

        private static async Task<FileData> TryLoadFromOverrideFile()
        {
            FileData fileData = default;
            try
            {
                fileData = await LocalStorageHelper.Load<FileData>(DefaultOverrideFilePath);
            }
            catch (Exception ex)
            {
                Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}", $"Failed to load data from override file '{DefaultOverrideFilePath}'. Reason: {ex.Message}.");
            }

            return fileData;
        }

        private static async Task<FileData> TryLoadFromDeployedFile()
        {
            FileData fileData = default;
            try
            {
                fileData = await LocalStorageHelper.Load<FileData>(DefaultDeployedFilePath);
            }
            catch (Exception ex)
            {
                Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}", $"Failed to load data from account file '{DefaultDeployedFilePath}'. Reason: {ex.Message}.");
            }

            return fileData;
        }
        #endregion Private Functions

        #region Public Classes
        /// <summary>
        /// The file data class.
        /// </summary>
        [Serializable]
        [XmlRoot(ElementName = "Configuration")]
        public class FileData
        {
            public RemoteRendingAccount Account;
            public StorageAccount Storage;
            public RemoteRendingSession Session;
            public SharingAccount Sharing;
            public AnchorAccount Anchor;

            public bool ShouldSerializeAccount()
            {
                return Account != null  &&
                    (Account.ShouldSerializeAccountDomains() ||
                     Account.ShouldSerializeAccountId() ||
                     Account.ShouldSerializeAccountKey());
            }

            public bool ShouldSerializeStorage()
            {
                return Storage != null &&
                    (Storage.ShouldSerializeStorageAccountName() ||
                    Storage.ShouldSerializeStorageAccountKey() ||
                    Storage.ShouldSerializeStorageModelContainer());
            }

            public bool ShouldSerializeSession()
            {
                return Session != null;
            }

            public bool ShouldSerializeSharing()
            {
                return Sharing != null &&
                    (Sharing.ShouldSerializePhotonRealtimeId());
            }

            public bool ShouldSerializeAnchor()
            {
                return Anchor != null &&
                    (Anchor.ShouldSerializeAnchorAccountId() ||
                    Anchor.ShouldSerializeAnchorAccountKey());
            }
        }

        [Serializable]
        public class RemoteRendingSession
        {
            public RemoteRendingSession Copy()
            {
                return new RemoteRendingSession()
                {
                    AutoReconnect = AutoReconnect,
                    AutoReconnectRate = AutoReconnectRate,
                    AutoRenewLease = AutoRenewLease,
                    MaxLeaseTime = MaxLeaseTime,
                    SessionOverride = SessionOverride,
                    Size = Size,
                    UnsafeSizeOverride = UnsafeSizeOverride
                };
            }

            [Tooltip("The preferred session size.")]
            public RenderingSessionVmSize Size;

            [Tooltip("Either a session guid or a session host name. If specified, the app will attempt to connect to this session. If a session guid is used, the location must be set accordingly.")]
            public string SessionOverride;

            [Tooltip("A size override to use instead of the enum value. This is unsafe, and should be avoided.")]
            public string UnsafeSizeOverride;

            [Tooltip("The default lease time, in seconds, of the ARR session. If *auto renew lease* is false or the app is disconnected, the session will expire after this time.")]
            public float MaxLeaseTime;

            [Tooltip("If true and the app is connected, the app will attempt to extend the ARR session lease before it expires. ")]
            public bool AutoRenewLease = true;

            [Tooltip("If true, the app will attempt to auto reconnect after a disconnection.")]
            public bool AutoReconnect = true;

            [Tooltip("The rate, in seconds, in which the app will attempt to reconnect after a disconnection.")]
            public float AutoReconnectRate;

            public bool ShouldSerializeSize() { return Size != RenderingSessionVmSize.None; }

            public bool ShouldSerializeSessionOverride() { return !string.IsNullOrEmpty(SessionOverride); }

            public bool ShouldSerializeUnsafeSizeOverride() { return !string.IsNullOrEmpty(UnsafeSizeOverride); }

            public bool ShouldSerializeMaxLeaseTime() { return MaxLeaseTime > 0; }

            public bool ShouldSerializeAutoReconnectRate() { return AutoReconnectRate > 0; }

            public bool ShouldSerializeAutoReconnect() { return !AutoReconnect; }

            public bool ShouldSerializeAutoRenewLease() { return !AutoRenewLease; }
        }

        [Serializable]
        public class RemoteRendingAccount
        {
            public RemoteRendingAccount Copy()
            {
                return new RemoteRendingAccount()
                {
                    AccountDomains = AccountDomains,
                    AccountId = AccountId,
                    AccountKey = AccountKey
                };
            }

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
        public class StorageAccount
        {
            public StorageAccount Copy()
            {
                return new StorageAccount()
                {
                    StorageAccountName = StorageAccountName,
                    StorageAccountKey = StorageAccountKey,
                    StorageModelContainer = StorageModelContainer
                };
            }

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

        public class SharingAccount
        {
            public SharingAccount Copy()
            {
                return new SharingAccount()
                {
                    PhotonRealtimeId = PhotonRealtimeId
                };
            }

            [Tooltip("The app id to use to initialize Photon's realtime service.")]
            public string PhotonRealtimeId;

            public bool ShouldSerializePhotonRealtimeId() { return !string.IsNullOrEmpty(PhotonRealtimeId); }
        }

        public class AnchorAccount
        {
            public AnchorAccount Copy()
            {
                return new AnchorAccount()
                {
                    AnchorAccountId = AnchorAccountId,
                    AnchorAccountKey = AnchorAccountKey
                };
            }

            [Tooltip("The account id to use for Azure Spatial Anchors")]
            public string AnchorAccountId;

            [Tooltip("The account key to use for Azure Spatial Anchors")]
            public string AnchorAccountKey;

            public bool ShouldSerializeAnchorAccountId()
            {
                Guid id = Guid.Empty;
                return Guid.TryParse(AnchorAccountId, out id) && id != Guid.Empty;
            }

            public bool ShouldSerializeAnchorAccountKey() { return !string.IsNullOrEmpty(AnchorAccountKey); }
        }
        #endregion Public Classes
    }
}

