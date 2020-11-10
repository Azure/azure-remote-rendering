// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Build.Editor;
using Microsoft.MixedReality.Toolkit.Utilities.Editor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum CustomBuildType
{
    HoloLens1,
    HoloLens2_ARM32,
    HoloLens2_ARM64,
    PC,
}

public enum CustomBuildPlatform
{
    x86 = 1,
    ARM = 2,
    ARM64 = 3
}

public enum CustomBuildConfiguration
{
    Debug,
    Release,
    Master,
    MasterWithLTCG
}

public class CustomBuildInfo
{
    public CustomBuildType CustomType { get; set; }
    public bool Silent { get; set; }
    public bool AutoBuildAppx { get; set; }
    public bool RebuildAppx { get; set; }
    public string Configuration { get; set; }
    public string BuildPlatform { get; set; }
    public string OutputDirectory { get; set; }
    public bool AutoIncrement { get; set; }
    public IEnumerable<string> Scenes { get; set; }
    public Action<BuildReport> PostBuildAction { get; set; }
}


public class CustomBuilder
{
    private const string CustomBuildPref_HoloLens_Scene = "SampleScene";
    private const string CustomBuildPref_Desktop_Scene = "SampleSceneDesktop";


    private const string CustomBuildPref_HoloLens1BuildDir = "_CustomBuild_HoloLens1BuildDir";
    private const string CustomBuildPref_HoloLens2BuildDir_ARM32 = "_CustomBuild_HoloLens2BuildDir";
    private const string CustomBuildPref_HoloLens2BuildDir_ARM64 = "_CustomBuild_HoloLens2BuildDir_ARM64";
    private const string CustomBuildPref_PCBuildDir = "_CustomBuild_PCBuildDir";

    private static CustomBuilder s_instance = new CustomBuilder();
    private bool m_building = false;

    /// <summary>
    /// The default directory where the hololens 1 build will be placed.
    /// </summary>
    public static string HoloLens1BuildDirectory
    {
        get { return GetUserSetting(CustomBuildPref_HoloLens1BuildDir, "UWP/HoloLens1"); }
        set { SetUserSetting(CustomBuildPref_HoloLens1BuildDir, value); }
    }

    /// <summary>
    /// The default directory where the hololens 2 build will be placed.
    /// </summary>
    public static string HoloLens2BuildDirectory_ARM32
    {
        get { return GetUserSetting(CustomBuildPref_HoloLens2BuildDir_ARM32, "UWP/HoloLens2_ARM32"); }
        set { SetUserSetting(CustomBuildPref_HoloLens2BuildDir_ARM32, value); }
    }

    /// <summary>
    /// The default directory where the hololens 2 build will be placed.
    /// </summary>
    public static string HoloLens2BuildDirectory_ARM64
    {
        get { return GetUserSetting(CustomBuildPref_HoloLens2BuildDir_ARM64, "UWP/HoloLens2_ARM64"); }
        set { SetUserSetting(CustomBuildPref_HoloLens2BuildDir_ARM64, value); }
    }

    /// <summary>
    /// The default directory where the server build will be placed.
    /// </summary>
    public static string PCBuildDirectory
    {
        get { return GetUserSetting(CustomBuildPref_PCBuildDir, "UWP/PC"); }
        set { SetUserSetting(CustomBuildPref_PCBuildDir, value); }
    }

    private CustomBuilder()
    {
    }

    [MenuItem("Builder/Build HoloLens 1 Client", false, 0)]
    public static async void BuildHoloLens1Project()
    {
        ConfigureBuildScene(CustomBuildType.HoloLens1);
        await s_instance.BuildUnityPlayer(CustomBuildType.HoloLens1);
    }

    [MenuItem("Builder/Build HoloLens 2 Client (arm)", false, 0)]
    public static async void BuildHoloLens2Project_ARM32()
    {
        ConfigureBuildScene(CustomBuildType.HoloLens2_ARM32);
        await s_instance.BuildUnityPlayer(CustomBuildType.HoloLens2_ARM32);
    }

    [MenuItem("Builder/Build HoloLens 2 Client (arm64)", false, 0)]
    public static async void BuildHoloLens2Project_ARM64()
    {
        ConfigureBuildScene(CustomBuildType.HoloLens2_ARM64);
        await s_instance.BuildUnityPlayer(CustomBuildType.HoloLens2_ARM64);
    }

    [MenuItem("Builder/Build PC Client", false, 1)]
    public static async void BuildDesktopProject()
    {
        ConfigureBuildScene(CustomBuildType.PC);
        await s_instance.BuildUnityPlayer(CustomBuildType.PC);
    }

    private static void ConfigureBuildScene(CustomBuildType buildType)
    {
        Scene targetScene = default;
        List<string> allDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup).Split(';').ToList();

        switch (buildType)
        {
            case CustomBuildType.HoloLens1:
            case CustomBuildType.HoloLens2_ARM32:
            case CustomBuildType.HoloLens2_ARM64:
                if (!PlayerSettings.virtualRealitySupported)
                {
                    if (!allDefines.Contains("USE_MR"))
                        allDefines.Add("USE_MR");
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, string.Join(";", allDefines));
                    PlayerSettings.virtualRealitySupported = true;
                }
                targetScene = SceneManager.GetSceneByName(CustomBuildPref_Desktop_Scene);
                break;
            case CustomBuildType.PC:
                if (PlayerSettings.virtualRealitySupported)
                {
                    if (allDefines.Contains("USE_MR"))
                        allDefines.Remove("USE_MR");
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, string.Join(";", allDefines));
                    PlayerSettings.virtualRealitySupported = false;
                }
                targetScene = SceneManager.GetSceneByName(CustomBuildPref_Desktop_Scene);
                break;
        }

        if (targetScene != default)
        {
            bool targetSceneActive = false;
            foreach (var editorScene in EditorBuildSettings.scenes)
            {
                if (editorScene.path.Equals(targetScene.path))
                {
                    editorScene.enabled = true;
                    targetSceneActive = true;
                }
                else
                {
                    editorScene.enabled = false;
                }
            }
            if (!targetSceneActive)
            {
                EditorBuildSettings.scenes.Append(new EditorBuildSettingsScene(targetScene.path, true));
            }
        }
    }

    /// <summary>
    /// Start a build using Unity's command line.
    /// </summary>
    public static async void StartCommandLineBuild()
    {
        // We don't need stack traces on all our logs. Makes things a lot easier to read.
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

        // default to HoloLens2
        var customBuildInfo = new CustomBuildInfo()
        {
            CustomType = CustomBuildType.HoloLens2_ARM32,
            Silent = true,
            AutoBuildAppx = true,
            OutputDirectory = ""
        };

        // parse command line arguments
        ParseBuildCommandLine(ref customBuildInfo);

        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "Starting command line build for application ({0})...", customBuildInfo.CustomType);
        EditorAssemblyReloadManager.LockReloadAssemblies = true;

        bool success = false;
        try
        {
            UnityPlayerBuildTools.SyncSolution();
            await s_instance.BuildUnityPlayer(customBuildInfo);
        }
        catch (Exception e)
        {
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, $"Build Failed!\n{e.Message}\n{e.StackTrace}");
            success = false;
        }

        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, $"Exiting command line build... Build success? {success}");
        EditorApplication.Exit(success ? 0 : 1);
    }

    /// <summary>
    /// Parse custom command line build arguments
    /// </summary>
    public static void ParseBuildCommandLine(ref CustomBuildInfo buildInfo)
    {
        string[] arguments = Environment.GetCommandLineArgs();

        for (int i = 0; i < arguments.Length; ++i)
        {
            switch (arguments[i])
            {
                case "-pc":
                    buildInfo.CustomType = CustomBuildType.PC;
                    break;
                case "-hololens1":
                    buildInfo.CustomType = CustomBuildType.HoloLens1;
                    break;
                case "-hololens2":
                    buildInfo.CustomType = CustomBuildType.HoloLens2_ARM64;
                    break;
                case "-buildOutput":
                    buildInfo.OutputDirectory = arguments[++i];
                    break;
            }
        }
    }

    private CustomBuildPlatform ToBuildPlatform(CustomBuildType type)
    {
        CustomBuildPlatform result = CustomBuildPlatform.x86;
        switch (type)
        {
            case CustomBuildType.HoloLens1:
            case CustomBuildType.PC:
                result = CustomBuildPlatform.x86;
                break;

            case CustomBuildType.HoloLens2_ARM32:
                result = CustomBuildPlatform.ARM;
                break;

            case CustomBuildType.HoloLens2_ARM64:
                result = CustomBuildPlatform.ARM64;
                break;

            default:
                Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "Unknown build type (type = {0})", type);
                break;
        }
        return result;
    }

    private Task BuildUnityPlayer(CustomBuildType type)
    {
        return BuildUnityPlayer(new CustomBuildInfo()
        {
            CustomType = type,
            Silent = false,
            AutoBuildAppx = false,
            OutputDirectory = string.Empty
        });
    }

    private async Task BuildUnityPlayer(CustomBuildInfo info)
    {
        if (m_building)
        {
            return;
        }
        m_building = true;

        try
        {
            await ExecuteBuild(info);
        }
        finally
        {
            m_building = false;
        }
    }

    private bool ValidateSceneCount()
    {
        if (EditorBuildSettings.scenes.Length == 0)
        {
            EditorUtility.DisplayDialog("Attention!",
                "No scenes are present in the build settings.\n" +
                "The current scene will be the one built.\n\n" +
                "Do you want to cancel and add one?",
                "Continue Anyway", "Cancel Build");
            return false;
        }

        return true;
    }

    private bool IsPCBuild(CustomBuildType type)
    {
        return type == CustomBuildType.PC;
    }

    private string SelectBuildDirectory(CustomBuildType type)
    {
        string defaultDirectory = null;
        switch (type)
        {
            case CustomBuildType.HoloLens1:
                defaultDirectory = HoloLens1BuildDirectory;
                break;

            case CustomBuildType.HoloLens2_ARM32:
                defaultDirectory = HoloLens2BuildDirectory_ARM32;
                break;

            case CustomBuildType.PC:
            default:
                defaultDirectory = PCBuildDirectory;
                break;
        }

        string buildDirectory = EditorUtility.OpenFolderPanel(
            string.Format("Select '{0}' Build Directory", type.ToString()),
            defaultDirectory,
            string.Empty);

        if (!string.IsNullOrEmpty(buildDirectory))
        {
            switch (type)
            {
                case CustomBuildType.HoloLens1:
                    HoloLens1BuildDirectory = buildDirectory;
                    break;

                case CustomBuildType.HoloLens2_ARM32:
                    HoloLens2BuildDirectory_ARM32 = buildDirectory;
                    break;

                case CustomBuildType.PC:
                default:
                    PCBuildDirectory = buildDirectory;
                    break;
            }
        }

        return buildDirectory;
    }

    private async Task ExecuteBuild(CustomBuildInfo customBuildInfo)
    {
        if (!ValidateSceneCount())
        {
            return;
        }

        if (!customBuildInfo.AutoBuildAppx && !customBuildInfo.Silent)
        {
            customBuildInfo.AutoBuildAppx = EditorUtility.DisplayDialog(PlayerSettings.productName, "Would you like to create an Appx package?", "Yes", "No");
        }

        if (string.IsNullOrEmpty(customBuildInfo.OutputDirectory) && !customBuildInfo.Silent)
        {
            customBuildInfo.OutputDirectory = SelectBuildDirectory(customBuildInfo.CustomType);
        }

        if (string.IsNullOrEmpty(customBuildInfo.OutputDirectory))
        {
            return;
        }

        // For PC version, make sure there is a "none" VR SDK
        string[] oldSDKs = PlayerSettings.GetVirtualRealitySDKs(BuildTargetGroup.WSA);
        if (IsPCBuild(customBuildInfo.CustomType) && !oldSDKs.Contains("None"))
        {
            string[] newSDKs = new string[oldSDKs.Length + 1];
            newSDKs[0] = "None";
            oldSDKs.CopyTo(newSDKs, 1);
            PlayerSettings.SetVirtualRealitySDKs(BuildTargetGroup.WSA, newSDKs);
        }

        // Make sure we're using instancing
        PlayerSettings.stereoRenderingPath = StereoRenderingPath.Instancing;

        // Post build actions
        void PostBuildAction(BuildReport buildReport)
        {
            PlayerSettings.SetVirtualRealitySDKs(BuildTargetGroup.WSA, oldSDKs);
            if (buildReport.summary.result != BuildResult.Succeeded)
            {
                EditorUtility.DisplayDialog($"{PlayerSettings.productName} Unity Build {buildReport.summary.result}!", "See console for failure details.", "OK");
            }
            else if (customBuildInfo.AutoBuildAppx || !EditorUtility.DisplayDialog(PlayerSettings.productName, "Unity build completed successfully.", "OK", "Build AppX"))
            {
                BuildAppx(new CustomBuildInfo()
                {
                    CustomType = customBuildInfo.CustomType,
                    RebuildAppx = false,
                    Configuration = CustomBuildConfiguration.Master.ToString(),
                    BuildPlatform = ToBuildPlatform(customBuildInfo.CustomType).ToString(),
                    OutputDirectory = customBuildInfo.OutputDirectory,
                    AutoIncrement = BuildDeployPreferences.IncrementBuildVersion,
                });
            }
        }

        await BuildPlayer(new CustomBuildInfo
        {
            CustomType = customBuildInfo.CustomType,
            OutputDirectory = customBuildInfo.OutputDirectory,
            BuildPlatform = ToBuildPlatform(customBuildInfo.CustomType).ToString(),
            Scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path),
            PostBuildAction = PostBuildAction
        });
    }

    private async Task BuildPlayer(CustomBuildInfo customBuildInfo)
    {
        await UwpPlayerBuildTools.BuildPlayer(new UwpBuildInfo
        {
            OutputDirectory = customBuildInfo.OutputDirectory,
            Scenes = customBuildInfo.Scenes,
            PostBuildAction = (innerBuildInfo, buildReport) =>
            {
                if (customBuildInfo.PostBuildAction != null)
                {
                    customBuildInfo.PostBuildAction(buildReport);
                }
            }
        });
    }

    private async void BuildAppx(CustomBuildInfo customAppxBuildInfo)
    {
        bool appxSuccess = false;
        try
        {
            EditorUtility.DisplayProgressBar("Creating AppX", "Compiling, Linking, and Packaging...", 0.9f);

            // Add "<ResolveAssemblyWarnOrErrorOnTargetArchitectureMismatch>Warning</ResolveAssemblyWarnOrErrorOnTargetArchitectureMismatch>" to work around ARM64 MSBUILD bug
            AddGlobalProperties(customAppxBuildInfo.OutputDirectory, PlayerSettings.productName, new (string, string)[] {
                ( "ResolveAssemblyWarnOrErrorOnTargetArchitectureMismatch", "Warning" )
            });

            SetLockReloadAssemblies(true);
            appxSuccess = await CustomUwpAppxBuildTools.BuildAppxAsync(new UwpBuildInfo()
            {
                RebuildAppx = customAppxBuildInfo.RebuildAppx,
                Configuration = customAppxBuildInfo.Configuration,
                BuildPlatform = customAppxBuildInfo.BuildPlatform,
                OutputDirectory = customAppxBuildInfo.OutputDirectory,
                AutoIncrement = customAppxBuildInfo.AutoIncrement,
            });
            SetLockReloadAssemblies(false);
        }
        catch (Exception ex)
        {
            appxSuccess = false;
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "Exception occurred when creating AppX package. {0}", ex.ToString());
        }

        EditorUtility.ClearProgressBar();
        if (appxSuccess)
        {
            if (!EditorUtility.DisplayDialog($"{PlayerSettings.productName} Appx", "AppX build completed successfully.", "OK", "Open in Explorer"))
            {
                EditorUtility.RevealInFinder(customAppxBuildInfo.OutputDirectory + "/AppPackages");
            }
        }
        else
        {
            EditorUtility.DisplayDialog($"{PlayerSettings.productName} Appx", "AppX build failed. See Console for details.", "OK");
        }
    }

    private static void SetLockReloadAssemblies(bool value)
    {
        // Move focus to scene view
        SceneView.FocusWindowIfItsOpen<UnityEditor.SceneView>(); 
        try
        {
            EditorAssemblyReloadManager.LockReloadAssemblies = value;
        }
        catch (Exception ex)
        {
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "Exception occurred when trying to lock assemblies for building. Reasion: {0}", ex.Message);
        }
    }

    private static string GetUserSetting(string settingName, string defaultValue)
    {
        return EditorPreferences.Get(settingName, defaultValue);
    }

    private static void SetUserSetting(string settingName, string settingValue)
    {
        EditorPreferences.Set(settingName, settingValue);
    }

    private static bool AddGlobalProperties(string buildDirectory, string projectName, (string, string)[] keysAndValues)
    {
        string propertyGroupTag = "PropertyGroup";
        string propertyGroupLabel = "Globals";

        // Find the vcxproj, assume the one we want is the first one
        string[] projectFiles = Directory.GetFiles(buildDirectory, projectName + ".vcxproj", SearchOption.AllDirectories);

        string projectFile = projectFiles[0];
        var rootNode = XElement.Load(projectFile);
        var defaultNamespace = rootNode?.GetDefaultNamespace();
        var propertyGroupNode =  rootNode?.Descendants(rootNode.GetDefaultNamespace() + propertyGroupTag).FirstOrDefault(
            el => (string)el.Attribute("Label") == propertyGroupLabel);

        if (propertyGroupNode == null)
        {
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, $"Package.appxmanifest for build (in path - {buildDirectory}) is missing an <{propertyGroupTag} Label=\"{propertyGroupLabel}\" /> node");
            return false;
        }

        HashSet<string> needToAdd = new HashSet<string>();
        foreach (var keyValue in keysAndValues)
        {
            needToAdd.Add(keyValue.Item1.ToLower());
        }

        var propertyGroupElements = propertyGroupNode.Elements();
        foreach (var element in propertyGroupElements)
        {
            needToAdd.Remove(element.Name.LocalName.ToLower());
        }

        foreach (var keyValue in keysAndValues)
        {
            if (!needToAdd.Contains(keyValue.Item1.ToLower()))
            {
                continue;
            }

            var newElement = new XElement(defaultNamespace + keyValue.Item1);
            newElement.Value = keyValue.Item2;
            propertyGroupNode.Add(newElement);
        }

        rootNode.Save(projectFile);
        return true;
    }
}
