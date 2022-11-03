// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

/// <summary>
/// A behavior to show an empty message when the list data source is empty
/// </summary>
public class ListEmptyMessage : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    [Tooltip("The list holding the data source.")]
    private ListItemRepeater listItemRepeater = null;

    /// <summary>
    /// The list holding the data source.
    /// </summary>
    public ListItemRepeater ListItemRepeater
    {
        get => listItemRepeater;
        set => listItemRepeater = value;
    }

    [SerializeField]
    [Tooltip("The container to show when the data source is empty.")]
    private GameObject emptyMessage;

    /// <summary>
    /// The container to show when the data source is empty.
    /// </summary>
    public GameObject EmptyMessage
    {
        get => emptyMessage;
        set => emptyMessage = value;
    }

    [SerializeField]
    [Tooltip("If number of items is less than this, the empty message is hidden.")]
    private int minItems = 0;

    /// <summary>
    /// If number of items is less than this, the empty message is hidden.
    /// </summary>
    public int MinItems
    {
        get => minItems;
        set => minItems = value;
    }

    [SerializeField]
    [Tooltip("If number of items is greater than this, the empty message is hidden.")]
    private int maxItems = 0;

    /// <summary>
    /// If number of items is greater than this, the empty message is hidden.
    /// </summary>
    public int MaxItems
    {
        get => maxItems;
        set => maxItems = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Functions
    private void OnEnable()
    {
        if (listItemRepeater != null)
        {
            listItemRepeater.DataSourceChanged.AddListener(UpdateEmptyMessage);
        }
        UpdateEmptyMessage(null);
    }

    private void OnDisable()
    {
        if (listItemRepeater != null)
        {
            listItemRepeater.DataSourceChanged.RemoveListener(UpdateEmptyMessage);
        }
    }
    #endregion MonoBehavior Functions

    #region Private Functions
    private void UpdateEmptyMessage(ListEventData args)
    {
        int items = -1;
        if (listItemRepeater != null && listItemRepeater.DataSource != null)
        {
            items = listItemRepeater.DataSource.Count;
        }

        if (emptyMessage != null)
        {
            emptyMessage.SetActive(items >= minItems && items <= maxItems);
        }
    }
    #endregion Private Functions
}
