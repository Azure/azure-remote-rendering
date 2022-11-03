// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication;

public class ShareRoomListItem : ListItemEventHandler
{
    private ISharingServiceRoom _room;

    #region Serialized Fields
    #endregion Serialized Fields

    #region Public Properties
    #endregion Public Properties

    #region MonoBehaviour Functions
    #endregion MonoBehaviour Functions

    #region Public Functions
    public override void OnDataSourceChanged(ListItem item, object oldValue, object newValue)
    {
        _room = newValue as ISharingServiceRoom;
        if (_room != null)
        {
            GetComponent<ListItemWithStaticAction>()?.SetPrimaryLabel(_room.Name);
        }
    }

    public override void OnInvoked(ListItem item)
    {
        if (_room != null)
        {
            AppServices.SharingService.JoinRoom(_room);
        }
    } 
    #endregion Public Functions
}

