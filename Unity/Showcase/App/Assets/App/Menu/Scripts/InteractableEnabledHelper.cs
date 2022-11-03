// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine;

/// <summary>
/// Helper class to fix setting IsEnabled on an interactable before its Awake function has run, which sets IsEnabled to
/// enabledOnStart.
/// </summary>
[RequireComponent(typeof(Interactable))]
public class InteractableEnabledHelper:MonoBehaviour
{
    private bool enabledOnStartOverride;

    private Interactable interactable;
    public Interactable Interactable
    {
        get
        {
            if(interactable == null)
            {
                interactable = GetComponent<Interactable>();
            }

            return interactable;
        }
    }

    public bool IsEnabled
    {
        set
        {
            enabledOnStartOverride = value;
            Interactable.IsEnabled = value;
        }
    }

    private void Start()
    {
        Interactable.IsEnabled = enabledOnStartOverride;
    }
}