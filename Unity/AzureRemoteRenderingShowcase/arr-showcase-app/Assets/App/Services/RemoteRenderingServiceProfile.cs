// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using System;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
	[MixedRealityServiceProfile(typeof(IRemoteRenderingService))]
	[CreateAssetMenu(fileName = "RemoteRenderingServiceProfile", menuName = "MixedRealityToolkit/RemoteRenderingService Configuration Profile")]
	public class RemoteRenderingServiceProfile : BaseMixedRealityProfile
    {
        [Header("Session Settings")]

        [Tooltip("The preferred session size.")]
        public RenderingSessionVmSize Size = RenderingSessionVmSize.Standard;

        [Tooltip("Either a session guid or a session host name. If specified, the app will attempt to connect to this session. If a session guid is used, the location must be set accordingly.")]
        public string SessionOverride = null;

        [NonSerialized]
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

        [Tooltip("If true, the model list will contain the default models, even when using an override file or a storage account.")]
        public bool AlwaysIncludeDefaultModels = false;

        [Header("Remote Rendering Account Settings")]

        [Tooltip("The default Azure remote rendering account domains. The first entry is the preferred domain. Optional if 'arr.account.xml' has been created and placed in the 'StreamingAssets' directory.")]
        public string[] AccountDomains = { "westus2.mixedreality.azure.com", "eastus.mixedreality.azure.com", "westeurope.mixedreality.azure.com", "southeastasia.mixedreality.azure.com" };

        [Tooltip("The default labels for the Azure remote rendering account domains.")]
        public string[] AccountDomainLabels = { "West US 2", "East US", "West Europe", "Southeast Asia" };

        [Tooltip("The default Azure remote rendering account id to use. Optional if 'arr.account.xml' has been created and placed in the 'StreamingAssets' directory.")]
        public string AccountId = Guid.Empty.ToString();

        [Tooltip("The default Azure remote rendering account key to use. Optional if 'arr.account.xml' has been created and placed in the 'StreamingAssets' directory.")]
        public string AccountKey = string.Empty;

        [Header("Storage Account Settings (Optional)")]

        [Tooltip("The default Azure storage account id to use.")]
        public string StorageAccountName = string.Empty;

        [Tooltip("The default Azure storage account key to use.")]
        public string StorageAccountKey = string.Empty;

        [Tooltip("The default Azure storage container to read models from.")]
        public string StorageModelContainer = string.Empty;

        /// <summary>
        /// The preferred max lease time of the renderer session, in seconds.
        /// </summary>
        public TimeSpan MaxLeaseTimespan => TimeSpan.FromSeconds(MaxLeaseTime);

        /// <summary>
        /// Get the preferred account domain.
        /// </summary>
        private string _preferredDomain;
        public string PreferredDomain
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
                        Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  "'AccountDomains' is null.");
                    }
                    else if (Array.IndexOf(AccountDomains, value) < 0)
                    {
                        Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}",  $"'AccountDomains' doesn't contain the preferred domain, '{value}'.");
                    }

                    _preferredDomain = value;
                }
            }
        }
    }
}
