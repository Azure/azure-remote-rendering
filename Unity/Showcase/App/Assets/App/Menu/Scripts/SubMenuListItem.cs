// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

/// <summary>
/// Represents a list item that is inside one of the app's sub-menus. This enables the list to 
/// go back to the preview sub-menu when list item is click
/// </summary>
public class SubMenuListItem : ListItemEventHandler
{
    #region Serialized Fields
    #endregion Serialized Fields

    #region Public Properties
    #endregion Public Properties

    #region MonoBehaviour Functions
    #endregion MonoBehaviour Functions

    #region Public Functions
    public override void OnInvoked(ListItem item)
    {
        var list = GetComponent<ListItem>()?.Parent.GetComponentInParent<SubMenuList>();
        if (list != null)
        {
            list.GoBack();
        }
    }
    #endregion Public Functions
}
