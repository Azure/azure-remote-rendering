// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

/// <summary>
/// Represents a list containing sharing room objects. Currently this is only used to pass along the sharing menu item.
/// </summary>
public class SharingRoomList : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    [Tooltip("The sub-menu controller that controls which sub-menu is visible. This is used by the back button to go back to the main menu.")]
    private SubMenuController menuController;

    /// <summary>
    /// The sub-menu controller that controls which sub-menu is visible.
    /// This is used by the back button to go back to the main menu.
    /// </summary>
    public SubMenuController MenuController
    {
        get => menuController;
        set => menuController = value;
    }

    [SerializeField]
    [Tooltip("The index of the menu to go to when the back button is clicked.")]
    private int backDestinationIndex = 0;

    /// <summary>
    /// The index of the menu to go to when the back button is clicked.
    /// </summary>
    public int BackDestinationIndex
    {
        get => backDestinationIndex;
        set => backDestinationIndex = value;
    }
    #endregion Serialized Fields
}

