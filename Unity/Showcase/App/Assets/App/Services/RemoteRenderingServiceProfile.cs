// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using App.Authentication;
using Microsoft.Azure.RemoteRendering;
using Microsoft.Identity.Client;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    [MixedRealityServiceProfile(typeof(IRemoteRenderingService))]
    [CreateAssetMenu(fileName = "RemoteRenderingServiceProfile", menuName = "MixedRealityToolkit/RemoteRenderingService Configuration Production Profile")]
    public class RemoteRenderingServiceProfile : BaseRemoteRenderingServiceProfile
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

        [Tooltip("The default Azure remote rendering account domains. The first entry is the preferred domain. Optional if 'arr.account.xml' has been created and placed in the 'StreamingAssets' directory.")]
        public string[] accountDomains = { "westus2.mixedreality.azure.com", "eastus.mixedreality.azure.com", "westeurope.mixedreality.azure.com", "southeastasia.mixedreality.azure.com" };
        public override string[] AccountDomains { get => accountDomains; set => accountDomains = value; }

        [Tooltip("The default labels for the Azure remote rendering account domains.")]
        public string[] accountDomainLabels = { "West US 2", "East US", "West Europe", "Southeast Asia" };
        public override string[] AccountDomainLabels { get => accountDomainLabels; set => accountDomainLabels = value; }

        [Tooltip("The default Azure remote rendering account id to use. Optional if 'arr.account.xml' has been created and placed in the 'StreamingAssets' directory.")]
        public string AccountId = Guid.Empty.ToString();

        [Tooltip("The default Azure remote rendering account's location. Optional if 'arr.account.xml' has been created and placed in the 'StreamingAssets' directory.")]
        public string AccountAuthenticationDomain = string.Empty;        

        [Tooltip("The Azure Active Directory Application ID to use. Optional if 'arr.account.xml' has been created and placed in the 'StreamingAssets' directory.")]
        public string AppId = string.Empty;

        [Tooltip("The Tenant ID to use, if empty 'common' will be used. Optional if 'arr.account.xml' has been created and placed in the 'StreamingAssets' directory.")]
        public string TenantId = string.Empty;

        [Tooltip("The Redirect URI to use, if empty 'localhost' will be used. Optional if 'arr.account.xml' has been created and placed in the 'StreamingAssets' directory.")]
        public string RedirectURI = string.Empty;

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
        
        public override BaseStorageAccountData StorageAccountData
        {
            get => new ADStorageAccountData(storageAccountName, storageModelContainer, StorageModelPathByUsername, AppId, TenantId, RedirectURI);
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
        /// Get the preferred account domain.
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

                if (AccountDomains == null || AccountDomains.Length == 0)
                {
                    return string.Empty;
                }

                _preferredDomain = AccountDomains[0];
                return _preferredDomain;
            }

            set
            {
                if (_preferredDomain != value)
                {
                    if (string.IsNullOrEmpty(value) && AccountDomains != null && AccountDomains.Length > 0)
                    {
                        value = AccountDomains[0];
                    }
                    else if (AccountDomains == null)
                    {
                        Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}", "'AccountDomains' is null.");
                    }
                    else if (Array.IndexOf(AccountDomains, value) < 0)
                    {
                        Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}", $"'AccountDomains' doesn't contain the preferred domain, '{value}'.");
                    }

                    _preferredDomain = value;
                }
            }
        }

        public override AuthenticationType AuthType => AuthenticationType.AccessToken;

        public override async Task<AzureFrontend> GetFrontend(string domain)
        {
            var authResult = await AADAuth.TryLogin(AppId, AADAuth.Scope.ARR, SelectAccount, ExecuteOnUnityThread.ApplicationToken, TenantId, RedirectURI);
            string accessToken = string.Empty;
            if (authResult != null)
                accessToken = authResult.AccessToken;
            else
                throw new Exception("Azure Active Directory authentication failed!");

            return new AzureFrontend(new AzureFrontendAccountInfo()
            {
                AccessToken = string.Empty, //STS Access token
                AccountDomain = domain.Trim(),
                AccountId = AccountId.Trim(),
                AccountAuthenticationDomain = AccountAuthenticationDomain.Trim(),
                AccountKey = string.Empty,
                AuthenticationToken = accessToken, //Active Directory Access Token
            });
        }

        public async Task<IAccount> SelectAccount(IEnumerable<IAccount> availableAccounts)
        {
            var selectedAccount = availableAccounts.First();
            if (await CachedCredentialsDialogController.CachedCredentialNeedsConfirmation(selectedAccount))
            {
                return selectedAccount;
            }
            else
            {
                return null;
            }
        }

        public override bool ValidateProfile(out string validateMessages)
        {
            if (string.IsNullOrEmpty(AccountId))
            {
                validateMessages = "Azure Remote Rendering account id hasn't been specified. Please check 'RemoteRenderingService' MRTK extension configuration.";
                return false;
            }
            else if (string.IsNullOrEmpty(AccountAuthenticationDomain))
            {
                validateMessages = "Azure Remote Rendering account authentication domain hasn't been specified. Please check 'RemoteRenderingService' MRTK extension configuration.";
                return false;
            }            
            else if (string.IsNullOrEmpty(AppId))
            {
                validateMessages = "Azure Active Directory Application id hasn't been specified. Please check 'RemoteRenderingService' MRTK extension configuration.";
                return false;
            }
            else if (string.IsNullOrEmpty(PreferredDomain))
            {
                validateMessages = "Azure Remote Rendering account domain hasn't been specified. Please check 'RemoteRenderingService' MRTK extension configuration.";
                return false;
            }
            validateMessages = null;
            return true;
        }

        public override RemoteRenderingServiceProfileFileData CreateFileData()
        {
            RemoteRenderingServiceProfileFileData result = new RemoteRenderingServiceProfileFileData();

            var sessionData = result.Session = new RemoteRenderingServiceSessionData();
            sessionData.MaxLeaseTime = MaxLeaseTime;
            sessionData.AutoRenewLease = AutoRenewLease;
            sessionData.AutoReconnect = AutoReconnect;
            sessionData.AutoReconnectRate = AutoReconnectRate;
            sessionData.Size = Size;
            sessionData.UnsafeSizeOverride = UnsafeSizeOverride;
            sessionData.SessionOverride = SessionOverride;

            if (AccountDomains?.Length > 0 ||
                AccountDomainLabels?.Length > 0 ||
                !string.IsNullOrEmpty(AccountId) ||
                !string.IsNullOrEmpty(AccountAuthenticationDomain) ||
                !string.IsNullOrEmpty(AppId))
            {
                var accountData = result.Account = new RemoteRenderingServiceAccountData();
                accountData.AccountDomains = AccountDomains;
                accountData.AccountDomainLabels = AccountDomainLabels;
                accountData.AccountId = AccountId;
                accountData.AccountAuthenticationDomain = AccountAuthenticationDomain;
                accountData.AppId = AppId;
                accountData.TenantId = TenantId;
            }

            if (!string.IsNullOrEmpty(StorageAccountName) ||
                !string.IsNullOrEmpty(StorageModelContainer))
            {
                var storageData = result.Storage = new RemoteRenderingServiceStorageAccountData();
                storageData.StorageAccountName = StorageAccountName;
                storageData.StorageModelContainer = StorageModelContainer;
            }

            return result;
        }
    }
}
