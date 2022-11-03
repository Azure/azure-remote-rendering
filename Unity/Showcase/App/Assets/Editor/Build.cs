// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEditor;

public class Build
{
    public static void ConfigurePlayerUWP()
    {
        Console.WriteLine("Configuring build for UWP.");

        string buildLocation = "./../../../../Bin/Unity/ShowcaseApp";
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; ++i)
        {
            if (args[i].Equals("-buildLocation", StringComparison.InvariantCultureIgnoreCase))
            {
                if (i < args.Length - 1)
                {
                    buildLocation = args[i + 1];
                    Console.WriteLine("Build location set to '{0}'.", buildLocation);
                }
            }
        }

        string vsTargetVersion = Environment.GetEnvironmentVariable("ARR_UNITY_VS_TARGET_VERSION");
        EditorUserBuildSettings.wsaUWPVisualStudioVersion = vsTargetVersion;
        Console.WriteLine("Using Visual Studio installation version '{0}'.", vsTargetVersion);

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();

        buildPlayerOptions.scenes = new string[]{ "Assets/Scenes/SampleScene.unity" };
        // The location path name is specified relative to the Unity project folder!
        buildPlayerOptions.locationPathName = buildLocation;
        buildPlayerOptions.targetGroup = BuildTargetGroup.WSA;
        buildPlayerOptions.target = BuildTarget.WSAPlayer;
        buildPlayerOptions.options = BuildOptions.IncludeTestAssemblies;

        var error = BuildPipeline.BuildPlayer(buildPlayerOptions);
        if (error.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            EditorApplication.Exit(0);
        else
            EditorApplication.Exit(1);
    }
}
