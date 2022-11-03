// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Identity.Client;
using Microsoft.MixedReality.Toolkit.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class CachedCredentialsDialogController : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    private int maxUserNameLength = 30;

    private static Func<IAccount, Task<bool>> OnCachedCredentialNeedsConfirmation;
    #endregion Serialized Fields

    #region MonoBehavior Functions
    private void Start()
    {
        OnCachedCredentialNeedsConfirmation += ShowDialog;
    }

    #endregion MonoBehavior Functions

    #region Public Functions

    public static async Task<bool> CachedCredentialNeedsConfirmation(IAccount account)
    {
        bool result = false;
        bool dialogComplete = false;

        ExecuteOnUnityThread.Enqueue(async () =>
        {
            result = await OnCachedCredentialNeedsConfirmation?.Invoke(account);
            dialogComplete = true;
        });

        while (!dialogComplete)
        {
            await Task.Delay(25);
        }

        return result;
    }

    public async Task<bool> ShowDialog(IAccount account)
    {
        bool useCachedCredentials = false;

        List<string> userNameLines = new List<string>() { account.Username };

        int maxBreaks = 4;
        while(userNameLines[userNameLines.Count - 1].Length > maxUserNameLength && maxBreaks > 0)
        {
            string last = userNameLines[userNameLines.Count - 1];

            if (last.Contains('@') && last.IndexOf('@') < maxUserNameLength)
            {
                userNameLines[userNameLines.Count - 1] = last.Substring(0, last.IndexOf('@') + 1);
                userNameLines.Add(last.Substring(last.IndexOf('@') + 1));
            }
            else if(last.Contains('.') && last.IndexOf('.') < maxUserNameLength)
            {
                userNameLines[userNameLines.Count - 1] = last.Substring(0, last.IndexOf('.') + 1);
                userNameLines.Add(last.Substring(last.IndexOf('.') + 1));
            }
            else
            {
                userNameLines[userNameLines.Count - 1] = last.Substring(0, maxUserNameLength);
                userNameLines.Add(last.Substring(maxUserNameLength + 1));
            }
            maxBreaks--;
        }

        string username = string.Join("\n    ", userNameLines);

        DialogOptions options = new DialogOptions()
        { 
            OKLabel = "Yes",
            CancelLabel = "No - Sign out",
            Buttons = AppDialog.AppDialogButtons.Ok | AppDialog.AppDialogButtons.Cancel,
            Title = "Use cached credentials?",
            Message =
                "This app has cached credentials for:" +
                "\n" +
                "\n" +
                $"    <b>{username}</b>" +
                "\n" +
                "\n" +
                "Would you like to use these credentials?"
        };

        useCachedCredentials = await AppServices.AppNotificationService.ShowDialog(options) == AppDialog.AppDialogResult.Ok;

        return useCachedCredentials;
    }
    #endregion
}

