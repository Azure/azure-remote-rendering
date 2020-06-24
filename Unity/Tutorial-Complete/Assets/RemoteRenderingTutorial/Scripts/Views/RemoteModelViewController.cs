// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections;
using System.Collections.Generic;
using Microsoft.MixedReality.Toolkit.UI;
using TMPro;
using UnityEngine;

public class RemoteModelViewController : BaseViewController<BaseRemoteRenderedModel>
{
    // View references
    public ProgressBar loadingProgressBar;
    public TextMeshPro modelNameDisplay;

    // Buttons
    public Interactable toggleLoadButton;
    
    protected override void SetViewEnabled(bool viewEnabled)
    {
        base.SetViewEnabled(viewEnabled);
        // Register events once we're enabled
        if (viewEnabled)
        {
            // Register event
            baseObject.ModelStateChange += OnModelStateChange;
            OnModelStateChange(baseObject.CurrentModelState);
            // Hook up the load progress bar
            baseObject.LoadProgress += (progress) => loadingProgressBar.SetProgress(progress);

            if(modelNameDisplay != null)
                modelNameDisplay.text = baseObject.ModelDisplayName;
        }
        else
        {
            // Unregister event
            if(baseObject != null) baseObject.ModelStateChange -= OnModelStateChange;
            OnModelStateChange(ModelState.NotReady);
        }
        // Match load button state to view state
        toggleLoadButton.IsEnabled = viewEnabled;
    }

    private void OnModelStateChange(ModelState state)
    {
        loadingProgressBar.Hide();
        
        switch (state)
        {
            case ModelState.NotReady:
                toggleLoadButton.IsEnabled = false;
                toggleLoadButton.IsToggled = false;
                break;
            case ModelState.Ready:
                toggleLoadButton.IsEnabled = true;
                toggleLoadButton.IsToggled = false;
                break;
            case ModelState.Loading:
                toggleLoadButton.IsEnabled = true;
                toggleLoadButton.IsToggled = true;
                // Show progress bar
                loadingProgressBar.Show();
                break;
            case ModelState.Loaded:
                toggleLoadButton.IsEnabled = true;
                toggleLoadButton.IsToggled = true;
                break;
            case ModelState.Unloading:
                toggleLoadButton.IsEnabled = false;
                toggleLoadButton.IsToggled = false;
                break;
            case ModelState.Error:
                toggleLoadButton.IsEnabled = false;
                toggleLoadButton.IsToggled = false;
                break;
        }
    }
    
    public void ToggleLoadInteraction()
    {
        if (toggleLoadButton.IsToggled)
        {
            baseObject.LoadModel();
        }
        else
        {
            baseObject.UnloadModel();
        }
    }
}
