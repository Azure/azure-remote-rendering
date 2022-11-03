// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// This helper is used to ensure the scene's main camera has the proper cull
/// </summary>
/// <remarks>
/// This is a work around for an Azure Remote Rendering SDK bug. Azure Remote Rendering is setting to the culling mask to 
/// 0x7FFFFFFF, not 0xFFFFFFFF, when playing in the Unity Editor. This work around should be removed once Azure Remote
/// Rendering is fixed.
/// </remarks>
public class FixMainCameraCullingMask : IProcessSceneWithReport
{
    public int callbackOrder => 0;

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        var roots = scene.GetRootGameObjects();
        foreach (var gameObject in roots)
        {
            var cameras = gameObject.GetComponentsInChildren<Camera>(includeInactive: true);
            foreach (var camera in cameras)
            {
                UpdateGameCameraCullingMask(camera);
            }
        }
    }

    private void UpdateGameCameraCullingMask(Camera camera)
    {
        if (camera.cameraType == CameraType.Game)
        {
            // culling mask should be everything
            camera.cullingMask = ~0;
        }
    }
}
