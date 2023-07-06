// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using UnityEditor;

public class Build
{
    public static void ConfigurePlayerUWP()
    {
        Console.WriteLine("Configuring build for UWP.");
        ConfigurePlayer(BuildTarget.WSAPlayer, false);
    }

    public static void ConfigurePlayerQuest()
    {
        Console.WriteLine("Configuring build for Quest.");
        ConfigurePlayer(BuildTarget.Android, true);
    }

    private static void ConfigurePlayer(BuildTarget target, bool includeVREnvironment)
    {
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

        List<string> sceneList = new List<string> { "Assets/Scenes/SampleScene.unity" };
        if (includeVREnvironment)
        {
            sceneList.Add( "Assets/App/VR/Background/scenes/RoomBlue.unity");
        }

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = sceneList.ToArray();
        // The location path name is specified relative to the Unity project folder!
        buildPlayerOptions.locationPathName = buildLocation;
        buildPlayerOptions.targetGroup = BuildPipeline.GetBuildTargetGroup(target);
        buildPlayerOptions.target = target;
        buildPlayerOptions.options = BuildOptions.IncludeTestAssemblies;

        var error = BuildPipeline.BuildPlayer(buildPlayerOptions);
        if (error.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            EditorApplication.Exit(0);
        else
            EditorApplication.Exit(1);
    }
}
