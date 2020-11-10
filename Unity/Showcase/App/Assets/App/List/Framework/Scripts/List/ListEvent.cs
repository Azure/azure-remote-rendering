// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine.Events;
using System;

/// <summary>
/// An event that is raised by the custom list control.
/// </summary>
[Serializable]
public class ListEvent : UnityEvent<ListEventData>
{
}

/// <summary>
/// An event that is raised when a custom list item control is clicked.
/// </summary>
[Serializable]
public class ListItemInvokedEvent : UnityEvent<ListItem>
{
}
