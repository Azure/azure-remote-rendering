// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

public class MenuController : MonoBehaviour
{
    public MenuSectionButton[] menuSectionButtons;

    private void Awake() {
        // Register button events
        foreach(MenuSectionButton menuButton in menuSectionButtons)
        {
            menuButton.OnButtonPressed += () => SelectSection(menuButton);
        }
    }

    private void Start()
    {
        SelectSection(menuSectionButtons[0]);
    }

    public void SelectSection(MenuSectionButton selectedSectionButton)
    {
        // Select section corresponding to pressed button, deselect all other sections
        foreach (MenuSectionButton sectionButton in menuSectionButtons)
        {
            sectionButton.SetSelected(sectionButton == selectedSectionButton);
        }
    }
}