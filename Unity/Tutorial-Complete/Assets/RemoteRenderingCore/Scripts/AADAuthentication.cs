// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.Identity.Client;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class AADAuthentication : BaseARRAuthentication
{
    [SerializeField]
    private string accountDomain;
    public string AccountDomain
    {
        get => accountDomain.Trim();
        set => accountDomain = value;
    }

    [SerializeField]
    private string activeDirectoryApplicationClientID;
    public string ActiveDirectoryApplicationClientID
    {
        get => activeDirectoryApplicationClientID.Trim();
        set => activeDirectoryApplicationClientID = value;
    }

    [SerializeField]
    private string azureTenantID;
    public string AzureTenantID
    {
        get => azureTenantID.Trim();
        set => azureTenantID = value;
    }

    [SerializeField]
    private string azureRemoteRenderingAccountID;
    public string AzureRemoteRenderingAccountID
    {
        get => azureRemoteRenderingAccountID.Trim();
        set => azureRemoteRenderingAccountID = value;
    }

    [SerializeField]
    private string azureRemoteRenderingAccountAuthenticationDomain;
    public string AzureRemoteRenderingAccountAuthenticationDomain
    {
        get => azureRemoteRenderingAccountAuthenticationDomain.Trim();
        set => azureRemoteRenderingAccountAuthenticationDomain = value;
    }    

    public override event Action<string> AuthenticationInstructions;

    string authority => "https://login.microsoftonline.com/" + AzureTenantID;

    string redirect_uri = "https://login.microsoftonline.com/common/oauth2/nativeclient";

    string[] scopes => new string[] { "https://sts." + AzureRemoteRenderingAccountAuthenticationDomain + "/mixedreality.signin" };

    public void OnEnable()
    {
        RemoteRenderingCoordinator.ARRCredentialGetter = GetAARCredentials;
        this.gameObject.AddComponent<ExecuteOnUnityThread>();
    }

    public async override Task<AzureFrontendAccountInfo> GetAARCredentials()
    {
        var result = await TryLogin();
        if (result != null)
        {
            Debug.Log("Account signin successful " + result.Account.Username);

            var AD_Token = result.AccessToken;

            return await Task.FromResult(new AzureFrontendAccountInfo(AzureRemoteRenderingAccountAuthenticationDomain, AccountDomain, AzureRemoteRenderingAccountID, "", AD_Token, ""));
        }
        else
        {
            Debug.LogError("Error logging in");
        }
        return default;
    }

    private Task DeviceCodeReturned(DeviceCodeResult deviceCodeDetails)
    {
        //Since everything in this task can happen on a different thread, invoke responses on the main Unity thread
        ExecuteOnUnityThread.Enqueue(() =>
        {
            // Display instructions to the user for how to authenticate in the browser
            Debug.Log(deviceCodeDetails.Message);
            AuthenticationInstructions?.Invoke(deviceCodeDetails.Message);
        });

        return Task.FromResult(0);
    }

    public override async Task<AuthenticationResult> TryLogin()
    {
        var clientApplication = PublicClientApplicationBuilder.Create(ActiveDirectoryApplicationClientID).WithAuthority(authority).WithRedirectUri(redirect_uri).Build();
        AuthenticationResult result = null;
        try
        {
            var accounts = await clientApplication.GetAccountsAsync();

            if (accounts.Any())
            {
                result = await clientApplication.AcquireTokenSilent(scopes, accounts.First()).ExecuteAsync();

                return result;
            }
            else
            {
                try
                {
                    result = await clientApplication.AcquireTokenWithDeviceCode(scopes, DeviceCodeReturned).ExecuteAsync(CancellationToken.None);
                    return result;
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
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("GetAccountsAsync");
            Debug.LogException(ex);
        }

        return null;
    }
}
