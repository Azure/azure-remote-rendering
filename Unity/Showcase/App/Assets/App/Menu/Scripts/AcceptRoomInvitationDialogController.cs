// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication;
using UnityEngine;

public class AcceptRoomInvitationDialogController : MonoBehaviour
{
    private bool _consideringInvitation = false;

    #region MonoBehavior Functions
    private void OnEnable()
    {
        AppServices.SharingService.RoomInviteReceived += OnRoomInviteReceived;
    }
    private void OnDisable()
    {
        AppServices.SharingService.RoomInviteReceived -= OnRoomInviteReceived;
    }
    #endregion MonoBehavior Functions

    #region Private Functions
    private async void OnRoomInviteReceived(ISharingService sender, ISharingServiceRoom room)
    {
        // Don't flood the user with invitation requests.  Only handle one, and ignore the rest.
        if (_consideringInvitation)
        {
            return;
        }

        _consideringInvitation = true;

        try
        {
            string message = string.IsNullOrEmpty(room.InvitationSender) ?
                $"You have been invited to join '{room.Name}'.\n\nWould you like to join this new room?" :
                $"{room.InvitationSender} has invited you to join '{room.Name}'.\n\nWould you like to join this new room?";

            var dialogResult = await AppServices.AppNotificationService.ShowDialog(new DialogOptions()
            {
                Title = "Join Room?",
                Message = message,
                Buttons = AppDialog.AppDialogButtons.All,
                OKLabel = "Accept",
                NoLabel = "Decline",
                CancelLabel = "Ignore"
            });

            if (dialogResult == AppDialog.AppDialogResult.Ok)
            {
                if (sender.CurrentRoom != null)
                {
                    await sender.LeaveRoom();
                }
                sender.JoinRoom(room);
            }
            else if (dialogResult == AppDialog.AppDialogResult.No)
            {
                sender.DeclineRoom(room);
            }
        }
        finally
        {
            _consideringInvitation = false;
        }
    }
    #endregion Private Functions
}
