// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PointerToolIcon : MonoBehaviour
{
    public GameObject iconVisuals;
    public HideMenuMovement hideMenu;

    public bool HandMenuStateShow { get; set; } = true;

    private void Update()
    {
        bool hideMenuState = hideMenu == null || hideMenu.IsVisible;
        iconVisuals.SetActive(HandMenuStateShow && !AppDialog.DialogOpen && hideMenuState);
    }
}
