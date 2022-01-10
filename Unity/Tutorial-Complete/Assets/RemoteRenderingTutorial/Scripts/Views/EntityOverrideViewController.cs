// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.MixedReality.Toolkit.UI;
using TMPro;
using UnityEngine;

public class EntityOverrideViewController : BaseViewController<BaseRemoteEntityHelper>
{
    public TextMeshPro selectedEntityText;

    public Interactable hideButton;
    public Interactable selectButton;
    public Interactable seeThroughButton;
    public Interactable tintButton;
    public Interactable clearButton;

    private BaseRemoteRayCastPointerHandler raycastPointerHandler;
    private Entity selectedEntity;

    private BaseRemoteRenderedModel targetModel;

    protected override void Configure()
    {
        if (baseObject == null)
            return; //No base object found, unable to configure

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
            // Raycast pointer handler
            raycastPointerHandler = baseObject.GetComponent<BaseRemoteRayCastPointerHandler>();
            // Register event
            if (raycastPointerHandler != null)
                raycastPointerHandler.RemoteEntityClicked += SelectEntity;
        }
        else
        {
            // Unregister event
            if (raycastPointerHandler != null)
                raycastPointerHandler.RemoteEntityClicked -= SelectEntity;
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
        if (selectedEntity == null)
        {
            // Disable buttons
            hideButton.IsEnabled = false;
            selectButton.IsEnabled = false;
            seeThroughButton.IsEnabled = false;
            tintButton.IsEnabled = false;
            clearButton.IsEnabled = false;

            // All states off
            hideButton.IsToggled = false;
            selectButton.IsToggled = false;
            seeThroughButton.IsToggled = false;
            tintButton.IsToggled = false;
        }
        else
        {
            // Enable buttons
            hideButton.IsEnabled = true;
            selectButton.IsEnabled = true;
            seeThroughButton.IsEnabled = true;
            tintButton.IsEnabled = true;
            clearButton.IsEnabled = true;

            // Match buttons to states
            hideButton.IsToggled = baseObject.GetState(selectedEntity, HierarchicalStates.Hidden) ==
                                   HierarchicalEnableState.ForceOn;
            selectButton.IsToggled = baseObject.GetState(selectedEntity, HierarchicalStates.Selected) ==
                                     HierarchicalEnableState.ForceOn;
            seeThroughButton.IsToggled = baseObject.GetState(selectedEntity, HierarchicalStates.SeeThrough) ==
                                         HierarchicalEnableState.ForceOn;
            tintButton.IsToggled = baseObject.GetState(selectedEntity, HierarchicalStates.UseTintColor) ==
                                   HierarchicalEnableState.ForceOn;
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

    public void HideButtonInteraction()
    {
        if (CheckValidEntity()) baseObject.ToggleHidden(selectedEntity);
    }

    public void SelectButtonInteraction()
    {
        if (CheckValidEntity()) baseObject.ToggleSelect(selectedEntity);
    }

    public void SeeThroughButtonInteraction()
    {
        if (CheckValidEntity()) baseObject.ToggleSeeThrough(selectedEntity);
    }

    public void TintButtonInteraction()
    {
        if (CheckValidEntity()) baseObject.ToggleTint(selectedEntity);
    }

    public void ClearButtonInteraction()
    {
        if (CheckValidEntity())
        {
            baseObject.RemoveOverrides(selectedEntity);
            // Clear button states
            hideButton.IsToggled = false;
            selectButton.IsToggled = false;
            seeThroughButton.IsToggled = false;
            tintButton.IsToggled = false;
        }
    }
}
