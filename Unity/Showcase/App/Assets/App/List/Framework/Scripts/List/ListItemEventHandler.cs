// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

/// <summary>
/// A abstract MonoBehavior that can be implemented to handle various list item events.
/// </summary>
public abstract class ListItemEventHandler : MonoBehaviour
{
    /// <summary>
    /// Handle data source changes.
    /// </summary>
    public virtual void OnDataSourceChanged(ListItem item, System.Object oldValue, System.Object newValue)
    {
    }

    /// <summary>
    /// Handle index changes.
    /// </summary>
    public virtual void OnIndexChanged(ListItem item, int oldValue, int newValue)
    {
    }

    /// <summary>
    /// Handle visibility changes.
    /// </summary>
    public virtual void OnVisibilityChanged(ListItem item)
    {
    }

    /// <summary>
    /// Handle selection changes.
    /// </summary>
    public virtual void OnSelectionChanged(ListItem item)
    {
    }

    /// <summary>
    /// Handle focus changes.
    /// </summary>
    public virtual void OnFocusChanged(ListItem item)
    {
    }

    /// <summary>
    /// Handle invocation.
    /// </summary>
    public virtual void OnInvoked(ListItem item)
    {
    }
}
