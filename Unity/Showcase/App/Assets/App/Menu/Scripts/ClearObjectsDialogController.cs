// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using System;
using System.Threading.Tasks;
using UnityEngine;

public class ClearObjectsDialogController : MonoBehaviour
{
    #region Serialized Fields    
    private static Func<Task<AppDialog.AppDialogResult>> OnCreateRoomNeedsConfirmation;
    private static Func<Task<AppDialog.AppDialogResult>> OnJoinRoomNeedsConfirmation;
    #endregion Serialized Fields

    #region MonoBehavior Functions
    private void Start()
    {
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
            if (createRoom)
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
        AppDialog.AppDialogResult bringObjects = await AppServices.AppNotificationService.ShowDialog(new DialogOptions()
        {
            Title = "Bring Objects to New Room?",
            Message = "You are about to create a shared room.\n\nWould you like to bring your holograms with you or start an empty room?",
            OKLabel = "Bring",
            NoLabel = "Empty",
            CancelLabel = "Cancel",
            Location = AppDialog.AppDialogLocation.Menu,
            Buttons = AppDialog.AppDialogButtons.All
        });

        return bringObjects;
    }
    
    private async Task<AppDialog.AppDialogResult> ShowJoinDialog()
    {
        AppDialog.AppDialogResult clearObjects = await AppServices.AppNotificationService.ShowDialog(new DialogOptions()
        {
            Title = "Confirm Clearing Objects?",
            Message = "You are about to join a shared room, all your holograms will be cleared.\n\nWould you like to clear your holograms?",
            NoLabel = "Clear",
            CancelLabel = "Cancel",
            Location = AppDialog.AppDialogLocation.Menu,
            Buttons = AppDialog.AppDialogButtons.No | AppDialog.AppDialogButtons.Cancel
        });

        return clearObjects;
    }
    #endregion
}

