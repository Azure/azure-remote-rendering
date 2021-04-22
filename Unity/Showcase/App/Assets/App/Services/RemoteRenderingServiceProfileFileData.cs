// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using System;
using System.Xml.Serialization;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// The file data class.
    /// </summary>
    [Serializable]
    [XmlRoot(ElementName = "Configuration")]
    public class RemoteRenderingServiceProfileFileData
    {
        public bool IsDevelopmentProfileData;

        public RemoteRenderingServiceSessionData Session;

        public RemoteRenderingServiceAccountData Account;
        public RemoteRenderingServiceStorageAccountData Storage;
            
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
        [Tooltip("The list of Azure remote rendering domains supported by this account. The first entry is the preferred one.")]
        [XmlArrayItem("RemoteRenderingDomain")]
        public string[] RemoteRenderingDomains;

        [Tooltip("The default labels for the Azure remote rendering domains.")]
        [XmlArrayItem("RemoteRenderingDomainLabel")]
        public string[] RemoteRenderingDomainLabels = { "West US 2", "West Europe", "East US", "Southeast Asia" };

        [Tooltip("The default Azure remote rendering account id to use.")]
        public string AccountId;

        [Tooltip("The domain of the Azure remote rendering account.")]
        public string AccountDomain;

        //Used in development
        [Tooltip("The development Azure remote rendering account key to use.")]
        public string AccountKey;

        //Used in production
        [Tooltip("The Azure Active Directory Application ID to use.")]
        public string AppId;

        //Used in production
        [Tooltip("The Tenant to authenticate against.")]
        public string TenantId;

        public bool ShouldSerializeRemoteRenderingDomains() { return RemoteRenderingDomains != null && RemoteRenderingDomains.Length > 0; }

        public bool ShouldSerializeAccountId()
        {
            Guid id = Guid.Empty;
            return Guid.TryParse(AccountId, out id) && id != Guid.Empty;
        }

        public bool ShouldSerializeAccountDomain() { return !string.IsNullOrEmpty(AccountDomain); }

        public bool ShouldSerializeTenantId()
        {
            Guid id = Guid.Empty;
            return Guid.TryParse(TenantId, out id) && id != Guid.Empty;
        }

        public bool ShouldSerializeAccountKey() { return !string.IsNullOrEmpty(AccountKey); }

        public bool ShouldSerializeAppId() 
        {
            Guid id = Guid.Empty;
            return Guid.TryParse(AppId, out id) && id != Guid.Empty;
        }
    }

    [Serializable]
    public class RemoteRenderingServiceStorageAccountData
    {
        [Tooltip("The default Azure storage account id to use.")]
        public string StorageAccountName;

        [Tooltip("The default Azure storage container to read models from.")]
        public string StorageModelContainer;

        [Tooltip("The default Azure storage account key to use.")]
        public string StorageAccountKey;

        public bool ShouldSerializeStorageAccountName() { return !string.IsNullOrEmpty(StorageAccountName); }

        public bool ShouldSerializeStorageAccountKey() { return !string.IsNullOrEmpty(StorageAccountKey); }

        public bool ShouldSerializeStorageModelContainer() { return !string.IsNullOrEmpty(StorageModelContainer); }
    }
}
