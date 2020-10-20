// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    //Storage account data using an Account Key. Account Keys are useful for development and testing, but not considered good practice for production
    public class AKStorageAccountData : BaseStorageAccountData
    {
        public static string MODEL_PATH_BY_USERNAME_FOLDER = "development";
        
        private string storageAccountName;
        private string storageModelContainer;
        private bool modelPathByUsername;
        private string storageAccountKey;

        public AKStorageAccountData(string storageAccountName, string storageModelContainer, bool modelPathByUsername, string storageAccountKey)
        {
            this.storageAccountName = storageAccountName;
            this.storageModelContainer = storageModelContainer;
            this.modelPathByUsername = modelPathByUsername;
            this.storageAccountKey = storageAccountKey;
        }

        public override string StorageAccountName => storageAccountName;

        public override string DefaultContainer => storageModelContainer;
        
        public override bool ModelPathByUsername => modelPathByUsername;

        public override AuthenticationType AuthType => AuthenticationType.AccountKey;

        public override async Task<string> GetAuthData()
        {
            return await Task.FromResult(storageAccountKey);
        }

        public override bool IsValid()
        {
            return !string.IsNullOrEmpty(storageAccountName) && !string.IsNullOrEmpty(storageModelContainer) && !string.IsNullOrEmpty(storageAccountKey);
        }
    }
}