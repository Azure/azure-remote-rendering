// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    public interface IRemoteRenderingStorageAccountData
    {
        string StorageAccountName { get; }
        string DefaultContainer { get; }
        bool ModelPathByUsername { get; }
        AuthenticationType AuthType { get; }
        bool IsValid();
        Task<string> GetAuthData();
    }
}
