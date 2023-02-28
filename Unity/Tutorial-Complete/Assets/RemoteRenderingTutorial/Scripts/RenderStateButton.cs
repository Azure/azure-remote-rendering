// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class RenderStateButton : MonoBehaviour
{
    public delegate void ButtonAction();

    public enum ButtonState
    {
        NotReady,
        Ready,
        Working,
        Active
    }

    public TextMeshPro buttonText;
    public Interactable disconnectButton;

    public ButtonAction AdvanceStateAction { get; set; }
    public ButtonAction ReverseStateAction { get; set; }

    private ButtonState CurrentButtonState { get; set; }

    public GameObject notReadyIcon;
    public GameObject readyIcon;
    public GameObject workingIcon;
    public GameObject activeIcon;

    public UnityEvent OnNotReady;
    public UnityEvent OnReady;
    public UnityEvent OnWorking;
    public UnityEvent OnActive;

    public void SetState(ButtonState state)
    {
        this.CurrentButtonState = state;
        DisableIcons();
        switch (state)
        {
            case ButtonState.NotReady:
                OnNotReady?.Invoke();
                notReadyIcon.SetActive(true);
                break;
            case ButtonState.Ready:
                OnReady?.Invoke();
                readyIcon.SetActive(true);
                break;
            case ButtonState.Working:
                OnWorking?.Invoke();
                workingIcon.SetActive(true);
                break;
            case ButtonState.Active:
                OnActive?.Invoke();
                activeIcon.SetActive(true);
                break;
        }
        
        if(disconnectButton != null) disconnectButton.IsEnabled = state == ButtonState.Active;
    }

    private void DisableIcons()
    {
        notReadyIcon.SetActive(false);
        readyIcon.SetActive(false);
        workingIcon.SetActive(false);
        activeIcon.SetActive(false);
    }

    public void SetText(string text)
    {
        if (buttonText != null)
            buttonText.text = text;
    }

    public void ButtonInteractionAdvance()
    {
        if (CurrentButtonState == ButtonState.Ready)
        {
            AdvanceStateAction?.Invoke();
        }
    }

    public void ButtonInteractionReverse()
    {
        if (CurrentButtonState == ButtonState.Active)
        {
            ReverseStateAction?.Invoke();
        }
    }
}