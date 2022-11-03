// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using App.Authentication;
using Microsoft.Identity.Client;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    //Storage account data using Azure Active Directory to get an Access Token. Access Tokens are the most secure option for development, test and production environments
    public class ADStorageAccountData : BaseStorageAccountData
    {
        private string storageAccountName;
        private string storageModelContainer;
        private bool modelPathByUsername;
        private string appId;
        private string tenantID;
        private string redirectURI;
        private string authority;

        public ADStorageAccountData(
            string storageAccountName, 
            string storageModelContainer,
            bool modelPathByUsername,
            string appId,
            string authority,
            string tenantID,
            string redirectURI)
        {
            this.storageAccountName = storageAccountName;
            this.storageModelContainer = storageModelContainer;
            this.modelPathByUsername = modelPathByUsername;
            this.appId = appId;
            this.tenantID = tenantID;
            this.redirectURI = redirectURI;
            this.authority = authority;
        }

        public override string StorageAccountName => storageAccountName;

        public override string DefaultContainer => storageModelContainer;
        
        public override bool ModelPathByUsername => modelPathByUsername;

        public override AuthenticationType AuthType => AuthenticationType.AccessToken;

        public override async Task<string> GetAuthData()
        {
            var authResult = await AADAuth.TryLogin(
                appId, 
                AADAuth.Scope.Storage,
                SelectAccount, 
                ExecuteOnUnityThread.ApplicationToken,
                authority,
                tenantID, 
                redirectURI);

            if (authResult != null)
                return authResult.AccessToken;
            else
                return string.Empty;
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

        public override bool IsValid()
        {
            return !string.IsNullOrEmpty(storageAccountName) && !string.IsNullOrEmpty(storageModelContainer) && !string.IsNullOrEmpty(appId);
        }
    }
}