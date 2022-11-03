// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;

public class CameraCullingMaskWatcher: MonoBehaviour
{
    private const int DESIRED_CULLING_MASK = -1;

    void Awake()
    {
        Debug.Log($"Starting culling mask: {CameraCache.Main.cullingMask}");
        CameraCache.Main.cullingMask = DESIRED_CULLING_MASK;
    }

    void Update()
    {
        if (CameraCache.Main.cullingMask != DESIRED_CULLING_MASK)
        {
            Debug.Log($"Culling mask change. Current culling mask: {CameraCache.Main.cullingMask}");
            CameraCache.Main.cullingMask = DESIRED_CULLING_MASK;
        }
    }
}
