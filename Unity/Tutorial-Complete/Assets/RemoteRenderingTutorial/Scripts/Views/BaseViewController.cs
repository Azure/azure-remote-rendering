using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine;
using Object = UnityEngine.Object;

public abstract class BaseViewController<T> : MonoBehaviour where T : Object
{
    public Interactable menuButton;

    protected T baseObject;

    private bool viewEnabled;

    protected virtual void Start()
    {
        TryFindBaseObject();
        // Start disabled
        SetViewEnabled(false);
        // Register remote coordinator event
        RemoteRenderingCoordinator.CoordinatorStateChange += OnCoordinatorStateChange;
        OnCoordinatorStateChange(RemoteRenderingCoordinator.instance.CurrentCoordinatorState);
    }

    protected virtual void OnDestroy()
    {
        RemoteRenderingCoordinator.CoordinatorStateChange -= OnCoordinatorStateChange;
    }

    protected virtual void Configure()
    {
        // Nothing to configure by default
    }

    private void OnCoordinatorStateChange(RemoteRenderingCoordinator.RemoteRenderingState state)
    {
        if (state == RemoteRenderingCoordinator.RemoteRenderingState.RuntimeConnected)
        {
            TryFindBaseObject();
            
            if (baseObject != null)
            {
                if(!viewEnabled) SetViewEnabled(true);
            }
            else
            {
                // Expected when the functionality hasn't been implmented yet
                NotificationBar.Message($"View disabled: No object of type {typeof(T)} found.");
            }
        }
    }

    private void TryFindBaseObject()
    {
        if (baseObject != null) return; // Already found
        baseObject = FindBaseObject();
        Configure();
    }

    protected virtual T FindBaseObject()
    {
        return FindObjectOfType<T>();
    }

    protected virtual void SetViewEnabled(bool setEnabled)
    {
        viewEnabled = setEnabled;
        menuButton.IsEnabled = viewEnabled;
    }
}
