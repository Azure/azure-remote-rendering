// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

/// <summary>
/// The data associated with an event that is raised by the custom list control.
/// </summary>
public class ListEventData
{
    public ListEventData(GameObject listObject, ListItem listItem = null, int listItemIndex = -1)
    {
        ListObject = listObject;
        ListItem = listItem;
        ListItemIndex = listItemIndex;
    }

    /// <summary>
    /// The object containing the list behavior
    /// </summary>
    public GameObject ListObject { get; }

    /// <summary>
    /// The list item related to this event
    /// </summary>
    public ListItem ListItem { get; }

    /// <summary>
    /// The list item index related to this event
    /// </summary>
    public int ListItemIndex { get; }
}
