// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using UnityEngine;

public class PointerToolIcon : MonoBehaviour
{
    public GameObject iconVisuals;
    public HideMenuMovement hideMenu;

    public bool HandMenuStateShow { get; set; } = true;

    private void Update()
    {
        bool hideMenuState = hideMenu == null || hideMenu.IsVisible;
        iconVisuals.SetActive(HandMenuStateShow && !AppServices.AppNotificationService.IsDialogOpen && hideMenuState);
    }
}
