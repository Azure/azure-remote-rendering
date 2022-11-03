// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    [CreateAssetMenu(fileName = "RemoteRenderingServiceDevelopmentProfile", menuName = "ARR Showcase/Configuration Profile/Remote Rendering Service/Development")]
    public class RemoteRenderingServiceDevelopmentProfile : BaseRemoteRenderingServiceProfile
    {
        [Header("Session Settings")]

        [Tooltip("The preferred session size.")]
        public RenderingSessionVmSize size = RenderingSessionVmSize.Standard;
        public override RenderingSessionVmSize Size
        {
            get => size;
            set => size = value;
        }

        [Tooltip("Either a session guid or a session host name. If specified, the app will attempt to connect to this session. If a session guid is used, the location must be set accordingly.")]
        public string sessionOverride = null;
        public override string SessionOverride
        {
            get => sessionOverride;
            set => sessionOverride = value;
        }

        [NonSerialized]
        [Tooltip("A size override to use instead of the enum value. This is unsafe, and should be avoided.")]
        public string unsafeSizeOverride = null;
        public override string UnsafeSizeOverride
        {
            get => unsafeSizeOverride;
            set => unsafeSizeOverride = value;
        }

        [Tooltip("The default lease time, in seconds, of the ARR session. If *auto renew lease* is false or the app is disconnected, the session will expire after this time.")]
        public float maxLeaseTime = 30 * 60;
        public override float MaxLeaseTime
        {
            get => maxLeaseTime;
            set => maxLeaseTime = value;
        }

        [Tooltip("If true and the app is connected, the app will attempt to extend the ARR session lease before it expires. ")]
        public bool autoRenewLease = true;
        public override bool AutoRenewLease
        {
            get => autoRenewLease;
            set => autoRenewLease = value;
        }

        [Tooltip("If true, the app will attempt to auto reconnect after a disconnection. ")]
        public bool autoReconnect = true;
        public override bool AutoReconnect
        {
            get => autoReconnect;
            set => autoReconnect = value;
        }

        [Tooltip("The rate, in seconds, in which the app will attempt to reconnect after a disconnection.")]
        public float autoReconnectRate = 15.0f;
        public override float AutoReconnectRate
        {
            get => autoReconnectRate;
            set => autoReconnectRate = value;
        }

        [Tooltip("If true, the model list will contain the default models, even when using an override file or a storage account.")]
        public bool alwaysIncludeDefaultModels = false;
        public override bool AlwaysIncludeDefaultModels
        {
            get => alwaysIncludeDefaultModels;
            set => alwaysIncludeDefaultModels = value;
        }

        [Header("Remote Rendering Account Settings")]

        [Tooltip("The default Azure remote rendering account region. The first entry is the preferred region. Optional if 'arr.account.xml' has been created and placed in the 'StreamingAssets' directory.")]
        public RemoteRenderingServiceRegion[] remoteRenderingDomains = RemoteRenderingServiceRegion.Defaults;
        public override RemoteRenderingServiceRegion[] RemoteRenderingDomains { get => remoteRenderingDomains; set => remoteRenderingDomains = value; }

        [Tooltip("The default Azure remote rendering account id to use. Optional if 'arr.account.xml' has been created and placed in the 'StreamingAssets' directory.")]
        public string AccountId = Guid.Empty.ToString();

        [Tooltip("The default Azure remote rendering account domain. Optional if 'arr.account.xml' has been created and placed in the 'StreamingAssets' directory.")]
        public string AccountDomain = string.Empty;

        [Tooltip("The default Azure remote rendering account key to use. Optional if 'arr.account.xml' has been created and placed in the 'StreamingAssets' directory.")]
        public string AccountKey = string.Empty;

        [Header("Storage Account Settings (Optional)")]

        [Tooltip("The default Azure storage account id to use.")]
        public string storageAccountName = string.Empty;
        public override string StorageAccountName { get => storageAccountName; set => storageAccountName = value; }

        [Tooltip("The default Azure storage container to read models from.")]
        public string storageModelContainer = string.Empty;
        public override string StorageModelContainer { get => storageModelContainer; set => storageModelContainer = value; }
        
        [Tooltip("If true, when searching for or uploading models, a base path with the user's username will be used.")]
        public bool storageModelPathByUsername = false;
        public override bool StorageModelPathByUsername { get => storageModelPathByUsername; set => storageModelPathByUsername = value; }

        [Tooltip("The default Azure storage account key to use.")]
        public string StorageAccountKey = string.Empty;

        public override BaseStorageAccountData StorageAccountData
        {
            get => new AKStorageAccountData(StorageAccountName, StorageModelContainer, StorageModelPathByUsername, StorageAccountKey);
        }

        /// <summary>
        /// The preferred max lease time of the renderer session, in seconds.
        /// </summary>
        public override TimeSpan MaxLeaseTimespan
        {
            get => TimeSpan.FromSeconds(MaxLeaseTime);
            set => MaxLeaseTime = (float)value.TotalSeconds;
        }

        /// <summary>
        /// Get the preferred remote rendering domain.
        /// </summary>
        private string _preferredDomain;
        public override string PreferredDomain
        {
            get
            {
                if (!string.IsNullOrEmpty(_preferredDomain))
                {
                    return _preferredDomain;
                }

                if (RemoteRenderingDomains == null || RemoteRenderingDomains.Length == 0)
                {
                    return string.Empty;
                }

                _preferredDomain = RemoteRenderingDomains[0].Domain;
                return _preferredDomain;
            }

            set
            {
                if (_preferredDomain != value)
                {
                    if (string.IsNullOrEmpty(value) && RemoteRenderingDomains != null && RemoteRenderingDomains.Length > 0)
                    {
                        value = RemoteRenderingDomains[0].Domain;
                    }
                    else if (RemoteRenderingDomains == null)
                    {
                        Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}", "'RemoteRenderingDomains' is null.");
                    }
                    else if (!RemoteRenderingDomains.Any(entry => entry.Domain == value))
                    {
                        Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}", $"'RemoteRenderingDomains' doesn't contain the preferred domain, '{value}'.");
                    }

                    _preferredDomain = value;
                }
            }
        }

        public override AuthenticationType AuthType => AuthenticationType.AccountKey;

        public override async Task<RemoteRenderingClient> GetClient(string domain)
        {
            return await Task.FromResult(new RemoteRenderingClient(new SessionConfiguration()
            {
                AccessToken = string.Empty,
                RemoteRenderingDomain = domain.Trim(),
                AccountId = AccountId.Trim(),
                AccountDomain = AccountDomain.Trim(),
                AccountKey = AccountKey.Trim(),
                AuthenticationToken = string.Empty,
            }));
        }

        public override bool ValidateProfile(out string validateMessages)
        {
            if (string.IsNullOrEmpty(AccountId))
            {
                validateMessages = "Azure Remote Rendering account id hasn't been specified. Please check 'RemoteRenderingService' MRTK extension configuration.";
                return false;
            }
            else if (string.IsNullOrEmpty(AccountDomain))
            {
                validateMessages = "Azure Remote Rendering account domain hasn't been specified. Please check 'RemoteRenderingService' MRTK extension configuration.";
                return false;
            }            
            else if (string.IsNullOrEmpty(AccountKey))
            {
                validateMessages = "Azure Remote Rendering account key hasn't been specified. Please check 'RemoteRenderingService' MRTK extension configuration.";
                return false;
            }
            else if (string.IsNullOrEmpty(PreferredDomain))
            {
                validateMessages = "Azure Remote Rendering domain hasn't been specified. Please check 'RemoteRenderingService' MRTK extension configuration.";
                return false;
            }
            validateMessages = null;
            return true;
        }
    }
}
