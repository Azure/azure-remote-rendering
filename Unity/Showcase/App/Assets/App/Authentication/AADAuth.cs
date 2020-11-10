// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace App.Authentication
{
    public static class AADAuth
    {
        static string authority => "https://login.microsoftonline.com/";

        const string defaultTenant = "common";

        const string defaultRedirect = "http://localhost";

        static string[] arrScopes = new string[] { "https://sts.mixedreality.azure.com/mixedreality.signin" };

        static string[] storageScopes = new string[] { "https://storage.azure.com/user_impersonation" };
        
        private static IAccount selectedAccount;
        public static IAccount SelectedAccount => selectedAccount;

        public static void PrepareOnMainThread()
        {
            //Nothing needs to be done here, just need to wake up this static class on the main thread
            AADTokenCache.PrepareOnMainThread();
        }

        public enum Scope
        {
            ARR,
            Storage
        }

        static IPublicClientApplication clientApp;
        static IPublicClientApplication GetClientApp(string applicationClientId, string tenantID, string redirectURI)
        {
            if (string.IsNullOrEmpty(tenantID))
            {
                tenantID = defaultTenant;
            }
            if (string.IsNullOrEmpty(redirectURI))
            {
                redirectURI = defaultRedirect;
            }
            if (clientApp == null)
            {
#if UNITY_EDITOR
                redirectURI = defaultRedirect;
#endif
                clientApp = PublicClientApplicationBuilder.Create(applicationClientId).WithAuthority(authority + tenantID).WithRedirectUri(redirectURI).Build();
                AADTokenCache.EnableSerialization(clientApp.UserTokenCache);
            }
            return clientApp;
        }

        public static async Task<AuthenticationResult> TryLogin(string applicationClientId, Scope scope, Func<IEnumerable<IAccount>, Task<IAccount>> selectAccount, CancellationToken cancelToken, string tenantID = defaultTenant, string redirectURI = defaultRedirect)
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

            var clientApplication = GetClientApp(applicationClientId, tenantID, redirectURI);
            AuthenticationResult result = null;

            try
            {
                var accounts = await clientApplication.GetAccountsAsync();
                if (accounts.Any() && selectedAccount == null)
                {
                    selectedAccount = await selectAccount(accounts);

                    //Clean up any accounts not selected (all accounts if none selected)
                    foreach (var account in accounts)
                    {
                        if (account == selectedAccount)
                            continue;
                        await clientApplication.RemoveAsync(account);
                    }
                }

                if (selectedAccount != null)
                {
                    result = await clientApplication.AcquireTokenSilent(PrimaryScopes, selectedAccount).ExecuteAsync();

                    return result;
                }
                else
                {
                    try
                    {
#if UNITY_EDITOR
                    var options = new SystemWebViewOptions()
                    {
                        HtmlMessageError = "<p> An error occured: {0}. Details {1} </p>",
                        HtmlMessageSuccess = "<p> Success! You may now close this browser. </p>"
                    };
                    result = await clientApplication.AcquireTokenInteractive(PrimaryScopes).WithExtraScopesToConsent(ExtraScopes).WithUseEmbeddedWebView(false).WithSystemWebViewOptions(options).ExecuteAsync(cancelToken);
#else
                        result = await clientApplication.AcquireTokenInteractive(PrimaryScopes).WithExtraScopesToConsent(ExtraScopes).WithUseEmbeddedWebView(true).ExecuteAsync(cancelToken);
#endif
                    }
                    catch (MsalUiRequiredException ex)
                    {
                        Debug.LogError("MsalUiRequiredException");
                        Debug.LogException(ex);
                    }
                    catch (MsalServiceException ex)
                    {
                        Debug.LogError("MsalServiceException");
                        Debug.LogException(ex);
                    }
                    catch (MsalClientException ex)
                    {
                        Debug.LogError("MsalClientException");
                        Debug.LogException(ex);
                        // Mitigation: Use interactive authentication
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("Exception");
                        Debug.LogException(ex);
                    }

                    selectedAccount = result.Account;

                    return result;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);

                Debug.LogError("Exception using cached credentials, clearing cache");
                //Clear the cache file if there's an exception using the cache
                if (File.Exists(AADTokenCache.CacheFilePath))
                    File.Delete(AADTokenCache.CacheFilePath);
            }
            
            return null;
        }
    }
}