// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.MixedReality.Toolkit.UI;
using System.Linq;
using TMPro;
using UnityEngine;

public class EntityMaterialViewController : BaseViewController<BaseEntityMaterialController>
{
    public TextMeshPro selectedEntityText;

    public Interactable tintButton;
    public Interactable roughnessButton;
    public PinchSlider roughnessSlider;
    public Interactable clearButton;

    public Interactable[] colorButtons;
    private Color[] buttonColorReference;

    private Entity selectedEntity;
    private bool roughnessAvailable;

    private BaseRemoteRenderedModel targetModel;

    protected override void Configure()
    {
        if (baseObject == null)
            return; //No base object found, unable to configure

        // Material controller settings
        baseObject.RevertOnEntityChange = false;
        // Init color palette
        buttonColorReference = new Color[colorButtons.Length];
        // Register color buttons
        for (int c = 0; c < colorButtons.Length; c++)
        {
            int color = c;
            colorButtons[c].OnClick.AddListener(() => ColorButtonInteraction(color));
            buttonColorReference[c] = colorButtons[c].GetComponentInChildren<Renderer>().material.color;
        }

        // Register model events
        targetModel = baseObject.GetComponent<BaseRemoteRenderedModel>();
        targetModel.ModelStateChange += OnModelStateChange;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (targetModel != null)
        {
            targetModel.ModelStateChange -= OnModelStateChange;
        }
    }

    private void OnModelStateChange(ModelState state)
    {
        // Clear entity selection if model changes to non-ready state
        if (state != ModelState.Ready) SelectEntity(null);
    }

    protected override void SetViewEnabled(bool viewEnabled)
    {
        base.SetViewEnabled(viewEnabled);
        // Register events once we're enabled
        if (viewEnabled)
        {
            // Register event
            baseObject.TargetEntityChanged += SelectEntity;
        }
        else
        {
            // Unregister event
            if (baseObject != null) baseObject.TargetEntityChanged -= SelectEntity;
            // Clear entity selection
            SelectEntity(null);
        }
    }

    private void SelectEntity(Entity entity)
    {
        // Update selection
        selectedEntity = entity;
        selectedEntityText.text = selectedEntity == null ? "No entity selected" : selectedEntity.Name;
        // Update buttons
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        // If any of the MRTK Interactables is not set or already destroyed on shutdown skip update
        if (tintButton == null || roughnessButton == null || clearButton == null || roughnessSlider == null || colorButtons.Any((Interactable b) => b == null))
        {
            return;
        }
        if (selectedEntity == null)
        {
            // Disable buttons
            tintButton.IsEnabled = false;
            roughnessButton.IsEnabled = false;
            clearButton.IsEnabled = false;

            // All states off
            tintButton.IsToggled = false;
            roughnessButton.IsToggled = false;
            clearButton.IsToggled = false;

            // Sliders set to zero
            roughnessSlider.SliderValue = 0f;

            // All colors off
            foreach (var colorButton in colorButtons)
            {
                colorButton.IsEnabled = false;
                colorButton.IsToggled = false;
            }
        }
        else
        {
            // Store if we have roughness overrides available
            roughnessAvailable = baseObject.RoughnessOverride != null;

            // Enable buttons
            tintButton.IsEnabled = true;
            roughnessButton.IsEnabled = roughnessAvailable;
            clearButton.IsEnabled = true;

            // Match buttons to states
            tintButton.IsToggled = baseObject.ColorOverride.OverrideActive;
            roughnessButton.IsToggled = roughnessAvailable && baseObject.RoughnessOverride.OverrideActive;

            // Slider values
            roughnessSlider.SliderValue = roughnessAvailable ? baseObject.RoughnessOverride.OverrideValue : 0f;

            // Color buttons
            for (var c = 0; c < colorButtons.Length; c++)
            {
                var colorButton = colorButtons[c];
                colorButton.IsEnabled = true;
                colorButton.IsToggled = buttonColorReference[c] == baseObject.ColorOverride.OverrideValue;
            }
        }
    }

    private bool CheckValidEntity()
    {
        // No entity selected
        if (selectedEntity == null) return false;
        // Entity is still valid
        if (selectedEntity.Valid) return true;
        // Entity no longer valid, clear selection
        SelectEntity(null);
        return false;
    }

    public void TintButtonInteraction()
    {
        if (CheckValidEntity()) baseObject.ColorOverride.OverrideActive = tintButton.IsToggled;
    }

    public void RoughnessButtonInteraction()
    {
        if (roughnessAvailable && CheckValidEntity())
            baseObject.RoughnessOverride.OverrideActive = roughnessButton.IsToggled;
    }

    public void RoughnessSliderInteraction(SliderEventData args)
    {
        if (roughnessAvailable && CheckValidEntity()) baseObject.RoughnessOverride.OverrideValue = args.NewValue;
    }

    private void ColorButtonInteraction(int color)
    {
        if (CheckValidEntity())
        {
            for (int c = 0; c < colorButtons.Length; c++)
            {
                bool selectedColor = (c == color);
                // Toggle button based on selected color
                colorButtons[c].IsToggled = selectedColor;
                // Set selected color
                if (selectedColor) baseObject.ColorOverride.OverrideValue = buttonColorReference[color];
            }
        }
    }

    public void ClearButtonInteraction()
    {
        if (CheckValidEntity())
        {
            baseObject.Revert();
            UpdateButtonStates();
        }
    }
}
