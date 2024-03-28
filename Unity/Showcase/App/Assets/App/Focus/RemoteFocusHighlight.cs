// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;

using UnityEngine;

using Microsoft.Azure.RemoteRendering;
using Microsoft.Azure.RemoteRendering.Unity;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.Showcase.App.Pointers;

/// <summary>
/// This behavior will apply a selection outline around ARR entities that are focused by a user. For this class to 
/// function properly, the application must be using a MRTK focus provider that implements IRemoteFocusProvider.
/// </summary>
public class RemoteFocusHighlight : InputSystemGlobalHandlerListener, IMixedRealityFocusChangedHandler
{
    Entity _root = null;
    Dictionary<uint, Entity> _pointerFocusedEntity = new Dictionary<uint, Entity>();
    Dictionary<Entity, HashSet<uint>> _entityIdSelectedCounts = new Dictionary<Entity, HashSet<uint>>();
    Dictionary<Entity, bool> _pendingSelection = new Dictionary<Entity, bool>();
    HashSet<Entity> _highlighting = new HashSet<Entity>();

    #region Serialized Fields
    [SerializeField]
    [Tooltip("Is the whole object being focused, or just a piece.")]
    private bool focusingWholeObject = true;

    /// <summary>
    /// Is the whole object being focused, or just a piece.
    /// </summary>
    public bool FocusingWholeObject
    {
        get => focusingWholeObject;
        set
        {
            if (focusingWholeObject != value)
            {
                focusingWholeObject = value;
                SetRootSelectionFlags();
            }
        }
    }

    [SerializeField]
    [Tooltip("The settings for the focus highlighting.")]
    private RemoteFocusHighlightSettings settings = RemoteFocusHighlightSettings.Default;

    /// <summary>
    /// The settings for the focus highlighting.
    /// </summary>
    public RemoteFocusHighlightSettings Settings
    {
        get => settings;
        set => settings = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Methods
    /// <summary>
    /// Check if any remote Entity focus has changed and
    /// commit the final set of selection highlighting changes.
    /// </summary>
    private void LateUpdate()
    {
        CheckFocusEntityUpdate();
        CommitSelectionChanges();
    }

    /// <summary>
    /// Remove the highlights from the highlighted items.
    /// </summary>
    protected override void OnDisable()
    {
        base.OnDisable();

        _pendingSelection.Clear();

        foreach (var highlighting in _highlighting)
        {
            _pendingSelection.Add(highlighting, false);
        }

        CommitSelectionChanges();
    }
    #endregion MonoBehavior Methods
    #region InputSystemGlobalHandlerListener Methods
    /// <summary>
    /// Register for global focus events, as children may hide these events. 
    /// </summary>
    protected override void RegisterHandlers()
    {
        CoreServices.InputSystem?.RegisterHandler<IMixedRealityFocusChangedHandler>(this);
    }

    /// <summary>
    /// Unregister for global focus events, as children may hide these events. 
    /// </summary>
    protected override void UnregisterHandlers()
    {
        CoreServices.InputSystem?.UnregisterHandler<IMixedRealityFocusChangedHandler>(this);
    }
    #endregion InputSystemGlobalHandlerListener Methods

    #region IMixedRealityFocusChangedHandler Methods
    /// <inheritdoc/>
    public void OnBeforeFocusChange(FocusEventData eventData)
    {
    }

    /// <inheritdoc/>
    public void OnFocusChanged(FocusEventData eventData)
    {
        // Ignore if this is no remote pointer, or this is disabled
        if (transform == null || !isActiveAndEnabled || !(eventData.Pointer is IRemotePointer remotePointer))
        {
            return;
        }

        UpdatePointerFocus(remotePointer, eventData.NewFocusedObject);
    }
    #endregion IMixedRealityFocusChangedHandler Methods

    #region Private Methods
    /// <summary>
    /// <see cref="OnFocusChanged"/> is only invoked when the focues <see cref="GameObject"/> changes,
    /// but not if the <see cref="IRemotePointer.FocusEntityTarget"/> does. We therefore have to
    /// manually check for any <see cref="IRemotePointer.FocusEntityTarget"/> changes.
    /// </summary>
    private void CheckFocusEntityUpdate()
    {
        foreach (IRemotePointer pointer in CoreServices.InputSystem.FocusProvider.GetPointers<IRemotePointer>())
        {
            UpdatePointerFocus(pointer, CoreServices.InputSystem.FocusProvider.GetFocusedObject(pointer));
        }
    }

    private void UpdatePointerFocus(IRemotePointer pointer, GameObject focusedObject)
    {
        _pointerFocusedEntity.TryGetValue(pointer.PointerId, out Entity oldEntity);
        Entity newEntity = (focusedObject != null && focusedObject.transform.IsChildOf(transform)) ? pointer.FocusEntityTarget : null;

        if (oldEntity != newEntity)
        {
            if (newEntity != null)
            {
                _pointerFocusedEntity[pointer.PointerId] = newEntity;
            }
            else
            {
                _pointerFocusedEntity.Remove(pointer.PointerId);
            }

            UpdateSelectionCount(oldEntity, pointer, false);
            UpdateSelectionCount(newEntity, pointer, true);

            SetChildSelectionFlags(oldEntity);
            SetChildSelectionFlags(newEntity);
            SetRootSelectionFlags();
        }
    }

    /// <summary>
    /// Set the highlighted property.
    /// </summary>
    private void SetChildSelectionFlags(Entity entity)
    {
        if (entity == null)
        {
            return;
        }

        int currentCount = GetSelectionCount(entity);
        _pendingSelection[entity] = currentCount > 0;
    }

    /// <summary>
    /// Set the highlighted property on the root entity.
    /// </summary>
    private void SetRootSelectionFlags()
    {
        if (_root == null || !_root.Valid)
        {
            _root = GetComponentInChildren<RemoteEntitySyncObject>()?.Entity;
        }

        if (_root == null || _entityIdSelectedCounts.ContainsKey(_root))
        {
            return;
        }

        _pendingSelection[_root] = _entityIdSelectedCounts.Count > 0;
    }

    /// <summary>
    /// Apply all pending selection changes.
    /// </summary>
    private void CommitSelectionChanges()
    {
        if (_pendingSelection.Count == 0)
        {
            return;
        }
        // First decide if child highlighting should be disabled
        bool wholeObject = _root != null && _pendingSelection.TryGetValue(_root, out wholeObject);
        var highlightSettings = settings.GetSettings(focusingWholeObject && wholeObject);
        var tintColor = highlightSettings.TintColor.toRemote();

        // Next update highlighting flags
        foreach (var entry in _pendingSelection)
        {
            if (!entry.Key.Valid || entry.Key == null)
            {
                continue;
            }

            HierarchicalStateOverrideComponent overrides = entry.Key.EnsureComponentOfType<HierarchicalStateOverrideComponent>();
            if (overrides == null)
            {
                continue;
            }

            overrides.SelectedState = HierarchicalEnableState.InheritFromParent;
            overrides.UseTintColorState = HierarchicalEnableState.InheritFromParent;

            if (entry.Value)
            {
                _highlighting.Add(entry.Key);
                var flags = GetHighlightFlag(entry.Key, highlightSettings);

                if ((flags & HierarchicalStates.Selected) != 0)
                {
                    overrides.SelectedState = HierarchicalEnableState.ForceOn;
                }

                if ((flags & HierarchicalStates.UseTintColor) != 0)
                {
                    overrides.UseTintColorState = HierarchicalEnableState.ForceOn;
                }
            }
            else
            {
                _highlighting.Remove(entry.Key);
            }

            if (overrides.UseTintColorState == HierarchicalEnableState.ForceOn &&
                (overrides.TintColor.Bytes != tintColor.Bytes))
            {
                overrides.TintColor = tintColor;
            }
        }
        _pendingSelection.Clear();
    }

    /// <summary>
    /// Either increase (selected == true) or decrease (selected == false) the number of pointers that are focusing a
    /// given entity. This function will also track the pointer which is focusing, so to later avoid having a "dead"
    /// pointer highlighting (i.e. selecting) an entity.
    /// </summary>
    private int UpdateSelectionCount(Entity entity, IMixedRealityPointer pointer, bool selected)
    {
        if (entity == null || pointer == null)
        {
            return 0;
        }

        HashSet<uint> pointerIds = null;
        _entityIdSelectedCounts.TryGetValue(entity, out pointerIds);

        if (selected)
        {
            if (pointerIds == null)
            {
                _entityIdSelectedCounts[entity] = pointerIds = new HashSet<uint>();
            }
            pointerIds.Add(pointer.PointerId);
        }
        else if (pointerIds != null)
        {
            pointerIds.Remove(pointer.PointerId);
            PurgePointerIds(pointerIds);
            if (pointerIds.Count == 0)
            {
                _entityIdSelectedCounts.Remove(entity);
            }
        }

        return pointerIds == null ? 0 : pointerIds.Count;
    }

    /// <summary>
    /// Obtain the number of pointers currently focusing a given entity.
    /// </summary>
    private int GetSelectionCount(Entity entity)
    {
        if (entity == null)
        {
            return 0;
        }

        int count = 0;
        HashSet<uint> pointerIds = null;
        if (_entityIdSelectedCounts.TryGetValue(entity, out pointerIds))
        {
            count = pointerIds.Count;
        }

        return count;
    }

    /// <summary>
    /// Remove pointer ids of pointers that no longer exist, from the given hash set.
    /// </summary>
    private void PurgePointerIds(HashSet<uint> ids)
    {
        HashSet<uint> removeIds = new HashSet<uint>(ids);
        if (removeIds.Count == 0)
        {
            return;
        }

        foreach (var pointer in PointerUtils.GetPointers())
        {
            removeIds.Remove(pointer.PointerId);
        }

        foreach (var removeId in removeIds)
        {
            ids.Remove(removeId);
        }
    }

    /// <summary>
    /// Get the highlight flag for the current entity
    /// </summary>
    private HierarchicalStates GetHighlightFlag(Entity entity, RemoteFocusHighlightSettings.Settings settings)
    {
        HierarchicalStates flags = HierarchicalStates.None;

        if (settings.Edges == RemoteFocusHighlightType.Piece && entity != _root)
        {
            flags |= HierarchicalStates.Selected;
        }
        else if (settings.Edges == RemoteFocusHighlightType.Whole && entity == _root)
        {
            flags |= HierarchicalStates.Selected;
        }

        if (settings.Tint == RemoteFocusHighlightType.Piece && entity != _root)
        {
            flags |= HierarchicalStates.UseTintColor;
        }
        else if (settings.Tint == RemoteFocusHighlightType.Whole && entity == _root)
        {
            flags |= HierarchicalStates.UseTintColor;
        }

        return flags;
    }
    #endregion Private Methods
}
