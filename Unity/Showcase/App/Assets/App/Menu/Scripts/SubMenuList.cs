// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

/// <summary>
/// Represents a list that is inside one of the app's sub-menus. This enables the list to 
/// go back to the preview sub-menu.
/// </summary>
public class SubMenuList : MonoBehaviour
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

    #region MonoBehavior Functions
    /// <summary>
    /// Find the closest SubMenuController if current MenuController is null.
    /// </summary>
    private void Start()
    {
        if (menuController == null)
        {
            menuController = this.GetComponentInParent<SubMenuController>();
        }
    }
    #endregion MonoBehavior Functions

    #region Public Methods
    /// <summary>
    /// Go to to the previous page.
    /// </summary>
    public void GoBack()
    {
        if (MenuController != null && BackDestinationIndex >= 0)
        {
            MenuController.GoToMenu(BackDestinationIndex);
        }
    }
    #endregion Public Methods
}
