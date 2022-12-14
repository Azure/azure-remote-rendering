// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;

using UnityEditor;

/// <summary>
/// Small hook for the BuildPlayerWindow 'Build' and 'Build And Run' buttons to prevent user from starting a build for unsupported platforms.
/// </summary>
[InitializeOnLoad]
public static class BuildGuard
{
    private static readonly List<BuildTarget> s_supportedPlatforms = new List<BuildTarget> { BuildTarget.WSAPlayer };
    private static string s_unsupportedPlatformDialogOptOutKey = "UNSUPPORTED_PLATFORM_DIALOG_OPT_OUT_KEY";

    static BuildGuard()
    {
        BuildPlayerWindow.RegisterBuildPlayerHandler(OnBuildClicked);
    }

    private static void OnBuildClicked(BuildPlayerOptions buildPlayerOptions)
    {
        if(!s_supportedPlatforms.Contains(buildPlayerOptions.target))
        {
            bool buildAnyways = EditorUtility.DisplayDialog(
                title: "Unsupported Platform",
                // Use the 'targetGroup' in the message as this is a more friendly string of the platform and mirrors the platform list in the Build Settings window.
                message: $"The target platform '{buildPlayerOptions.targetGroup}' is currently not supported!\n\nPlease refer to the documentation for a list of supported platforms, or use the custom 'Builder' menu to build the application.",
                ok: "Build anyways...",
                cancel: "Abort",
                DialogOptOutDecisionType.ForThisSession,
                s_unsupportedPlatformDialogOptOutKey);

            if (!buildAnyways)
            {
                return;
            }
        }

        BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(buildPlayerOptions);
    }
}
