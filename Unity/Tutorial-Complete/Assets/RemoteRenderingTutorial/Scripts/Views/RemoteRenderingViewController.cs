// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using static RenderStateButton;

public class RemoteRenderingViewController : MonoBehaviour
{
    public MenuController menuController;
    public MenuSectionButton sessionMenuButton;
    public MenuSectionButton sessionToolsMenuButton;

    public RenderStateButton InitializeButton;

    public RenderStateButton RemoteSessionButton;

    public RenderStateButton RuntimeButton;

    public TextMeshPro DeviceCodeInstructions;

    public void Start()
    {
        RemoteRenderingCoordinator.CoordinatorStateChange += ApplyStateToView;
        ApplyStateToView(RemoteRenderingCoordinator.instance.CurrentCoordinatorState);

        //Set the actions that build up the ARR stack
        InitializeButton.AdvanceStateAction = () => RemoteRenderingCoordinator.instance.InitializeARR();
        RemoteSessionButton.AdvanceStateAction = () => RemoteRenderingCoordinator.instance.JoinRemoteSession();
        RuntimeButton.AdvanceStateAction = () => RemoteRenderingCoordinator.instance.ConnectRuntimeToRemoteSession();

        RemoteSessionButton.ReverseStateAction = () => RemoteRenderingCoordinator.instance.StopRemoteSession();
        RuntimeButton.ReverseStateAction = () => RemoteRenderingCoordinator.instance.DisconnectRuntimeFromRemoteSession();

        var auth = RemoteRenderingCoordinator.instance.GetComponent<BaseARRAuthentication>();
        if (auth != null)
        {
            auth.AuthenticationInstructions += (instructions) =>
            {
                DeviceCodeInstructions.gameObject.SetActive(true);
                DeviceCodeInstructions.text = instructions;
            };
            
        }
        DeviceCodeInstructions.gameObject.SetActive(false);
    }

    /// <summary>
    /// Configure the view to match the supplied state
    /// </summary>
    /// <param name="state">The state to match the current view to</param>
    private void ApplyStateToView(RemoteRenderingCoordinator.RemoteRenderingState state)
    {
        DisableAllButtons();
        switch (state)
        {
            case RemoteRenderingCoordinator.RemoteRenderingState.NotInitialized:
                InitializeButton.SetState(ButtonState.Ready);
                break;

            case RemoteRenderingCoordinator.RemoteRenderingState.NotAuthorized:
                InitializeButton.SetState(ButtonState.Active);
                break;

            case RemoteRenderingCoordinator.RemoteRenderingState.NoSession:
                InitializeButton.SetState(ButtonState.Active);
                RemoteSessionButton.SetState(ButtonState.Ready);
                break;

            case RemoteRenderingCoordinator.RemoteRenderingState.ConnectingToExistingRemoteSession:
                InitializeButton.SetState(ButtonState.Active);
                RemoteSessionButton.SetState(ButtonState.Working);
                RemoteSessionButton.SetText("Connecting to existing Remote Session");
                break;
            case RemoteRenderingCoordinator.RemoteRenderingState.ConnectingToNewRemoteSession:
                InitializeButton.SetState(ButtonState.Active);
                RemoteSessionButton.SetState(ButtonState.Working);
                RemoteSessionButton.SetText("Connecting to new Remote Session");
                break;

            case RemoteRenderingCoordinator.RemoteRenderingState.RemoteSessionReady:
                InitializeButton.SetState(ButtonState.Active);
                RemoteSessionButton.SetState(ButtonState.Active);
                RemoteSessionButton.SetText("Connected to new Remote Session");
                RuntimeButton.SetState(ButtonState.Ready);
                break;

            case RemoteRenderingCoordinator.RemoteRenderingState.ConnectingToRuntime:
                menuController.SelectSection(sessionMenuButton);
                InitializeButton.SetState(ButtonState.Active);
                RemoteSessionButton.SetState(ButtonState.Active);
                RuntimeButton.SetState(ButtonState.Working);
                break;

            case RemoteRenderingCoordinator.RemoteRenderingState.RuntimeConnected:
                sessionToolsMenuButton.Interactable.IsEnabled = true;
                InitializeButton.SetState(ButtonState.Active);
                RemoteSessionButton.SetState(ButtonState.Active);
                RuntimeButton.SetState(ButtonState.Active);
                break;
        }
        
        // Update menu buttons based on session state
        bool runtimeConnected = state == RemoteRenderingCoordinator.RemoteRenderingState.RuntimeConnected;
        menuController.SelectSection(sessionMenuButton);
        sessionToolsMenuButton.Interactable.IsEnabled = runtimeConnected;
    }

    private void DisableAllButtons()
    {
        
        InitializeButton.SetState(ButtonState.NotReady);
        RemoteSessionButton.SetState(ButtonState.NotReady);
        RuntimeButton.SetState(ButtonState.NotReady);

        DeviceCodeInstructions?.gameObject.SetActive(false);
    }
}
