// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Identity.Client;
using System;
using System.Threading.Tasks;
using UnityEngine;

public class ClearObjectsDialogController : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    [Tooltip("The dialog used to confirm the clearing of remote objects when creating a room.")]
    private AppDialog createDialogPrefab = null;
    [SerializeField]
    [Tooltip("The dialog used to confirm the clearing of remote objects when joining a room.")]
    private AppDialog joinDialogPrefab = null;
    
    private static Func<Task<AppDialog.AppDialogResult>> OnCreateRoomNeedsConfirmation;
    private static Func<Task<AppDialog.AppDialogResult>> OnJoinRoomNeedsConfirmation;

    #endregion Serialized Fields

    #region MonoBehavior Functions
    private void Start()
    {
        if (createDialogPrefab == null || joinDialogPrefab == null)
        {
            DestroyController();
        }

        OnCreateRoomNeedsConfirmation += ShowCreateDialog;
        OnJoinRoomNeedsConfirmation += ShowJoinDialog;
    }

    #endregion MonoBehavior Functions

    #region Public Functions

    public static async Task<AppDialog.AppDialogResult> ClearObjectsNeedsConfirmation(bool createRoom)
    {
        AppDialog.AppDialogResult result = AppDialog.AppDialogResult.Cancel;
        bool dialogComplete = false;

        ExecuteOnUnityThread.Enqueue(async () =>
        {
            if(createRoom)
            {
                result = await OnCreateRoomNeedsConfirmation?.Invoke();
            }
            else
            {
                result = await OnJoinRoomNeedsConfirmation?.Invoke();
            }

            dialogComplete = true;
        });

        while (!dialogComplete)
        {
            await Task.Delay(25);
        }
        
        return result;
    }

    private async Task<AppDialog.AppDialogResult> ShowCreateDialog()
    {
        if (createDialogPrefab == null)
        {
            DestroyController();
            return AppDialog.AppDialogResult.Cancel;
        }

        AppDialog.AppDialogResult bringObjects;

        GameObject dialogObject = null;
        try
        {
            dialogObject = Instantiate(createDialogPrefab.gameObject, transform);
            var appDialog = dialogObject.GetComponent<AppDialog>();

            appDialog.DialogText.text = 
                "You are about to create a shared room.\n" +
                "Would you like to bring your holograms\n" +
                "with you or start an empty room?";
            appDialog.DialogHeaderText.text = "Bring Objects into New Room?";
            appDialog.OkButtonText.text = "Bring";
            appDialog.NoButtonText.text = "Empty";
            appDialog.CancelButtonText.text = "Cancel";

            bringObjects = await appDialog.Open();
        }
        finally
        {
            if (dialogObject != null)
            {
                GameObject.Destroy(dialogObject);
            }
        }

        return bringObjects;
    }
    
    private async Task<AppDialog.AppDialogResult> ShowJoinDialog()
    {
        if (joinDialogPrefab == null)
        {
            DestroyController();
            return AppDialog.AppDialogResult.Cancel;
        }

        AppDialog.AppDialogResult clearObjects;

        GameObject dialogObject = null;
        try
        {
            dialogObject = Instantiate(joinDialogPrefab.gameObject, transform);
            var appDialog = dialogObject.GetComponent<AppDialog>();

            appDialog.DialogText.text = 
                "You are about to join a shared room,\n" +
                "all your holograms will be cleared.\n" +
                "Would you like to continue?";
            appDialog.DialogHeaderText.text = "Confirm Clearing Objects?";
            appDialog.OkButtonText.text = "Yes";
            appDialog.CancelButtonText.text = "No";

            clearObjects = await appDialog.Open();
        }
        finally
        {
            if (dialogObject != null)
            {
                GameObject.Destroy(dialogObject);
            }
        }

        return clearObjects;
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

