// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

public class RemoteCutPlaneViewController : BaseViewController<BaseRemoteCutPlane>
{
    public RemoteCutPlaneViewControllerHelper helperPrefab;
    private RemoteCutPlaneViewControllerHelper helperInstance;

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
            baseObject.CutPlaneReadyChanged += OnCutPlaneReadyChanged;
            OnCutPlaneReadyChanged(baseObject.CutPlaneReady);
        }
        else
        {
            // Unregister event
            if(baseObject != null) baseObject.CutPlaneReadyChanged -= OnCutPlaneReadyChanged;
            // Destroy helper
            if(helperInstance != null) Destroy(helperInstance.gameObject);
            helperInstance = null;
        }
    }

    private void OnCutPlaneReadyChanged(bool ready)
    {
        // Match toggle and helper states to ready state
        if (ready != menuButton.IsToggled) menuButton.IsToggled = ready;
        helperInstance.gameObject.SetActive(ready);
    }

    public void ToggleInteraction()
    {
        // Enable/disable cut plane based on toggle interaction
        if (menuButton.IsToggled)
        {
            baseObject.CreateCutPlane();
        }
        else
        {
            baseObject.DestroyCutPlane();
        }
    }
}