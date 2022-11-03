// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.UI;
using System;
using UnityEngine;

/// <summary>
/// Represents a menu item with a data source attached
/// </summary>
public class ListItem : MonoBehaviour
{
    private object _dataSource;
    private int _index = -1;
    private bool _visible = true;
    private bool _selected = false;
    private WeakReference<ListItemRepeater> _parent;
    private Transform _lastSeenParent;
    private float _lastInvoke = float.MinValue;
    private float _invokeDelay = 1f;


    #region Serialized Fields
    [Header("Item Properties")]

    [SerializeField]
    [Tooltip("The size of the list item.")]
    private Vector2 itemSize = new Vector2(0.034f, 0.034f);

    /// <summary>
    /// The size of the list item.
    /// </summary>
    public Vector2 ItemSize
    {
        get => itemSize;
        set => itemSize = value;
    }

    [SerializeField]
    [Tooltip("The padding between item and selection container.")]
    private Vector2 selectionContainerPadding = new Vector2(0.004f, 0.004f);

    /// <summary>
    /// The padding between item and selection container.
    /// </summary>
    public Vector2 SelectionContainerPadding
    {
        get => selectionContainerPadding;
        set => selectionContainerPadding = value;
    }

    [Header("Item Components")]

    [SerializeField]
    [Tooltip("The button transforms whose width and height will be modified to match the 'Item Size'.")]
    private Transform[] sizableTransforms = new Transform[0];

    /// <summary>
    /// The button transforms whose width and height will be modified to match the 'Item Size'.
    /// </summary>
    public Transform[] SizableTransforms
    {
        get => sizableTransforms;
        set => sizableTransforms = value;
    }

    [SerializeField]
    [Tooltip("The button transforms whose width and height will be modified to undo scaling applied to root.")]
    private Transform[] invertSizableTransforms = new Transform[0];

    /// <summary>
    /// The button transforms whose width and height will be modified to undo scaling applied to root.
    /// </summary>
    public Transform[] InvertSizableTransforms
    {
        get => invertSizableTransforms;
        set => invertSizableTransforms = value;
    }

    [SerializeField]
    [Tooltip("The button transforms whose width and height will be modified with selection changes.")]
    private Transform[] selectionSizableTransforms = new Transform[0];

    /// <summary>
    /// The button transforms whose width and height will be modified with selection changes.
    /// </summary>
    public Transform[] SelectionSizableTransforms
    {
        get => selectionSizableTransforms;
        set => selectionSizableTransforms = value;
    }

    [SerializeField]
    [Tooltip("The button colliders whose width and height will be modified to match the 'Item Size'.")]
    private BoxCollider[] sizableColliders = new BoxCollider[0];

    /// <summary>
    /// The button colliders whose width and height will be modified to match the 'Item Size'.
    /// </summary>
    public BoxCollider[] SizableColliders
    {
        get => sizableColliders;
        set => sizableColliders = value;
    }

    [SerializeField]
    [Tooltip("The button mesh whose width and height will be modified.")]
    private MeshFilter sizableMeshFilter = null;

    /// <summary>
    /// The button mesh whose width and height will be modified.
    /// </summary>
    public MeshFilter SizableMeshFilter
    {
        get => sizableMeshFilter;
        set => sizableMeshFilter = value;
    }

    [SerializeField]
    [Tooltip("The main button interaction.")]
    private Interactable buttonInteractable = null;

    /// <summary>
    /// The main button interaction.
    /// </summary>
    public Interactable ButtonInteractable
    {
        get => buttonInteractable;
        set => buttonInteractable = value;
    }

    [SerializeField]
    [Tooltip("The selection UX container object, displayed when item is selected.")]
    private Transform selectionTransform = null;

    /// <summary>
    ///The selection UX container object, displayed when item is selected.
    /// </summary>
    public Transform SelectionTransform
    {
        get => selectionTransform;
        set => selectionTransform = value;
    }

    [Header("Item Events")]

    [SerializeField]
    [Tooltip("Raised when the list item is selected.")]
    private ListItemInvokedEvent selectionChanged = new ListItemInvokedEvent();
    
    /// <summary>
    /// Raised when the list item is selected.
    /// </summary>
    public ListItemInvokedEvent SelectionChanged => selectionChanged;

    [SerializeField]
    [Tooltip("Raised when list item was clicked, pressed or invoked somehow.")]
    private ListItemInvokedEvent invokedEvent = new ListItemInvokedEvent();
    
    /// <summary>
    /// Raised when list item was clicked, pressed or invoked somehow.
    /// </summary>
    public ListItemInvokedEvent InvokedEvent => invokedEvent;
    #endregion Serialized Fields

    #region MonoBehavior Functions
    protected virtual void OnValidate()
    {
        if (!Application.isPlaying)
        {
            this.UpdateComponentSizes();
        }
    }

    protected virtual void Start()
    {
        if (this.buttonInteractable == null)
        {
            this.buttonInteractable = GetComponentInChildren<Interactable>();
        }

        this.UpdateComponentSizes();
        this.UpdateSelectionContainerVisibility();
        this.AddEventHandlers();
    }
    
    protected virtual void OnDisable()
    {
        this.Selected = false;
    }

    protected virtual void OnDestroy()
    {
        this.RemoveEventHandlers();
    }
    #endregion MonoBehavior Functions

    #region Public Properties
    /// <summary>
    /// Get or set the list parent
    /// </summary>
    public ListItemRepeater Parent
    {
        get
        {
            ListItemRepeater result = null;
            _parent?.TryGetTarget(out result);
            return result;
        }

        protected set
        {
            _parent = new WeakReference<ListItemRepeater>(value);
        }
    }

    /// <summary>
    /// The data source backing this item
    /// </summary>
    public object DataSource
    {
        get
        {
            return _dataSource;
        }

        protected set
        {
            if (_dataSource != value)
            {
                var oldValue = value;
                _dataSource = value;
                this.OnDataSourceChanged(oldValue, _dataSource);

                foreach (var handler in GetEventHandlers())
                {
                    handler.OnDataSourceChanged(this, oldValue, _dataSource);
                }
            }
        }
    }

    /// <summary>
    /// Get the total item size
    /// </summary>
    public Vector2 TotalItemSize
    {
        get
        {
            return itemSize + selectionContainerPadding;
        }
    }

    /// <summary>
    /// Get the index of this menu item
    /// </summary>
    public int Index
    {
        get
        {
            return _index;
        }

        protected set
        {
            if (_index != value)
            {
                int oldValue = _index;
                _index = value;
                this.OnIndexChanged(oldValue, _index);

                foreach (var handler in GetEventHandlers())
                {
                    handler.OnIndexChanged(this, oldValue, _index);
                }
            }
        }
    }

    /// <summary>
    /// Get if this item is visible in the list
    /// </summary>
    public bool Visible
    {
        get
        {
            return _visible;
        }

        protected set
        {
            if (_visible != value)
            {
                _visible = value;
                this.OnVisibilityChanged();
            }
        }
    }

    /// <summary>
    /// Get the item that is selected the list
    /// </summary>
    public bool Selected
    {
        get
        {
            return _selected;
        }

        protected set
        {
            if (_selected != value)
            {
                _selected = value;
                this.OnSelectionChanged();
            }
        }
    }
    #endregion Public Properties

    /// <summary>
    /// Reset list item sizes
    /// </summary>
    public void ResetSize()
    {
        UpdateComponentSizes();
    }

    /// <summary>
    /// Set the parent backing this item
    /// </summary>
    public void SetParent(ListItemRepeater list)
    {
        this.Parent = list;
    }

    /// <summary>
    /// Set the data source backing this item
    /// </summary>
    public void SetDataSource(System.Object value)
    {
        this.DataSource = value;
    }

    /// <summary>
    /// Set if this item is visible within the list.
    /// </summary>
    public void SetVisibility(bool visible)
    {
        this.Visible = visible;
    }

    /// <summary>
    /// Set if this item is selected within the list.
    /// </summary>
    public void SetSelection(bool selected)
    {
        this.Selected = selected;
    }

    /// <summary>
    /// Set the index value of this item
    /// </summary>
    public void SetIndex(int index)
    {
        this.Index = index;
    }

    /// <summary>
    /// Called with the menu item was invoked, clicked, or pressed.
    /// </summary>
    public void Invoked()
    {
        float timeSinceLastInvoke = Time.time - _lastInvoke;
        if (this.Visible && timeSinceLastInvoke >= _invokeDelay)
        {
            this.OnInvoked();
            this.invokedEvent?.Invoke(this);
            _lastInvoke = Time.time;
        }
    }

    /// <summary>
    /// Find all event handler components
    /// </summary>
    protected ListItemEventHandler[] GetEventHandlers()
    {
        return this.gameObject.GetComponents<ListItemEventHandler>() ?? new ListItemEventHandler[0];
    }

    /// <summary>
    /// Handle data source changes.
    /// </summary>
    protected virtual void OnDataSourceChanged(System.Object oldValue, System.Object newValue)
    {
    }

    /// <summary>
    /// Handle index changes
    /// </summary>
    protected virtual void OnIndexChanged(int oldValue, int newValue)
    {
    }

    /// <summary>
    /// Handle visibility changes.
    /// </summary>
    protected virtual void OnVisibilityChanged()
    {
        foreach (var handler in GetEventHandlers())
        {
            handler.OnVisibilityChanged(this);
        }
    }

    /// <summary>
    /// Handle selection changes.
    /// </summary>
    protected virtual void OnSelectionChanged()
    {
        this.UpdateSelectionSizableTransforms();
        this.UpdateSelectionContainerVisibility();
        foreach (var handler in GetEventHandlers())
        {
            handler.OnSelectionChanged(this);
        }

        SelectionChanged?.Invoke(this);
    }

    /// <summary>
    /// Handle focus changes.
    /// </summary>
    protected virtual void OnFocusChanged()
    {
        foreach (var handler in GetEventHandlers())
        {
            handler.OnFocusChanged(this);
        }
    }

    /// <summary>
    /// Handle invocation.
    /// </summary>
    protected virtual void OnInvoked()
    {
        foreach (var handler in GetEventHandlers())
        {
            handler.OnInvoked(this);
        }
    }

    /// <summary>
    /// Update button components
    /// </summary>
    private void UpdateComponentSizes()
    {
        if (itemSize.x == 0 || itemSize.y == 0)
        {
            return;
        }

        if (sizableTransforms != null)
        {
            foreach (var sizable in sizableTransforms)
            {
                if (sizable == null)
                {
                    continue;
                }

                sizable.localScale = new Vector3(itemSize.x, itemSize.y, sizable.transform.localScale.z);
            }
        }

        if (sizableColliders != null)
        {
            foreach (var collider in sizableColliders)
            {
                if (collider == null)
                {
                    continue;
                }

                collider.size = new Vector3(itemSize.x, itemSize.y, collider.size.z);
            }
        }

        if (buttonInteractable != null)
        {
            var nearInteractable = buttonInteractable.GetComponent<NearInteractionTouchable>();
            var nearCollider = buttonInteractable.GetComponent<BoxCollider>();
            if (nearInteractable != null && nearCollider != null)
            {
                nearInteractable.SetTouchableCollider(nearCollider);
            }
        }

        this.UpdateSelectionSizableTransforms();

        if (selectionTransform != null)
        {
            Vector3 selectionSize = new Vector3(
                itemSize.x + this.selectionContainerPadding.x,
                itemSize.y + this.selectionContainerPadding.y,
                selectionTransform.localScale.z);

            selectionTransform.localScale = selectionSize;
        }

        if (sizableMeshFilter != null)
        {
            Mesh mesh = sizableMeshFilter.sharedMesh;
            if (mesh != null && mesh.bounds.size.x != 0 && mesh.bounds.size.y != 0)
            {
                Vector3 meshSize = new Vector3(
                    itemSize.x / mesh.bounds.size.x,
                    itemSize.y / mesh.bounds.size.y,
                    sizableMeshFilter.transform.localScale.z);

                sizableMeshFilter.transform.localScale = meshSize;
            }
        }
    }

    /// <summary>
    /// Add button event handlers
    /// </summary>
    private void AddEventHandlers()
    {
        if (buttonInteractable != null)
        {
            buttonInteractable.OnClick.AddListener(this.Invoked);
        }
    }

    /// <summary>
    /// Remove button event handlers
    /// </summary>
    private void RemoveEventHandlers()
    {
        if (buttonInteractable != null)
        {
            buttonInteractable.OnClick.RemoveListener(this.Invoked);
        }
    }

    /// <summary>
    /// Show container if selected, otherwise hide it.
    /// </summary>
    private void UpdateSelectionContainerVisibility()
    {
        if (this.selectionTransform == null)
        {
            return;
        }

        this.selectionTransform.gameObject.SetActive(this.Selected);
    }

    /// <summary>
    /// Update transforms that change size with selection state.
    /// </summary>
    private void UpdateSelectionSizableTransforms()
    {
        Vector2 size = this.itemSize;
        if (this.Selected)
        {
            size += this.selectionContainerPadding;
        }

        Vector2 invertSize = new Vector2(1.0f / size.x, 1.0f / size.y);

        if (selectionSizableTransforms != null)
        {
            foreach (var sizable in selectionSizableTransforms)
            {
                if (sizable == null)
                {
                    continue;
                }

                sizable.localScale = new Vector3(size.x, size.y, sizable.transform.localScale.z);
            }
        }

        if (invertSizableTransforms != null)
        {
            foreach (var invertSizable in invertSizableTransforms)
            {
                if (invertSizable == null)
                {
                    continue;
                }

                invertSizable.localScale = new Vector3(invertSize.x, invertSize.y, invertSizable.transform.localScale.z);
            }
        }
    }
}
