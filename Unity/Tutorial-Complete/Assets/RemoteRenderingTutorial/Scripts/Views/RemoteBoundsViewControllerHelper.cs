// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Experimental.UI;
using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;
#if UNITY_WSA
using UnityEngine.XR.WSA;

#endif

[RequireComponent(typeof(ObjectManipulator))]
[RequireComponent(typeof(BoundingBox))]
public class RemoteBoundsViewControllerHelper : ViewControllerHelper<BaseRemoteBounds>
{
    private ObjectManipulator objectManipulator;
    private BoundingBox boundingBox;
    private bool isLocked = true;

    private ObjectManipulatorEventsRedirect redirect;

    private ManipulationHandFlags manipulationFlags;

    private void Awake()
    {
        objectManipulator = GetComponent<ObjectManipulator>();
        boundingBox = GetComponent<BoundingBox>();
    }

    public override void Initialize(BaseRemoteBounds source)
    {
        // Clean up collider that's automatically created on enable
        Destroy(boundingBox.TargetBounds);
        // Override the bounding box to point to the model's remote bounds
        boundingBox.BoundsOverride = source.BoundsBoxCollider;
        boundingBox.Target = source.gameObject;
        objectManipulator.HostTransform = source.transform;
        // Store object manipulator flags for re-enabling
        manipulationFlags = objectManipulator.ManipulationType;
        // Redirect pointer events to object manipulator
        redirect = source.gameObject.AddComponent<ObjectManipulatorEventsRedirect>();
        redirect.redirectTarget = objectManipulator;
    }

    public void Unlock()
    {
        boundingBox.Active = true;
        isLocked = false;
        boundingBox.CreateRig();
        // Enable object manipulator
        objectManipulator.enabled = true;
        objectManipulator.ManipulationType = manipulationFlags;

#if UNITY_WSA
        Destroy(gameObject.GetComponent<WorldAnchor>());
#endif
    }

    public void Lock()
    {
        boundingBox.Active = false;
        isLocked = true;
        // Disable object manipulator
        objectManipulator.enabled = false;
        objectManipulator.ManipulationType = 0;

#if UNITY_WSA
        gameObject.EnsureComponent<WorldAnchor>();
#endif
    }

    public void ToggleLock()
    {
        if (isLocked)
            Unlock();
        else
            Lock();
    }

    private void OnDestroy()
    {
        if(redirect != null) Destroy(redirect);
    }
}