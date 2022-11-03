// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This disables colliders that fall outside of a list's visible area.
/// </summary>
public class ListColliderClipper : MonoBehaviour
{
    private ListScrollerRange _lastVisibleRegion = ListScrollerRange.Empty;
    private IList<object> _lastDataSource = null;
    private HashSet<int> _disabledSet = new HashSet<int>();
    private HashSet<int> _enabledSet = new HashSet<int>();

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The list scroller component.")]
    private ListScrollerBase scroller = null;

    /// <summary>
    /// Get or set the list scroller component.
    /// </summary>
    public ListScrollerBase Scroller
    {
        get => scroller;
        set => scroller = value;
    }

    [SerializeField]
    [Tooltip("The list repeater component.")]
    private ListItemRepeater itemRepeater = null;

    /// <summary>
    /// Get or set the list repeater component.
    /// </summary>
    public ListItemRepeater ItemRepeater
    {
        get => itemRepeater;
        set => itemRepeater = value;
    }

    [SerializeField]
    [Tooltip("If true, this will disable the entire list item gameObject, and not just the colliders.")]
    private bool disableGameObjects = false;

    /// <summary>
    /// If true, this will disable the entire list item gameObject, and not just the colliders.
    /// </summary>
    public bool DisableGameObjects
    {
        get => disableGameObjects;
        set => disableGameObjects = value;
    }

    #endregion Region Serialize Fields

    #region MonoBehavior Methods
    private void Start()
    {
        if (scroller == null)
        {
            scroller = GetComponent<ListScrollerBase>();
        }

        if (itemRepeater == null)
        {
            itemRepeater = GetComponent<ListItemRepeater>();
        }

        if (itemRepeater != null)
        {
            itemRepeater.ListItemCreating.AddListener(InitializeEnableSetOfNewItem);
        }
    }

    private void Update()
    {
        if (_lastDataSource != itemRepeater.DataSource ||
            _lastVisibleRegion != scroller.VisibleRange)
        {
            _lastDataSource = itemRepeater.DataSource;
            _lastVisibleRegion = scroller.VisibleRange;
            UpdateEnableState();
        }
    }

    private void OnDestroy()
    {
        if (itemRepeater != null)
        {
            itemRepeater.ListItemCreating.RemoveListener(InitializeEnableSetOfNewItem);
        }
    }
    #endregion MonoBehavior Methods

    #region Private Methods
    private void InitializeEnableSetOfNewItem(ListEventData data)
    {
        if (data != null && data.ListItem != null && scroller != null)
        {
            var range = scroller.VisibleRange;
            bool enabled = data.ListItemIndex >= range.startIndex && data.ListItemIndex < range.endIndex;
            SetEnableState(data.ListItem, enabled);
        }
    }

    private void UpdateEnableState()
    {
        if (itemRepeater != null && scroller != null)
        {
            _disabledSet.Clear();
            _enabledSet.Clear();

            int count = itemRepeater.Count;
            for (int i = 0; i < count; i++)
            {
                _disabledSet.Add(i);
            }

            var visible = scroller.VisibleRange;
            for (int i = visible.startIndex; i < visible.endIndex; i++)
            {
                _disabledSet.Remove(i);
                _enabledSet.Add(i);
            }

            ApplyEnableState();
        }
    }

    private void ApplyEnableState()
    {
        if (itemRepeater != null)
        {
            foreach (int i in _disabledSet)
            {
                SetEnableState(itemRepeater.GetItem(i), false);
            }

            foreach (int i in _enabledSet)
            {
                SetEnableState(itemRepeater.GetItem(i), true);
            }
        }
    }

    private void DisableAll()
    {
        int count = itemRepeater.DataSource?.Count ?? 0;
        for (int i = 0; i < count; i++)
        {
            SetEnableState(itemRepeater.GetItem(i), false);
        }
    }

    private void SetEnableState(ListItem item, bool enable)
    {
        if (item != null)
        {
            if (disableGameObjects)
            {
                item.gameObject.SetActive(enable);
            }
            else
            {
                SetCollidersEnableState(item.GetComponentsInChildren<Collider>(), enable);
            }
        }
    }

    private void SetCollidersEnableState(Collider[] colliders, bool enable)
    {
        if (colliders == null)
        {
            return;
        }

        int count = colliders.Length;
        for (int i = 0; i < count; i++)
        {
            colliders[i].enabled = enable;
        }
    }
    #endregion Private Methods
}
