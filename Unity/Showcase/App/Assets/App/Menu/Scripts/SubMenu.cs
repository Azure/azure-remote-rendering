// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using UnityEngine.Events;

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

    [Header("Events")]

    [SerializeField]
    [Tooltip("Event fired when the sub-menu is visible.")]
    private UnityEvent activated = new UnityEvent();

    /// <summary>
    /// TEvent fired when the sub-menu is visible.
    /// </summary>
    public UnityEvent Activated => activated;

    [SerializeField]
    [Tooltip("Event fired when the sub-menu is hidden.")]
    private UnityEvent deactivated = new UnityEvent();

    /// <summary>
    /// TEvent fired when the sub-menu is hidden.
    /// </summary>
    public UnityEvent Deactivated => deactivated;
    #endregion Serialized Fields

    #region Public Properties
    #endregion Public Properties

    #region MonoBehaviour Functions
    #endregion MonoBehaviour Functions

    #region Public Functions
    /// <summary>
    /// Invoke this when the sub-menu has been activated.
    /// </summary>
    public virtual void OnActivated()
    {
        SetActiveParts(true);
        activated?.Invoke();
    }

    /// <summary>
    /// Invoke this when the sub-menu has been deactivated.
    /// </summary>
    public virtual void OnDeactivated()
    {
        SetActiveParts(false);
        deactivated?.Invoke();
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

