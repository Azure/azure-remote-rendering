// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEditor;
using System.IO;

public class BuildAssetBundle
{
    public static void BuildAllAssetBundles(string outputPath)
    {
        if (string.IsNullOrEmpty(outputPath))
        {
            return;
        }

        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        BuildPipeline.BuildAssetBundles(
            outputPath,
            BuildAssetBundleOptions.None,
            EditorUserBuildSettings.activeBuildTarget);
    }
}
