// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    public abstract class BaseStorageAccountData : IRemoteRenderingStorageAccountData
    {
        public abstract string StorageAccountName { get; }
        public abstract string DefaultContainer { get; }
        public abstract bool ModelPathByUsername { get; }
        public abstract AuthenticationType AuthType { get; }
        public abstract bool IsValid();
        public abstract Task<string> GetAuthData();
    }
}
