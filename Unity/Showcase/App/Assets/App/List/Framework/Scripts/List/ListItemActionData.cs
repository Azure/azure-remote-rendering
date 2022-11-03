// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;
/// <summary>
/// When in a list and list item prefab has the ListItemActionButtonData behavior, 
/// this class indicates that the action button functionality should be used.
/// An "action button" is a special item that when click executes the action
/// defined by this class.
/// </summary>
public class ListItemActionData
{
    private Action _action;

    /// <summary>
    /// Get or set the action primary label.
    /// </summary>
    public string PrimaryLabel { get; set; }

    /// <summary>
    /// Get or set the action secondary label.
    /// </summary>
    public string SecondaryLabel { get; set; }

    /// <summary>
    /// Get or the set the fancy icon type.
    /// </summary>
    public FancyIconType IconType { get; set; }

    /// <summary>
    /// Get or set the icon prefab.
    /// </summary>
    public GameObject IconOverridePrefab { get; set; }

    public ListItemActionData(Action action)
    {
        _action = action ?? throw new ArgumentNullException();
    }    

    /// <summary>
    /// Excute the action.
    /// </summary>
    public void Execute()
    {
        _action?.Invoke();
    }
}
