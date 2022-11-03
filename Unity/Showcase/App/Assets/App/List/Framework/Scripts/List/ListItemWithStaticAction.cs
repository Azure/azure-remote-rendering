// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using TMPro;
using UnityEngine;

/// <summary>
/// When on a list item prefab, this will show an action button ux, and execute the action when clicked.
/// </summary>
public class ListItemWithStaticAction : ListItemEventHandler
{
    ListItemActionData _data = null;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("This is the label field that holds the action name.")]
    private TextMeshPro staticActionPrimaryLabel = null;

    /// <summary>
    /// This is the label field that holds the action name.
    /// </summary>
    public TextMeshPro StaticActionPrimaryLabel
    {
        get => staticActionPrimaryLabel;
        set => staticActionPrimaryLabel = value;
    }

    [SerializeField]
    [Tooltip("This is the label field that holds the action name.")]
    private TextMeshPro staticActionSecondaryLabel = null;

    /// <summary>
    /// This is the label field that holds the action name.
    /// </summary>
    public TextMeshPro StaticActionSecondaryLabel
    {
        get => staticActionSecondaryLabel;
        set => staticActionSecondaryLabel = value;
    }

    [SerializeField]
    [Tooltip("This is the container that holds the icon prefab. This will cleared when loaded.")]
    private GameObject iconContainer = null;

    /// <summary>
    /// This is the container that holds the icon prefab. This will cleared when loaded.
    /// </summary>
    public GameObject IconContainer
    {
        get => iconContainer;
        set => iconContainer = value;
    }
    #endregion Serialized Fields

    #region MonoBehaviour Functions
    #endregion MonoBehaviour Functions

    #region Public Functions
    public override void OnDataSourceChanged(ListItem item, object oldValue, object newValue)
    {
        _data = newValue as ListItemActionData;
        if (_data != null)
        {
            SetPrimaryLabel(_data.PrimaryLabel);
            SetSecondaryLabel(_data.SecondaryLabel);
            SetIconOverride(_data.IconOverridePrefab);
            SetIconType(_data.IconType);
        }
    }

    public override void OnInvoked(ListItem item)
    {
        if (_data != null)
        {
            _data.Execute();
        }
    }

    public void SetPrimaryLabel(string label)
    {
        if (staticActionPrimaryLabel != null &&
            staticActionPrimaryLabel.text != label)
        {
            staticActionPrimaryLabel.text = label;
        }
    }

    public void SetSecondaryLabel(string label)
    {
        if (staticActionSecondaryLabel != null &&
            staticActionSecondaryLabel.text != label)
        {
            staticActionSecondaryLabel.text = label;
        }
    }
    #endregion Public Functions

    #region Private Functions
    public void SetIconOverride(GameObject prefab)
    {
        if (iconContainer != null && prefab != null)
        {
            int childCount = iconContainer.transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                // destroy immediately to avoid the ClippingUtility from discovering
                // renderers that might belong to the child that is being deleted.
                DestroyImmediate(iconContainer.transform.GetChild(i).gameObject);
            }
            Instantiate(prefab, iconContainer.transform);
        }
    }

    public void SetIconType(FancyIconType iconType)
    {
        if (iconType != FancyIconType.Unknown)
        {
            var fancyIcon = GetComponentInChildren<FancyIcon>();
            fancyIcon.Selected = iconType;
        }
    }
    #endregion Private Functions
}
