// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace App.Authentication
{
    public static class AADAuth
    {
        private const string defaultRedirect = "http://localhost";

        private static readonly string defaultAuthority = AadAuthorityAudience.AzureAdAndPersonalMicrosoftAccount.ToString();

        private static readonly string[] arrScopes = new string[] { "https://sts.mixedreality.azure.com/mixedreality.signin" };

        private static readonly string[] storageScopes = new string[] { "https://storage.azure.com/user_impersonation" };
        
        private static IAccount selectedAccount;

        private static IPublicClientApplication clientApp;

        private static readonly LogHelper log = new LogHelper(nameof(AADAuth));

        public static IAccount SelectedAccount => selectedAccount;

        public static void PrepareOnMainThread()
        {
            // Nothing needs to be done here, just need to wake up this static class on the main thread
            AADTokenCache.PrepareOnMainThread();
        }

        public enum Scope
        {
            ARR,
            Storage
        }

        private static IPublicClientApplication GetClientApp(string applicationClientId, string authority, string tenantID, string redirectURI)
        {
            if (string.IsNullOrEmpty(authority))
            {
                authority = defaultAuthority;
            }

            if (string.IsNullOrEmpty(redirectURI))
            {
                redirectURI = defaultRedirect;
            }

            if (clientApp == null || clientApp.AppConfig.ClientId != applicationClientId)
            {
#if UNITY_EDITOR
                redirectURI = defaultRedirect;
#endif
                var builder = PublicClientApplicationBuilder.Create(applicationClientId).WithRedirectUri(redirectURI);

                if (!string.IsNullOrWhiteSpace(tenantID))
                {
                    builder = builder.WithTenantId(tenantID);
                }
                else 
                {
                    AadAuthorityAudience audience;
                    if (Enum.TryParse(authority, out audience))
                    {
                        builder = builder.WithAuthority(audience);
                    }
                    else
                    {
                        builder = builder.WithAuthority(authority);
                    }
                }

                clientApp = builder.Build();

                AADTokenCache.EnableSerialization(clientApp.UserTokenCache);
            }
            return clientApp;
        }

        public static async Task<AuthenticationResult> TryLogin(
            string applicationClientId, 
            Scope scope, 
            Func<IEnumerable<IAccount>, Task<IAccount>> selectAccount,
            CancellationToken cancelToken,
            string authority, 
            string tenantID, 
            string redirectURI)
        {
            string[] PrimaryScopes = null;
            string[] ExtraScopes = null;
            switch (scope)
            {
                case Scope.ARR:
                    PrimaryScopes = arrScopes;
                    ExtraScopes = storageScopes;
                    break;
                case Scope.Storage:
                    PrimaryScopes = storageScopes;
                    ExtraScopes = arrScopes;
                    break;
            }

            AuthenticationResult result = null;
            var clientApplication = GetClientApp(applicationClientId, authority, tenantID, redirectURI);
            await SelectAccount(clientApplication, selectAccount);

            if (selectedAccount != null)
            {
                try
                {
                    result = await clientApplication.AcquireTokenSilent(PrimaryScopes, selectedAccount).ExecuteAsync();
                }
                catch (MsalUiRequiredException ex)
                {
                    log.LogWarning("Failed to acquire token silently. Attempting to display interactive sign-in UI. Exception: {0}", ex);
                }
            }

            if (result == null)
            {
                result = await AcquireTokenInteractive(clientApplication, PrimaryScopes, ExtraScopes, cancelToken);
            }

            return result;
        }

        private static async Task<AuthenticationResult> AcquireTokenInteractive(IPublicClientApplication clientApplication, string[] PrimaryScopes, string[] ExtraScopes, CancellationToken cancelToken)
        {
            AuthenticationResult result = null;
            try
            {
#if UNITY_EDITOR
                var options = new SystemWebViewOptions()
                {
                    HtmlMessageError = "<p> An error occurred: {0}. Details {1} </p>",
                    HtmlMessageSuccess = "<p> Success! You may now close this browser. </p>"
                };
                result = await clientApplication.
                    AcquireTokenInteractive(PrimaryScopes).
                    WithExtraScopesToConsent(ExtraScopes).
                    WithUseEmbeddedWebView(false).
                    WithSystemWebViewOptions(options).
                    ExecuteAsync(cancelToken);
#else
                result = await clientApplication.
                    AcquireTokenInteractive(PrimaryScopes).
                    WithExtraScopesToConsent(ExtraScopes).
                    WithUseEmbeddedWebView(true).
                    ExecuteAsync(cancelToken);
#endif
            }
            catch (MsalUiRequiredException ex)
            {
                log.LogError("Failed to acquire token. MsalUiRequiredException: {0}", ex);
            }
            catch (MsalServiceException ex)
            {
                log.LogError("Failed to acquire token. MsalServiceException: {0}", ex);
            }
            catch (MsalClientException ex)
            {
                log.LogError("Failed to acquire token. MsalClientException: {0}", ex);
                // Mitigation: Use interactive authentication
            }
            catch (Exception ex)
            {
                log.LogError("Failed to acquire token. Exception: {0}", ex);
            }

            selectedAccount = result?.Account;
            return result;
        }

        private static async Task SelectAccount(IPublicClientApplication clientApplication, Func<IEnumerable<IAccount>, Task<IAccount>> selectAccount)
        {
            try
            {
                var accounts = await clientApplication.GetAccountsAsync();
                if (accounts.Any() && selectedAccount == null)
                {
                    selectedAccount = await selectAccount(accounts);

                    // Clean up any accounts not selected (all accounts if none selected)
                    foreach (var account in accounts)
                    {
                        if (account != selectedAccount)
                        {
                            await clientApplication.RemoveAsync(account);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                selectedAccount = null;
                log.LogWarning("Failed to use cached credentials, clearing cache. Exception: {0}", ex);

                // Clear the cache file if there's an exception using the cache
                if (File.Exists(AADTokenCache.CacheFilePath))
                {
                    File.Delete(AADTokenCache.CacheFilePath);
                }
            }
        }
    }
}