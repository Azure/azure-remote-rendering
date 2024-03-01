using System;
using UnityEditor;

public class Build
{
    public static void ConfigurePlayerUWP()
    {
        Console.WriteLine("Configuring build for UWP.");

        string buildLocation = "./../../../Bin/Unity/Tutorial-Complete";
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
