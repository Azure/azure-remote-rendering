// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine;

/// <summary>
/// A helper to select and open a sub menu
/// </summary>
[RequireComponent(typeof(Interactable))]
public class SelectSubMenu : MonoBehaviour
{
    private Interactable _interactable = null;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The sub menu controller to change.")]
    private SubMenuController controller = null;

    /// <summary>
    /// The sub menu controller to change.
    /// </summary>
    public SubMenuController Controller
    {
        get => controller;
        set => controller = value;
    }

    [SerializeField]
    [Tooltip("When the interactable is clicked, this sub-menu contianing this type will be selected.")]
    private SubMenu subMenuPrefab;

    /// <summary>
    /// When the interactable is clicked, this sub-menu contianing this type will be selected.
    /// </summary>
    public SubMenu SubMenuPrefab
    { 
        get => subMenuPrefab;
        set => subMenuPrefab = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Functions
    private void Start()
    {
        _interactable = GetComponent<Interactable>();
        if (_interactable != null)
        {
            _interactable.OnClick.AddListener(OpenSubMenu);
        }

        if (controller == null)
        {
            controller = GetComponentInParent<SubMenuController>();
        }
    }

    private void OnDestroy()
    {
        if (_interactable != null)
        {
            _interactable.OnClick.RemoveListener(OpenSubMenu);
        }
    }
    #endregion MonoBehavior Functions

    #region Private Functions
    private void OpenSubMenu()
    {
        if (controller != null && subMenuPrefab != null)
        {
            controller.GoToMenu(subMenuPrefab.GetType());
        }
    }
    #endregion Private Functions
}
