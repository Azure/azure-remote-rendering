// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine;

public class RemoteBoundsViewController : BaseViewController<BaseRemoteBounds>
{
    // Helpers
    public RemoteBoundsViewControllerHelper helperPrefab;
    private RemoteBoundsViewControllerHelper helperInstance;

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
            baseObject.BoundsStateChange += BoundsStateChange;
            BoundsStateChange(baseObject.CurrentBoundsState);
        }
        else
        {
            // Unregister events
            if(baseObject != null) baseObject.BoundsStateChange -= BoundsStateChange;
            // Destroy helper
            if(helperInstance != null) Destroy(helperInstance.gameObject);
            helperInstance = null;
        }
    }

    private void BoundsStateChange(RemoteBoundsState state)
    {
        switch (state)
        {
            case RemoteBoundsState.NotReady:
            case RemoteBoundsState.Error:
            case RemoteBoundsState.Updating:
                menuButton.IsToggled = true; // Lock
                menuButton.IsEnabled = false;
                UpdateLockState();
                break;
            case RemoteBoundsState.Ready:
                menuButton.IsToggled = false; // Unlock
                menuButton.IsEnabled = true;
                UpdateLockState();
                break;
        }
    }

    private void UpdateLockState()
    {
        if (menuButton.IsToggled)
        {
            helperInstance.Lock();
        }
        else
        {
            helperInstance.Unlock();
        }
    }

    public void ToggleLockInteraction()
    {
        UpdateLockState();
    }
}