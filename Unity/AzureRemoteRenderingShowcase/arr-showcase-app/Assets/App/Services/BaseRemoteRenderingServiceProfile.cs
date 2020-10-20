// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using System;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    public abstract class BaseRemoteRenderingServiceProfile : BaseMixedRealityProfile, IRemoteRenderingServiceProfile
    {
        public abstract float MaxLeaseTime { get; set; }
        public abstract TimeSpan MaxLeaseTimespan { get; set; }
        public abstract string PreferredDomain { get; set; }
        public abstract string UnsafeSizeOverride { get; set; }
        public abstract RenderingSessionVmSize Size { get; set; }
        public abstract string SessionOverride { get; set; }
        public abstract bool AutoReconnect { get; set; }
        public abstract float AutoReconnectRate { get; set; }
        public abstract bool AlwaysIncludeDefaultModels { get; set; }
        public abstract bool AutoRenewLease { get; set; }
        public abstract string StorageAccountName { get; set; }
        public abstract string StorageModelContainer { get; set; }
        public abstract bool StorageModelPathByUsername { get; set; }
        public abstract string[] AccountDomains { get; set; }
        public abstract string[] AccountDomainLabels { get; set; }
        public abstract BaseStorageAccountData StorageAccountData { get; }
        public abstract AuthenticationType AuthType { get; }
        public abstract RemoteRenderingServiceProfileFileData CreateFileData();
        public abstract Task<AzureFrontend> GetFrontend(string domain);
        public abstract bool ValidateProfile(out string validateMessages);
    }
}
