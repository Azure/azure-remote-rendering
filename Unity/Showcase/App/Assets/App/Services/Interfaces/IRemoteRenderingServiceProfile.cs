// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using System;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    public interface IRemoteRenderingServiceProfile
    {
        AuthenticationType AuthType { get; }
        float MaxLeaseTime { get; set; }
        TimeSpan MaxLeaseTimespan { get; set; }
        string PreferredDomain { get; set; }
        string UnsafeSizeOverride { get; set; }
        RenderingSessionVmSize Size { get; set; }
        string SessionOverride { get; set; }
        bool AutoReconnect { get; set; }
        float AutoReconnectRate { get; set; }
        bool AutoRenewLease { get; set; }
        BaseStorageAccountData StorageAccountData { get; }
        bool ValidateProfile(out string validateMessages);
        Task<RemoteRenderingClient> GetClient(string domain);
    }
}