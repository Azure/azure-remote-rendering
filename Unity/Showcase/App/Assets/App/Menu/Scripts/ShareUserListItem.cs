// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;

/// <summary>
/// A list item that will display a user's name and status.
/// </summary>
public class ShareUserListItem : ListItemEventHandler
{
    private bool _valid;
    private SharingServicePlayerData _data;
    private LogHelper<ShareUserListItem> _logger = new LogHelper<ShareUserListItem>();

    #region Serialized Fields
    #endregion Serialized Fields

    #region Public Properties
    #endregion Public Properties

    #region MonoBehaviour Functions
    #endregion MonoBehaviour Functions

    #region Public Functions
    public override void OnDataSourceChanged(ListItem item, object oldValue, object newValue)
    {
        var listItemWithLabel = GetComponent<ListItemWithStaticAction>();
        if (newValue is SharingServicePlayerData)
        {
            _data = (SharingServicePlayerData)newValue;
            listItemWithLabel.SetPrimaryLabel(_data.DisplayName);
            listItemWithLabel.SetSecondaryLabel(ToStatusString(_data.Status));
            _valid = true;
        }
        else
        {
            _valid = false;
        }
    }

    public override async void OnInvoked(ListItem item)
    {
        var room = AppServices.SharingService.CurrentRoom;
        if (_valid && room != null && room.IsPrivate)
        {
            var dialogResult = await AppServices.AppNotificationService.ShowDialog(new DialogOptions()
            {
                Title = "Invite User?",
                Message = $"Do you want to invite {_data.DisplayName} to your current room, '{room.Name}'?",
                Location = AppDialog.AppDialogLocation.Menu,
                Buttons = AppDialog.AppDialogButtons.Ok | AppDialog.AppDialogButtons.No,
                OKLabel = "Yes",
                NoLabel = "No"
            });

            if (dialogResult == AppDialog.AppDialogResult.Ok)
            {
                if (await AppServices.SharingService.InviteToRoom(_data))
                {
                    AppServices.AppNotificationService.RaiseNotification(
                        $"{_data.DisplayName} has been invited to the current room.",
                        AppNotificationType.Info);
                }
                else
                {
                    AppServices.AppNotificationService.RaiseNotification(
                        $"We couldn't invite {_data.DisplayName} to the room.",
                        AppNotificationType.Error);
                }
            }
        }
    }
    #endregion Public Functions

    #region Private Functions
    private string ToStatusString(SharingServicePlayerStatus status)
    {
        switch (status)
        {
            case SharingServicePlayerStatus.Unknown:
                return "Unknown Status";

            case SharingServicePlayerStatus.Offline:
                return "Offline";

            case SharingServicePlayerStatus.Online:
                return "Online";

            default:
                _logger.LogError("Unknown player presence status '{0}'", status);
                return "Unknown Status";
        }
    }
    #endregion Private Functions
}

