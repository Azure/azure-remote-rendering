// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Experimental.UI;
using UnityEngine;

[RequireComponent(typeof(ObjectManipulator))]
public class RemoteCutPlaneViewControllerHelper : ViewControllerHelper<BaseRemoteCutPlane>
{
    private ObjectManipulator objectManipulator;

    public void Awake()
    {
        objectManipulator = GetComponent<ObjectManipulator>();
    }

    public override void Initialize(BaseRemoteCutPlane remoteCutPlane)
    {
        objectManipulator.HostTransform = remoteCutPlane.transform;
    }
}
