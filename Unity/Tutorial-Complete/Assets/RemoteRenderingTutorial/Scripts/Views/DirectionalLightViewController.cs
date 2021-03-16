// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine;

public class DirectionalLightViewController : BaseViewController<BaseRemoteLight>
{
    // Buttons
    public Interactable toggleLockButton;
    public Interactable[] colorButtons;
    private Color[] buttonColorReference;

    // Helpers
    public RemoteLightViewControllerHelper helperPrefab;
    private RemoteLightViewControllerHelper helperInstance;

    protected override void Configure()
    {
        // Init color palette
        buttonColorReference = new Color[colorButtons.Length];
        // Register color buttons
        for (int c = 0; c < colorButtons.Length; c++)
        {
            int color = c;
            colorButtons[c].OnClick.AddListener(() => ColorButtonInteraction(color));
            buttonColorReference[c] = colorButtons[c].GetComponentInChildren<Renderer>().material.color;
        }
    }

    protected override BaseRemoteLight FindBaseObject()
    {
        BaseRemoteLight foundObject = null;
        // Find the first directional light that meets our requirements
        foreach (var remoteLight in FindObjectsOfType<BaseRemoteLight>())
        {
            if (remoteLight.RemoteLightType == Microsoft.Azure.RemoteRendering.ObjectType.DirectionalLightComponent)
            {
                foundObject = remoteLight;
                break;
            }
        }

        return foundObject;
    }

    protected override void SetViewEnabled(bool viewEnabled)
    {
        base.SetViewEnabled(viewEnabled);
        // Register events once we're enabled
        if (viewEnabled)
        {
            // Create helper
            helperInstance = Instantiate(helperPrefab, baseObject.transform);
            helperInstance.Initialize(baseObject);
            // Register event
            baseObject.LightReadyChanged += OnLightReadyChanged;
            OnLightReadyChanged(baseObject.LightReady);
        }
        else
        {
            // Unregister event
            if (baseObject != null) baseObject.LightReadyChanged -= OnLightReadyChanged;
            // Destroy helper
            if (helperInstance != null) Destroy(helperInstance.gameObject);
            helperInstance = null;
        }
    }

    private void OnLightReadyChanged(bool ready)
    {
        // Match toggle and helper states to ready state
        if (ready != toggleLockButton.IsToggled) toggleLockButton.IsToggled = ready;
        helperInstance.gameObject.SetActive(ready);
    }

    public void ToggleLockInteraction()
    {
        // Enable/disable cut plane based on toggle interaction
        helperInstance.gameObject.SetActive(toggleLockButton.IsToggled);
    }

    public void IntensitySliderInteraction(SliderEventData args)
    {
        baseObject?.SetIntensity(args.NewValue);
    }

    private void ColorButtonInteraction(int color)
    {
        for (int c = 0; c < colorButtons.Length; c++)
        {
            bool selectedColor = (c == color);
            // Toggle button based on selected color
            colorButtons[c].IsToggled = selectedColor;
            // Set selected color
            if (selectedColor) baseObject.SetColor(buttonColorReference[color]);
        }
    }
}
