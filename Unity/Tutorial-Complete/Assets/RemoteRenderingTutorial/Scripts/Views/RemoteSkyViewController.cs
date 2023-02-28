// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.Utilities;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

public class RemoteSkyViewController : BaseViewController<BaseRemoteSky>
{
    // Buttons
    public SkyboxButton[] skyButtons;
    private Dictionary<string, SkyboxButton> buttonLookup = new Dictionary<string, SkyboxButton>();

    protected override void Configure()
    {
        // Wire up skybox buttons
        foreach (var skyButton in skyButtons)
        {
            if (!buttonLookup.ContainsKey(skyButton.skyboxName))
            {
                skyButton.Interactable.OnClick.AddListener(() =>
                {
                    if (baseObject.CanSetSky) baseObject.SetSky(skyButton.skyboxName);
                });
                buttonLookup.Add(skyButton.skyboxName, skyButton);
            }
        }
    }

    protected override void SetViewEnabled(bool viewEnabled)
    {
        base.SetViewEnabled(viewEnabled);
        // Register events once we're enabled
        if (viewEnabled)
        {
            // Register for event
            baseObject.SkyChanged += OnSkyChanged;
            OnSkyChanged(baseObject.CurrentSky);
        }
        else
        {
            // Unregister event
            if(baseObject != null) baseObject.SkyChanged -= OnSkyChanged;
        }
    }

    private void OnSkyChanged(string selected)
    {
        // When there's a new selection, set everything to not toggled and then toggle just the selected
        foreach (var button in skyButtons)
        {
            button.Interactable.IsToggled = false;
        }

        SkyboxButton skyboxButton;
        if(buttonLookup.TryGetValue(selected, out skyboxButton))
            skyboxButton.Interactable.IsToggled = true;
    }
}
