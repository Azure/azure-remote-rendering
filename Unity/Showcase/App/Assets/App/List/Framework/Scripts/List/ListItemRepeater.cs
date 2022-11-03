// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections;
using System.Collections.Generic;
using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;

/// <summary>
/// The main list component that manages creating new list items and placing them within a list container.
/// </summary>
public class ListItemRepeater : MonoBehaviour
{
    private List<ListItem> _listItems;
    private bool _listItemsInvalid;
    private bool _started;
    private bool _appQuitting;

    #region Serialize Fields
    [Header("List Data")]

    [SerializeField]
    [Tooltip("A list of data to render within the list container.")]
    private IList<object> dataSource = null;

    /// <summary>
    /// A list of data to render within the list container.
    /// </summary>
    public IList<object> DataSource 
    {
        get => dataSource;

        set
        {
            if (dataSource != value)
            {
                dataSource = value;
                CreateItems();
                dataSourceChanged?.Invoke(new ListEventData(gameObject, listItem: null, listItemIndex: -1));
            }
        }
    }

    [Header("List Layout")]

    [SerializeField]
    [Tooltip("The size of the visible region in the list.")]
    private Vector2 listSize = new Vector2(0.25f, 0.25f);

    /// <summary>
    /// The size of the visible region in the list.
    /// </summary>
    public Vector2 ListSize
    {
        get => listSize;
        set => listSize = value;
    }

    [SerializeField]
    [Tooltip("The padding around the visible region of the list.")]
    private Vector2 listPadding = new Vector2(0.001f, 0.001f);

    /// <summary>
    /// The padding around the visible region of the list.
    /// </summary>
    public Vector2 ListPadding
    {
        get => listPadding;
        set => listPadding = value;
    }

    [Header("List Item Layout")]

    [SerializeField]
    [Tooltip("The prefab for the list item. The will be one instance of this prefab per 'Data Source' entry.")]
    private GameObject listItemPrefab = null;

    /// <summary>
    /// The prefab for the list item. The will be one instance of this prefab per 'Data Source' entry.
    /// </summary>
    public GameObject ListItemPrefab
    {
        get => listItemPrefab;
        set => listItemPrefab = value;
    }

    [SerializeField]
    [Tooltip("The padding around each of the list items.")]
    private Vector2 listItemPadding = new Vector2(0.001f, 0.001f);

    /// <summary>
    /// The padding around each of the list items.
    /// </summary>
    public Vector2 ListItemPadding
    {
        get => listItemPadding;
        set => listItemPadding = value;
    }

    [Header("List Parts")]

    [SerializeField]
    [Tooltip("The list container that'll hold list items. If null, a container will be searched for on this game object.")]
    private ListContainer listContainer = null;

    /// <summary>
    /// The list container that'll hold list items. If null, a container will be searched for on this game object.
    /// </summary>
    public ListContainer ListContainer
    {
        get => listContainer;
        set => listContainer = value;
    }

    [SerializeField]
    [Tooltip("The game object clipping utility. If null, a clipping utility will be searched for on this game object.")]
    private ClippingUtility clippingUtility = null;

    /// <summary>
    /// The game object clipping utility. If null, a clipping utility will be searched for on this game object.
    /// </summary>
    public ClippingUtility ClippingUtility
    {
        get => clippingUtility;
        set => clippingUtility = value;
    }

    [SerializeField]
    [Tooltip("The clipping primitive that'll actually clip objects. If null, a clipping primitive will be searched for on this game object.")]
    private ClippingPrimitive clippingPrimitive = null;

    /// <summary>
    /// The game object clipping utility. If null, a clipping primitive will be searched for on this game object.
    /// </summary>
    public ClippingPrimitive ClippingPrimitive
    {
        get => clippingPrimitive;
        set => clippingPrimitive = value;
    }

    [SerializeField]
    [Tooltip("The list scroller. If null, a list scroller will be searched for on this game object.")]
    private ListScrollerBase listScroller = null;

    /// <summary>
    /// The list scroller. If null, a list scroller will be searched for on this game object.
    /// </summary>
    public ListScrollerBase ListScroller
    {
        get => listScroller;
        set => listScroller = value;
    }

    [SerializeField]
    [Tooltip("The list dragging behavior.  If null, a drag value will be searched for on this game object.")]
    private DragValue listDragInput = null;

    /// <summary>
    /// The list dragging behavior.  If null, a drag value will be searched for on this game object.
    /// </summary>
    public DragValue ListDragInput
    {
        get => listDragInput;
        set => listDragInput = value;
    }

    [SerializeField]
    [Tooltip("The list back plate area.")]
    private GameObject listBackground = null;

    /// <summary>
    /// The game object clipping utility. If null, a clipping primitive will be searched for on this game object.
    /// </summary>
    public GameObject ListBackground
    {
        get => listBackground;
        set => listBackground = value;
    }

    [Header("List Event")]

    [SerializeField]
    [Tooltip("Event raised when a list data source changes.")]
    private ListEvent dataSourceChanged = new ListEvent();

    /// <summary>
    /// Event raised when a list data source changes.
    /// </summary>
    public ListEvent DataSourceChanged => dataSourceChanged;

    [SerializeField]
    [Tooltip("Event raised when a list item object is being created.")]
    private ListEvent listItemCreating = new ListEvent();

    /// <summary>
    /// Event raised when a list item object is being created.
    /// </summary>
    public ListEvent ListItemCreating => listItemCreating;

    [SerializeField]
    [Tooltip("Event raised when a list item object was created.")]
    private ListEvent listItemCreated = new ListEvent();

    /// <summary>
    /// Event raised when a list item object was created.
    /// </summary>
    public ListEvent ListItemCreated => listItemCreated;

    [SerializeField]
    [Tooltip("Event raised when a list item object was destroyed.")]
    private ListEvent listItemDestroyed = new ListEvent();

    /// <summary>
    /// Event raised when a list item object was destroyed.
    /// </summary>
    public ListEvent ListItemDestroyed => listItemDestroyed;

    [SerializeField]
    [Tooltip("Event raised when selection changes.")]
    private ListEvent selectionChanged = new ListEvent();

    /// <summary>
    /// Event raised when selection changes.
    /// </summary>
    public ListEvent SelectionChanged => selectionChanged;
    #endregion Serialize Fields

    #region Public Properties
    /// <summary>
    /// Get the selected item
    /// </summary>
    public ListItem Selected { get; private set; }
    

    /// <summary>
    /// Get the size of the data source
    /// </summary>
    public int Count => dataSource == null ? 0 : dataSource.Count;
    #endregion Public Properties

    #region MonoBehavior Functions
    /// <summary>
    /// Create the initial set of list objects.
    /// </summary>
    private void Start()
    {
        _started = true;

        if (listContainer == null)
        {
            listContainer = GetComponent<ListContainer>();
        }

        if (clippingUtility == null)
        {
            clippingUtility = GetComponent<ClippingUtility>();
        }

        if (clippingPrimitive == null)
        {
            clippingPrimitive = GetComponent<ClippingPrimitive>();
        }

        if (listScroller == null)
        {
            listScroller = GetComponent<ListScrollerBase>();
        }

        if (listDragInput == null)
        {
            listDragInput = GetComponent<DragValue>();
        }

        InvalidateItems();
    }

    /// <summary>
    /// Track the quitting of the application
    /// </summary>
    private void OnApplicationQuit()
    {
        _appQuitting = true;
    }
    #endregion MonoBehavior Functions

    #region Public Functions
    /// <summary>
    /// Is the given list item a child of this list.
    /// </summary>
    public bool IsChild(ListItem listItem)
    {
        if (listItem == null)
        {
            return false;
        }

        return listContainer?.Contains(listItem.transform) == true;
    }

    /// <summary>
    /// Is the given list item actually a child of a list repeater.
    /// </summary>
    public static bool IsListRepeaterChild(ListItem listItem)
    {
        if (listItem == null)
        {
            return false;
        }

        ListItemRepeater  listRepeater = listItem.GetComponentInParent<ListItemRepeater>();
        return listRepeater?.IsChild(listItem) == true;
    }

    /// <summary>
    /// Get the list item at the current index.
    /// </summary>
    public ListItem GetItem(int index)
    {
        if (_listItems == null || index < 0 || index >= _listItems.Count)
        {
            return null;
        }

        return _listItems[index];
    }
    #endregion Public Functions

    #region Private Functions
    /// <summary>
    /// Invalid items and delay update.
    /// </summary>
    private void InvalidateItems()
    {
        _listItemsInvalid = true;
        StartCoroutine(DelayCreateItemsIfInvalid());
    }

    /// <summary>
    /// Create items if the item set is still invalid.
    /// </summary>
    private IEnumerator DelayCreateItemsIfInvalid()
    {
         yield return 0;
        if (_listItemsInvalid)
        {
            CreateItems();
        }
    }

    /// <summary>
    /// Create new list items, and attach to the container object.
    /// </summary>
    private void CreateItems()
    {
        if (!_started || _appQuitting)
        {
            return;
        }

        listContainer?.StartUpdate();
        DestroyItems();
        _listItems = new List<ListItem>();
        _listItemsInvalid = false;

        if (listContainer != null)
        {
            int index = 0;
            Vector2 itemSize = Vector2.zero;

            if (dataSource != null && listItemPrefab != null)
            {
                int count = dataSource.Count;
                for (int i = 0; i < count; i++)
                {
                    var data = dataSource[i];
                    var listItemGameObject = GameObject.Instantiate(listItemPrefab);
                    listContainer.Insert(listItemGameObject.transform, false);

                    var listItem = listItemGameObject.GetComponent<ListItem>();
                    if (listItem != null)
                    {
                        listItemCreating?.Invoke(new ListEventData(gameObject, listItem, listItemIndex: i));
                        listItem.SetParent(this);
                        listItem.SetDataSource(data);
                        listItem.SetIndex(index++);
                        listItem.SelectionChanged.AddListener(ListItemSelectionChanged);
                        _listItems.Add(listItem);

                        // capture the item size
                        itemSize = listItem.TotalItemSize;
                    }
                }
            }

            listContainer.ContainerPadding = this.listPadding;
            listContainer.ItemContainerSize = itemSize + this.listItemPadding;
        }

        listContainer?.EndUpdate();
        var totalListSize = this.listSize + this.listPadding;

        if (listDragInput != null)
        {
            listDragInput.DragStartDistance = 0;
            listDragInput.DragEndDistance = Vector3.Dot(listDragInput.GetSliderAxis(), this.listSize);
        }

        if (this.clippingPrimitive != null)
        {
            this.clippingPrimitive.transform.localScale = new Vector3(
                this.listSize.x, this.listSize.y, this.clippingPrimitive.transform.localScale.z);
        }

        if (this.listBackground != null)
        {
            this.listBackground.transform.localScale = new Vector3(
                totalListSize.x, totalListSize.y, this.listBackground.transform.localScale.z);
        }

        if (listScroller != null)
        {
            // Update the scrollable region size.
            listScroller.SetScrollSize(this.listSize);

            // Reset scroller position
            listScroller.SnapTo(0.0f);
        }

        // Turn on clipping after children have been added.
        clippingUtility?.UpdateClippedChildren();
        if (clippingPrimitive != null)
        {
            clippingPrimitive.enabled = true;
        }

        CreatedItems();
    }

    /// <summary>
    /// Called once new items have been created
    /// </summary>
    private void CreatedItems()
    {
        int count = _listItems.Count;
        for (int i = 0; i < count; i++)
        {
            var listItem = _listItems[i];
            listItemCreated?.Invoke(new ListEventData(gameObject, listItem, listItemIndex: i));
        }
    }

    /// <summary>
    /// Destroy all item within the container object.
    /// </summary>
    private void DestroyItems()
    {
        if (clippingPrimitive != null)
        {
            clippingPrimitive.enabled = false;
        }

        listContainer?.DestoryAll();

        if (_listItems != null)
        {
            int count = _listItems.Count;
            for (int i = 0; i < count; i++)
            {
                var listItem = _listItems[i];
                listItem?.SelectionChanged.RemoveListener(ListItemSelectionChanged);
                listItemDestroyed?.Invoke(new ListEventData(gameObject, listItem, listItemIndex: i));
            }
            _listItems = null;
        }
    }

    /// <summary>
    /// Handle a list item's selection change, and deselected the last selected item.
    /// </summary>
    private void ListItemSelectionChanged(ListItem listItem)
    {
        if (listItem == null)
        {
            return;
        }

        bool selected = listItem.Selected;
        if (selected && Selected != listItem)
        {
            Selected?.SetSelection(false);
            int selectedIndex = _listItems.IndexOf(listItem);
            Selected = selectedIndex >= 0 && selectedIndex < _listItems.Count ? _listItems[selectedIndex] : null;
            Selected?.SetSelection(true);
            SelectionChanged?.Invoke(new ListEventData(gameObject, Selected, listItemIndex: selectedIndex));
        }
        else if (!selected && Selected == listItem)
        {
            Selected?.SetSelection(false);
            Selected = null;
            SelectionChanged?.Invoke(new ListEventData(gameObject, Selected, listItemIndex: - 1));
        }
    }
    #endregion Private Functions
}
