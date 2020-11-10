// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class CachedCredentialsDialogController : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    [Tooltip("The dialog used to confirm the use of cached credentials.")]
    private AppDialog dialogPrefab = null;

    private int maxUserNameLength = 30;

    private static Func<IAccount, Task<bool>> OnCachedCredentialNeedsConfirmation;

    /// <summary>
    /// The dialog used to confirm creation of a new remote session.
    /// </summary>
    public AppDialog DialogPrefab
    {
        get => dialogPrefab;
        set => dialogPrefab = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Functions
    private void Start()
    {
        if (dialogPrefab == null)
        {
            DestroyController();
        }

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
        if (dialogPrefab == null)
        {
            DestroyController();
            return false;
        }

        bool useCachedCredentials = false;

        GameObject dialogObject = null;
        try
        {
            dialogObject = Instantiate(dialogPrefab.gameObject, transform);
            var appDialog = dialogObject.GetComponent<AppDialog>();

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

            appDialog.DialogText.text = 
                "This app has cached credentials for:" +
                "\n" +
                "\n" +
                $"    <b>{username}</b>" +
                "\n" +
                "\n" +
                "Would you like to use these credentials ?";
            appDialog.DialogHeaderText.text = "Use cached credentials?";
            appDialog.OkButtonText.text = "Yes";
            appDialog.CancelButtonText.text = "No - Sign out";

            useCachedCredentials = await appDialog.Open() == AppDialog.AppDialogResult.Ok;
        }
        finally
        {
            if (dialogObject != null)
            {
                GameObject.Destroy(dialogObject);
            }
        }

        DestroyController();

        return useCachedCredentials;
    }
    #endregion

    #region Private Functions

    private void DestroyController()
    {
        if (gameObject != null)
        {
            GameObject.Destroy(gameObject);
        }
    }

    #endregion Private Functions
}

