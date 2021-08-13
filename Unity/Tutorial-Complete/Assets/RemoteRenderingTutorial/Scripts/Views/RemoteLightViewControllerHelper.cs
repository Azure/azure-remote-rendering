// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Experimental.UI;
using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine;
#if UNITY_WSA
using UnityEngine.XR.ARFoundation;
#endif

[RequireComponent(typeof(ObjectManipulator))]
public class RemoteLightViewControllerHelper : ViewControllerHelper<BaseRemoteLight>
{
    private ObjectManipulator objectManipulator;

    public void Awake()
    {
        objectManipulator = GetComponent<ObjectManipulator>();
    }

    public override void Initialize(BaseRemoteLight remoteLight)
    {
        transform.localPosition = Vector3.zero;
        transform.localEulerAngles = new Vector3(-90f, 0f, 0f);
        objectManipulator.HostTransform = remoteLight.transform;
    }

    private void OnEnable()
    {
#if UNITY_WSA
        gameObject.EnsureComponent<ARAnchor>(); 
#endif
    }

    private void OnDisable()
    {
#if UNITY_WSA
        Destroy(gameObject.GetComponent<ARAnchor>());
#endif
    }
}
