// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The list component that laysout list items in a grid.
/// </summary>
public class ListContainer : GridObjectCollection
{
    #region Private Fields
    private int _lockUpdatesCount;
    private Vector2 _padding = Vector2.zero;
    private Vector2 _itemContainerSize = Vector2.zero;
    private Dictionary<Transform, Transform> _itemToContainer = new Dictionary<Transform, Transform>();
    #endregion Private Fields

    #region Public Properties
    /// <summary>
    /// The padding around the container
    /// </summary>
    public Vector2 ContainerPadding
    {
        get => _padding;

        set
        {
            if (_padding != value)
            {
                _padding = value;
                UpdateCollection();
            }
        }
    }

    /// <summary>
    /// The size of the entire list container. This is updated after each 
    /// insert, removal, and item container changes.
    /// </summary>
    public Vector2 ContainerSize { get; private set; } = Vector2.zero;

    /// <summary>
    /// The size of a single item container
    /// </summary>
    public Vector2 ItemContainerSize
    {
        get => _itemContainerSize;

        set
        {
            if (_itemContainerSize != value)
            {
                _itemContainerSize = value;
                UpdateCollection();
            }
        }
    }

    /// <summary>
    /// The layout of items on an x-y grid
    /// </summary>
    public Vector2 ContainerItemSize { get; private set; }
    #endregion Public Properties

    #region Public Functions
    public bool Contains(Transform listItem)
    {
        return listItem?.parent?.parent == transform;
    }

    /// <summary>
    /// Notify the list container that its children will be changing. This prevent the container from executing any 
    /// layout request. To resume container layouts, call EndUpdate().
    /// </summary>
    public void StartUpdate()
    {
        _lockUpdatesCount++;
    }

    /// <summary>
    /// Insert transform into container.
    /// </summary>
    public void Insert(Transform listItem, bool worldPositionStays)
    {
        if (listItem == null || Contains(listItem))
        {
            return;
        }

        Transform listItemContainer = null;
        if (!_itemToContainer.TryGetValue(listItem, out listItemContainer))
        {
            GameObject listItemContainerObject = new GameObject();
            listItemContainerObject.name = $"'{listItem.name}' Container";
            listItemContainerObject.transform.SetParent(transform, false);
            UpdateCollection();

            listItemContainer = listItemContainerObject.transform;
        }

        listItem.SetParent(listItemContainer.transform, worldPositionStays);
        _itemToContainer[listItem] = listItemContainer.transform;
    }

    /// <summary>
    /// Destroy all the children within this container.
    /// </summary>
    public List<Transform> DestoryAll()
    {
        var containerCount = transform.childCount;
        var toDestory = new List<Transform>();
        for (int i = 0; i < containerCount; i++)
        {
            Transform container = transform.GetChild(i);
            var itemCount = container.childCount;
            for (int j = 0; j < itemCount; j++)
            {
                toDestory.Add(container.GetChild(j));
            }
        }

        StartUpdate();
        foreach (var destory in toDestory)
        {
            Remove(destory);
            if (destory.gameObject != null)
            {
                try
                {
                    GameObject.Destroy(destory.gameObject);
                }
                catch (Exception)
                {
                }
            }
        }
        EndUpdate();

        return toDestory;
    }

    /// <summary>
    /// Remove the list item from this container.
    /// </summary>
    public void Remove(Transform listItem)
    {
        _itemToContainer.Remove(listItem);
        if (listItem == null || !Contains(listItem))
        {
            return;
        }

        Transform listItemContainer = listItem.parent;
        listItem.SetParent(null, true);

        if (listItemContainer != null)
        {
            listItemContainer.SetParent(null, true);
            if (listItemContainer.gameObject != null)
            {
                try
                {
                    GameObject.Destroy(listItemContainer.gameObject);
                }
                catch (Exception)
                {
                }
            }
        }

        UpdateCollection();
    }

    /// <summary>
    /// Notify the list container that its children won't be changing anymore. This will resume container layouts.
    /// </summary>
    public void EndUpdate()
    {
        _lockUpdatesCount = Mathf.Max(0, _lockUpdatesCount - 1);
        UpdateCollection();
    }

    /// <summary>
    /// Try to update the current collection's layout.
    /// </summary>
    public override void UpdateCollection()
    {
        if (_lockUpdatesCount <= 0)
        {
            this.CellWidth = this.ItemContainerSize.x;
            this.CellHeight = this.ItemContainerSize.y;
            this.UpdateListContainerSize();
            base.UpdateCollection();
        }
    }
    #endregion

    #region Private Functions
    /// <summary>
    /// Update the entire container size.
    /// </summary>
    private void UpdateListContainerSize()
    {
        float xItems = 0;
        float yItems = 0;
        float totalItems = _itemToContainer.Count;
        float columns = 0;
        float rows = 0;

        if (Layout == LayoutOrder.RowThenColumn)
        {
            rows = this.Rows;
            columns = rows == 0 ? 0 : Mathf.Ceil(totalItems / rows);
        }
        else
        {
            columns = this.Columns;
            rows = columns == 0 ? 0 : Mathf.Ceil(totalItems / columns);
        }

        switch (this.Layout)
        {
            case LayoutOrder.ColumnThenRow:
            case LayoutOrder.RowThenColumn:
                xItems = columns;
                yItems = rows;
                break;

            case LayoutOrder.Horizontal:
                xItems = totalItems;
                yItems = 1;
                break;

            case LayoutOrder.Vertical:
                xItems = 1;
                yItems = totalItems;
                break;

            default:
                Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  "Unsupported layout order. Can't calculate list container size.");
                break;
        }

        this.ContainerSize = 
            this.ContainerPadding +
            new Vector2(xItems * _itemContainerSize.x, yItems * _itemContainerSize.y);

        this.ContainerItemSize = new Vector2(xItems, yItems);
    }


    /// <summary>
    /// Overriding base function for laying out all the children when UpdateCollection is called.
    /// </summary>
    protected override void LayoutChildren()
    {
        var nodeGrid = new Vector3[NodeList.Count];
        Vector3 newPos;

        // Now lets lay out the grid
        HalfCell = new Vector2(CellWidth * 0.5f, CellHeight * 0.5f);

        // First start with a grid then project onto surface
        ResolveGridItemsInVerticalLayout(nodeGrid, Layout);

        switch (SurfaceType)
        {
            case ObjectOrientationSurfaceType.Plane:
                for (int i = 0; i < NodeList.Count; i++)
                {
                    ObjectCollectionNode node = NodeList[i];
                    newPos = nodeGrid[i];
                    newPos.z = Distance;
                    node.Transform.localPosition = newPos;
                    UpdateNodeFacing(node);
                    NodeList[i] = node;
                }
                break;

            case ObjectOrientationSurfaceType.Cylinder:
                for (int i = 0; i < NodeList.Count; i++)
                {
                    ObjectCollectionNode node = NodeList[i];
                    newPos = VectorExtensions.CylindricalMapping(nodeGrid[i], Radius);
                    node.Transform.localPosition = newPos;
                    UpdateNodeFacing(node);
                    NodeList[i] = node;
                }
                break;

            case ObjectOrientationSurfaceType.Sphere:

                for (int i = 0; i < NodeList.Count; i++)
                {
                    ObjectCollectionNode node = NodeList[i];
                    newPos = VectorExtensions.SphericalMapping(nodeGrid[i], Radius);
                    node.Transform.localPosition = newPos;
                    UpdateNodeFacing(node);
                    NodeList[i] = node;
                }
                break;

            case ObjectOrientationSurfaceType.Radial:
                int curColumn = 0;
                int curRow = 1;
                int columns = 0;
                int rows = 0;

                if (Layout == LayoutOrder.RowThenColumn)
                {
                    rows = this.Rows;
                    columns = rows == 0 ? 0 : Mathf.CeilToInt((float)NodeList.Count / rows);
                }
                else
                {
                    columns = this.Columns;
                    rows = columns == 0 ? 0 : Mathf.CeilToInt((float)NodeList.Count / columns);
                }

                for (int i = 0; i < NodeList.Count; i++)
                {
                    ObjectCollectionNode node = NodeList[i];
                    newPos = VectorExtensions.RadialMapping(nodeGrid[i], RadialRange, Radius, curRow, rows, curColumn, columns);

                    if (curColumn == (columns - 1))
                    {
                        curColumn = 0;
                        ++curRow;
                    }
                    else
                    {
                        ++curColumn;
                    }

                    node.Transform.localPosition = newPos;
                    UpdateNodeFacing(node);
                    NodeList[i] = node;
                }
                break;
        }
    }

    protected void ResolveGridItemsInVerticalLayout(Vector3[] grid, LayoutOrder order)
    {
        int cellCounter = 0;
        int xMax, yMax;

        int columns = 0;
        int rows = 0;

        if (Layout == LayoutOrder.RowThenColumn)
        {
            rows = this.Rows;
            columns = rows == 0 ? 0 : Mathf.CeilToInt((float)NodeList.Count / rows);
        }
        else
        {
            columns = this.Columns;
            rows = columns == 0 ? 0 : Mathf.CeilToInt((float)NodeList.Count / columns);
        }

        switch (order)
        {
            case LayoutOrder.Vertical:
                xMax = 1;
                yMax = NodeList.Count;
                break;
            case LayoutOrder.Horizontal:
                xMax = NodeList.Count;
                yMax = 1;
                break;
            case LayoutOrder.ColumnThenRow:
            case LayoutOrder.RowThenColumn:
            default:
                xMax = columns;
                yMax = rows;
                break;
        }

        float startOffsetX = (xMax * 0.5f) * CellWidth;
        float startOffsetY = (yMax * 0.5f) * CellHeight;

        if (order == LayoutOrder.ColumnThenRow)
        {
            for (int y = 0; y < yMax; y++)
            {
                for (int x = 0; x < xMax; x++)
                {
                    SetItemPostion(grid, cellCounter++, x, y, startOffsetX, startOffsetY);
                }
            }
        }
        else
        {
            for (int x = 0; x < xMax; x++)
            {
                for (int y = 0; y < yMax; y++)
                {
                    SetItemPostion(grid, cellCounter++, x, y, startOffsetX, startOffsetY);
                }
            }
        }
    }

    private void SetItemPostion(Vector3[] grid, int cell, int xPos, int yPos, float offsetX = 0, float offsetY = 0)
    {
        if (cell >= NodeList.Count || cell >= grid.Length)
        {
            return;
        }

        grid[cell].Set(
            (-offsetX + (xPos * CellWidth) + HalfCell.x) + NodeList[cell].Offset.x,
            (offsetY - (yPos * CellHeight) - HalfCell.y) + NodeList[cell].Offset.y,
            0.0f);
    }
    #endregion Private Functions
}
