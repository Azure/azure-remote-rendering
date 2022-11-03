// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

/// <summary>
/// A hide operation that can be canceled to show the element that was hidden.
/// </summary>
public class AppSettingsHideOperation
{
    Action _show = null;

    public AppSettingsHideOperation(Action show)
    {
        _show = show;
    }

    /// <summary>
    /// Show the item that was hidden. Show will succeed if there are no other active hide requests.
    /// </summary>
    public void Cancel()
    {
        _show?.Invoke();
    }
}
