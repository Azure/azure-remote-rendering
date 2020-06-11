// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

/// <summary>
/// An optional behavior to place on sub-menu containers. This is controlled by a SubMenuController class. This
/// behavior gives containers the ability to define which componets should be shown when the sub-menu is active.
/// </summary>
public class SubMenu : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    [Tooltip("The game objects that are shown when the sub-menu is active, and hidden when the sub-menu is inactive.")]
    private GameObject[] showable = new GameObject[0];

    /// <summary>
    /// The game objects that are shown when the sub-menu is active, and hidden when the sub-menu is inactive.
    /// </summary>
    public GameObject[] Showable
    {
        get => showable;
        set => showable = value;
    }
    #endregion Serialized Fields

    #region Public Properties
    #endregion Public Properties

    #region MonoBehaviour Functions
    #endregion MonoBehaviour Functions

    #region Public Functions
    /// <summary>
    /// Invoke this when the sub-menu has been activated.
    /// </summary>
    public void OnActivated()
    {
        SetActiveParts(true);
    }

    /// <summary>
    /// Invoke this when the sub-menu has been deactivated.
    /// </summary>
    public void OnDeactivated()
    {
        SetActiveParts(false);
    }
    #endregion Public Functions

    #region Private Functions
    /// <summary>
    /// Show or hide parts.
    /// </summary>
    private void SetActiveParts(bool active)
    {
        if (showable == null)
        {
            return;
        }

        foreach (GameObject part in Showable)
        {
            if (part != null)
            {
                part.SetActive(active);
            }
        }
    }
    #endregion Private Functions
}

