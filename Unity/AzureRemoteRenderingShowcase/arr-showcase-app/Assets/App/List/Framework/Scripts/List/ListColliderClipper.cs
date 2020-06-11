// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This disables colliders that fall outside of a list's visible area.
/// </summary>
public class ListColliderClipper : MonoBehaviour
{
    private (int startIndex, int count) _lastVisibleRegion = (-1, 0);
    private List<object> _lastDataSource = null;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The list scroller component.")]
    ListScrollerBase scroller = null;

    /// <summary>
    /// Get or set the list scroller component.
    /// </summary>
    ListScrollerBase Scroller
    {
        get => scroller;
        set => scroller = value;
    }

    [SerializeField]
    [Tooltip("The list repeater component.")]
    ListItemRepeater itemRepeater = null;

    /// <summary>
    /// Get or set the list repeater component.
    /// </summary>
    ListItemRepeater ItemRepeater
    {
        get => itemRepeater;
        set => itemRepeater = value;
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
    }

    private void Update()
    {
        if (_lastDataSource != itemRepeater.DataSource)
        {
            _lastDataSource = itemRepeater.DataSource;
            _lastVisibleRegion = (-1, 0);
            DisableAll();
        }


        if (_lastVisibleRegion != scroller.VisibleRange)
        {
            UpdateColliderState(scroller.VisibleRange);
        }
    }
    #endregion MonoBehavior Methods

    #region Private Methods
    private void UpdateColliderState((int startIndex, int endIndex) range)
    {
        for (int i = _lastVisibleRegion.startIndex; i < _lastVisibleRegion.count; i++)
        {
            ListItem currentItem = itemRepeater.GetItem(i);
            if (currentItem != null)
            {
                SetCollidersEnableState(currentItem.GetComponentsInChildren<Collider>(), false);
            }
        }

        _lastVisibleRegion = scroller.VisibleRange;
        for (int i = _lastVisibleRegion.startIndex; i < _lastVisibleRegion.count; i++)
        {
            ListItem currentItem = itemRepeater.GetItem(i);
            if (currentItem != null)
            {
                SetCollidersEnableState(currentItem.GetComponentsInChildren<Collider>(), true);
            }
        }
    }

    private void DisableAll()
    {
        int count = itemRepeater.DataSource?.Count ?? 0;
        for (int i = 0; i < count; i++)
        {
            ListItem currentItem = itemRepeater.GetItem(i);
            if (currentItem != null)
            {
                SetCollidersEnableState(currentItem.GetComponentsInChildren<Collider>(), false);
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
