// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication;
using TMPro;
using UnityEngine;

public class SharingRoomListItem : ListItemEventHandler
{
    private bool _isBackButton;
    private ISharingServiceRoom _room;
    private SubMenuController _menuController;
    private int _backButtonDestinationIndex = 0;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The container holding a normal icon.")]
    private GameObject nonBackButtonIcon = null;

    /// <summary>
    /// The container holding a normal icon.
    /// </summary>
    public GameObject NonBackButtonIcon
    {
        get => nonBackButtonIcon;
        set => nonBackButtonIcon = value;
    }

    [SerializeField]
    [Tooltip("The container holding a back icon.")]
    private GameObject backButtonIcon = null;

    /// <summary>
    /// The container holding a back icon.
    /// </summary>
    public GameObject BackButtonIcon
    {
        get => backButtonIcon;
        set => backButtonIcon = value;
    }

    [SerializeField]
    [Tooltip("This is the label field that holds the room name.")]
    private TextMeshPro label = null;

    /// <summary>
    /// This is the label field that holds the room name.
    /// </summary>
    public TextMeshPro Label
    {
        get => label;
        set => label = value;
    }
    #endregion Serialized Fields

    #region Public Properties

    #endregion Public Properties

    #region MonoBehaviour Functions
    private void Awake()
    {
        if (nonBackButtonIcon != null)
        {
            nonBackButtonIcon.SetActive(false);
        }

        if (backButtonIcon != null)
        {
            backButtonIcon.SetActive(false);
        }
    }
    #endregion MonoBehaviour Functions

    #region Public Functions
    public override void OnDataSourceChanged(ListItem item, System.Object oldValue, System.Object newValue)
    {
        SharingRoomList list = item?.Parent?.GetComponent<SharingRoomList>();
        if (list != null)
        {
            _menuController = list.MenuController;
            _backButtonDestinationIndex = list.BackDestinationIndex;
        }
        else
        {
            _menuController = null;
        }

        _room = newValue as ISharingServiceRoom;
        if (_room == null)
        {
            _isBackButton = true;
            UpdateLabel("Back");
        }
        else
        {
            _isBackButton = false;
            UpdateLabel(_room.Name);
        }

        if (nonBackButtonIcon != null)
        {
            nonBackButtonIcon.SetActive(!_isBackButton);
        }

        if (backButtonIcon != null)
        {
            backButtonIcon.SetActive(_isBackButton);
        }
    }

    public override void OnInvoked(ListItem item)
    {
        if (_room != null)
        {
            AppServices.SharingService.JoinRoom(_room);
        }
        else
        {
            AppServices.SharingService.Connect();
        }

        _menuController?.GoToMenu(_backButtonDestinationIndex);
        // // Close share menu
        // GetComponentInParent<HandMenuHooks>()?.ClearMenu();
    } 
    #endregion Public Functions

    #region Private Functions
    private void UpdateLabel(string value)
    {
        if (label != null)
        {
            label.text = string.IsNullOrEmpty(value) ? "Unknown" : value;
        }
    }
    #endregion Private Functions
}

