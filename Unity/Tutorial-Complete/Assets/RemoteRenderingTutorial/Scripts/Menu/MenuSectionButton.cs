// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine;

[RequireComponent(typeof(Interactable))]
public class MenuSectionButton : MonoBehaviour
{
    public GameObject menuSection;
    
    private Interactable interactable;
    public Interactable Interactable
    {
        get
        {
            if (interactable == null) interactable = GetComponent<Interactable>();
            return interactable;
        }
    }

    public event Action OnButtonPressed;

    public void ButtonPressed() {
        OnButtonPressed?.Invoke();
    }

    public void SetSelected(bool selected)
    {
        menuSection.SetActive(selected);
        Interactable.IsToggled = selected;
    }
}
