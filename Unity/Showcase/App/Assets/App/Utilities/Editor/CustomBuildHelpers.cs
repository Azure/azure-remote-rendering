// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.OpenXR;
using Microsoft.MixedReality.Toolkit.Build.Editor;
using Microsoft.MixedReality.Toolkit.Utilities.Editor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEditor.XR.Management;
using UnityEditor.XR.OpenXR;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using System.Reflection;

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
    ARM64 = 3,
    x64 = 4
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


public static class CustomBuilder
{ 
    private static readonly IEnumerable<string> Scenes_Default = new string[] { "Assets/Scenes/SampleScene.unity" };
    private static readonly IEnumerable<string>  Scenes_DefaultPC = new string[] { "Assets/Scenes/SampleSceneDesktop.unity" }; 
    private const string CustomBuildPref_HoloLens1BuildDir = "_CustomBuild_HoloLens1BuildDir";
    private const string CustomBuildPref_HoloLens2BuildDir_ARM32 = "_CustomBuild_HoloLens2BuildDir";
    private const string CustomBuildPref_HoloLens2BuildDir_ARM64 = "_CustomBuild_HoloLens2BuildDir_ARM64";
    private const string CustomBuildPref_PCBuildDir = "_CustomBuild_PCBuildDir";

    private static bool s_isBuilding = false;

    /// <summary>
    /// The default directory where the HoloLens 1 build will be placed.
    /// </summary>
    public static string HoloLens1BuildDirectory
    {
        get { return GetUserSetting(CustomBuildPref_HoloLens1BuildDir, "UWP/HoloLens1"); }
        set { SetUserSetting(CustomBuildPref_HoloLens1BuildDir, value); }
    }

    /// <summary>
    /// The default directory where the HoloLens 2 build will be placed.
    /// </summary>
    public static string HoloLens2BuildDirectory_ARM32
    {
        get { return GetUserSetting(CustomBuildPref_HoloLens2BuildDir_ARM32, "UWP/HoloLens2_ARM32"); }
        set { SetUserSetting(CustomBuildPref_HoloLens2BuildDir_ARM32, value); }
    }

    /// <summary>
    /// The default directory where the HoloLens 2 build will be placed.
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

    [MenuItem("Builder/Build HoloLens 1 Client", false, 0)]
    public static async void BuildHoloLens1Project()
    {
        await BuildUnityPlayer(CustomBuildType.HoloLens1);
    }

    [MenuItem("Builder/Build HoloLens 2 Client (arm)", false, 0)]
    public static async void BuildHoloLens2Project_ARM32()
    {
        await BuildUnityPlayer(CustomBuildType.HoloLens2_ARM32);
    }

    [MenuItem("Builder/Build HoloLens 2 Client (arm64)", false, 0)]
    public static async void BuildHoloLens2Project_ARM64()
    {
        await BuildUnityPlayer(CustomBuildType.HoloLens2_ARM64);
    }

    [MenuItem("Builder/Build PC Client", false, 1)]
    public static async void BuildDesktopProject()
    {
        await BuildUnityPlayer(CustomBuildType.PC);
    }

    [MenuItem("Builder/Build Asset Bundles", isValidateFunction: false, priority = 111)]
    private static void SelectOutputDirectoryAndBuildAllAssetBundles()
    {
        bool build = EditorUtility.DisplayDialogComplex(
            title: $"Building '{EditorUserBuildSettings.activeBuildTarget}' Assets",
            message: $"You are about to build asset bundles for the '{EditorUserBuildSettings.activeBuildTarget}' target. You can change this target under the 'File > Building Settings' window.\n\nWould you like to continue?",
            ok: "Yes",
            cancel: "No",
            alt: null) == 0;

        if (build)
        {
            BuildAssetBundle.BuildAllAssetBundles(EditorUtility.OpenFolderPanel(
                "Select Output Directory",
                GetDefaultAssetBundleDirectory(),
                string.Empty));
        }
    }

    /// <summary>
    /// Start a build using Unity's command line.
    /// </summary>
    public static async void StartCommandLineBuild()
    {
        // We don't need stack traces on all our logs. Makes things a lot easier to read.
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

        // Default to HoloLens2
        var customBuildInfo = new CustomBuildInfo()
        {
            CustomType = CustomBuildType.HoloLens2_ARM64,
            Silent = true,
            AutoBuildAppx = false,
            OutputDirectory = "Build"
        };

        // parse command line arguments
        ParseBuildCommandLine(ref customBuildInfo);

        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "Starting command line build for application ({0})...", customBuildInfo.CustomType);
        bool success = false;
        try
        {
            success = await BuildUnityPlayer(customBuildInfo);
        }
        catch (Exception e)
        {
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, $"Build Exception: {e}");
        }

        if (success)
        {
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, $"Success! Unity build completed.");
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, $"Error! Unity build failed.");
            EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// Parse custom command line build arguments
    /// </summary>
    public static void ParseBuildCommandLine(ref CustomBuildInfo buildInfo)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        
        // Boolean used to track whether buildInfo contains scenes that are not specified by command line arguments.
        // These non command line argument scenes should be overwritten by those specified in the command line.
        bool buildInfoContainsNonCommandLineScene = buildInfo.Scenes != null && buildInfo.Scenes.Count() > 0;

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
                case "-hololens":
                case "-hololens2":
                    buildInfo.CustomType = CustomBuildType.HoloLens2_ARM64;
                    break;
                case "-buildOutput":
                    buildInfo.OutputDirectory = arguments[++i];
                    break;
                case "-sceneList":
                    if (buildInfoContainsNonCommandLineScene)
                    {
                        buildInfo.Scenes = SplitSceneList(arguments[++i]);
                        buildInfoContainsNonCommandLineScene = false;
                    }
                    else
                    {
                        buildInfo.Scenes = buildInfo.Scenes.Union(SplitSceneList(arguments[++i]));
                    }
                    break;
                case "-sceneListFile":
                    string path = arguments[++i];
                    if (File.Exists(path))
                    {
                        if (buildInfoContainsNonCommandLineScene)
                        {
                            buildInfo.Scenes = SplitSceneList(File.ReadAllText(path));
                            buildInfoContainsNonCommandLineScene = false;
                        }
                        else
                        {
                            buildInfo.Scenes = buildInfo.Scenes.Union(SplitSceneList(File.ReadAllText(path)));
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Scene list file at '{path}' does not exist.");
                    }
                    break;
            }
        }
    }

    private static CustomBuildPlatform ToBuildPlatform(CustomBuildType type)
    {
        CustomBuildPlatform result = CustomBuildPlatform.x86;
        switch (type)
        {
            case CustomBuildType.HoloLens1:
                result = CustomBuildPlatform.x86;
                break;

            case CustomBuildType.PC:
                result = CustomBuildPlatform.x64;
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

    private static Task<bool> BuildUnityPlayer(CustomBuildType type)
    {
        return BuildUnityPlayer(new CustomBuildInfo()
        {
            CustomType = type,
            Silent = false,
            AutoBuildAppx = false,
            OutputDirectory = string.Empty,
        });
    }

    private static async Task<bool> BuildUnityPlayer(CustomBuildInfo info)
    {
        if (s_isBuilding)
        {
            return false;
        }

        bool success = false;
        try
        {
            s_isBuilding = true;
            success = await ExecuteBuild(info);
        }
        finally
        {
            s_isBuilding = false;
        }
        return success;
    }

    private static bool ValidateSceneCount(IEnumerable<string> scenes)
    {
        if (scenes == null || scenes.Count() == 0)
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

    private static bool IsPCBuild(CustomBuildType type)
    {
        return type == CustomBuildType.PC;
    }

    private static CustomBuildType GetCustomBuildTypeFromEditorSettings()
    {
        CustomBuildType type = CustomBuildType.PC;
        bool isUwp = EditorUserBuildSettings.activeBuildTarget == BuildTarget.WSAPlayer;
        
        if (isUwp)
        {
            switch (EditorUserBuildSettings.wsaArchitecture)
            {
                case "ARM32":
                    type = CustomBuildType.HoloLens2_ARM32;
                    break;

                case "x86":
                case "AMD64":
                    type = CustomBuildType.PC;
                    break;

                default:
                    type = CustomBuildType.HoloLens2_ARM64;
                    break;
            }
        }

        return type;
    }

    private static string GetDefaultBuildDirectory()
    {
        return GetDefaultBuildDirectory(GetCustomBuildTypeFromEditorSettings());
    }

    private static string GetDefaultBuildDirectory(CustomBuildType type)
    {
        string defaultDirectory;
        switch (type)
        {
            case CustomBuildType.HoloLens1:
                defaultDirectory = HoloLens1BuildDirectory;
                break;

            case CustomBuildType.HoloLens2_ARM32:
                defaultDirectory = HoloLens2BuildDirectory_ARM32;
                break;

            case CustomBuildType.HoloLens2_ARM64:
                defaultDirectory = HoloLens2BuildDirectory_ARM64;
                break;

            case CustomBuildType.PC:
            default:
                defaultDirectory = PCBuildDirectory;
                break;
        }
        return defaultDirectory;
    }

    private static string GetDefaultAssetBundleDirectory()
    {
        return GetDefaultAssetBundleDirectory(GetDefaultBuildDirectory());
    }

    private static string GetDefaultAssetBundleDirectory(string parentDirectory)
    {
        if (string.IsNullOrEmpty(parentDirectory))
        {
            return null;
        }
        else
        {
            return $"{parentDirectory}/AssetBundles";
        }
    }

    private static string SelectBuildDirectory(CustomBuildType type)
    {
        string buildDirectory = EditorUtility.OpenFolderPanel(
            string.Format("Select '{0}' Build Directory", type.ToString()),
            GetDefaultBuildDirectory(type),
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

                case CustomBuildType.HoloLens2_ARM64:
                    HoloLens2BuildDirectory_ARM64 = buildDirectory;
                    break;

                case CustomBuildType.PC:
                default:
                    PCBuildDirectory = buildDirectory;
                    break;
            }
        }

        return buildDirectory;
    }

    private static Task<bool> ExecuteBuild(CustomBuildInfo customBuildInfo)
    {
        var scenes = SelectScenes(customBuildInfo);
        if (!ValidateSceneCount(scenes))
        {
            throw new ArgumentException("No Unity scene was specified.");
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
            throw new ArgumentException("No output build directory was specified.");
        }

        // Make sure XR is off for PC
        XRSettingsCache originalXRSettings = null;
        if (IsPCBuild(customBuildInfo.CustomType))
        {
            originalXRSettings = XRSettingsHelper.Capture(BuildTargetGroup.WSA);
            XRSettingsHelper.Disable(BuildTargetGroup.WSA);
        }
        else
        {
            XRSettingsHelper.Validate(BuildTargetGroup.WSA);
        }

        // Update solutions
        UnityPlayerBuildTools.SyncSolution();

        // Post build actions
        void PostBuildAction(BuildReport buildReport)
        {
            // Restore settings
            XRSettingsHelper.Restore(BuildTargetGroup.WSA, originalXRSettings);
        
            // Handle error or build appx
            if (buildReport.summary.result != BuildResult.Succeeded)
            {
                string errorMessage = $"{PlayerSettings.productName} Unity Build {buildReport.summary.result}!";
                Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, context: null, "{0}", errorMessage);
                if (!customBuildInfo.Silent)
                {
                    EditorUtility.DisplayDialog(errorMessage, "See console for failure details.", "OK");
                } 
            }
            else if (customBuildInfo.AutoBuildAppx || 
                (!customBuildInfo.Silent && !EditorUtility.DisplayDialog(PlayerSettings.productName, "Unity build completed successfully.", "OK", "Build AppX")))
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

        return BuildPlayer(new CustomBuildInfo
        {
            CustomType = customBuildInfo.CustomType,
            OutputDirectory = customBuildInfo.OutputDirectory,
            BuildPlatform = ToBuildPlatform(customBuildInfo.CustomType).ToString(),
            Scenes = scenes,
            PostBuildAction = PostBuildAction
        });
    }

    private static IEnumerable<string> SelectScenes(CustomBuildInfo customBuildInfo)
    {
        var scenes = customBuildInfo.Scenes;
        if (scenes == null || scenes.Count() == 0)
        {
            if (IsPCBuild(customBuildInfo.CustomType))
            {
                scenes = Scenes_DefaultPC;
            }
            else
            {
                scenes = Scenes_Default;
            }
        }

        if (scenes == null || scenes.Count() == 0)
        {
            scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path);
        }

        return scenes;
    }

    private static Task<bool> BuildPlayer(CustomBuildInfo customBuildInfo)
    {
        return UwpPlayerBuildTools.BuildPlayer(new UwpBuildInfo
        {
            OutputDirectory = customBuildInfo.OutputDirectory,
            BuildPlatform = customBuildInfo.BuildPlatform,
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

    private static async void BuildAppx(CustomBuildInfo customAppxBuildInfo)
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
        try
        {
            SceneView.FocusWindowIfItsOpen<UnityEditor.SceneView>();
        }
        catch (Exception ex)
        {
            Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "Exception occurred when trying to focus window during build. Reason: {0}", ex.Message);
        }

        try
        {
            EditorAssemblyReloadManager.LockReloadAssemblies = value;
        }
        catch (Exception ex)
        {
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "Exception occurred when trying to lock assemblies for building. Reason: {0}", ex.Message);
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

    private static IEnumerable<string> SplitSceneList(string sceneList)
    {
        return from scene in sceneList.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                select scene.Trim();
    }

    /// <summary>
    /// A helper to cache XR settings with
    /// </summary>
    private class XRSettingsCache
    {
        public bool InitManagerOnStart { get; private set; }

        public IList<Type> XRLoaders { get; private set; }

        public static XRSettingsCache Create(BuildTargetGroup buildTargetGroup)
        {
            // Get current xr settings
            var xrSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.WSA);

            // Cache settings
            XRSettingsCache settings = new XRSettingsCache();
            settings.InitManagerOnStart = xrSettings.InitManagerOnStart;
            settings.XRLoaders = new List<Type>();

            var manager = xrSettings.Manager;
            if (manager != null)
            {
                var loaders = manager.activeLoaders;
                for (int i = 0; i <= loaders.Count - 1; i++)
                {
                    settings.XRLoaders.Add(loaders[i].GetType());
                }
            }

            return settings;
        }
    }

    private static class XRSettingsHelper
    {
        public static XRSettingsCache Capture(BuildTargetGroup buildTargetGroup)
        {
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "Saving XR settings for '{0}'.", buildTargetGroup);
            return XRSettingsCache.Create(buildTargetGroup);
        }

        public static void Disable(BuildTargetGroup buildTargetGroup)
        {
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "Disable XR settings for '{0}'.", buildTargetGroup);

            var xrSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(buildTargetGroup);
            xrSettings.InitManagerOnStart = false;

            var manager = xrSettings.Manager;
            if (manager != null)
            {
                var loaders = manager.activeLoaders;
                for (int i = loaders.Count - 1; i >= 0; i--)
                {
                    if (!manager.TryRemoveLoader(loaders[i]))
                    {
                        Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "Failed to disable XR settings for '{0}'. Failed to remove XR loader '{1}'",
                            buildTargetGroup, loaders[i].name);
                    }
                }
            }
        }

        public static void Restore(BuildTargetGroup buildTargetGroup, XRSettingsCache settingsCache)
        {
            if (settingsCache != null)
            {
                Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "Restoring XR settings for '{0}'.", buildTargetGroup);

                var xrSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(buildTargetGroup);
                xrSettings.InitManagerOnStart = settingsCache.InitManagerOnStart;

                var manager = xrSettings.Manager;
                if (manager != null)
                {
                    var loaders = settingsCache.XRLoaders;
                    for (int i = 0; i <= loaders.Count - 1; i++)
                    {
                        var loader = ScriptableObject.CreateInstance(loaders[i]) as XRLoader;
                        if (!manager.TryAddLoader(loader))
                        {
                            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "Failed to restore XR settings for '{0}'. Failed to add XR loader '{1}'",
                                buildTargetGroup, loader.name);
                        }
                    }
                }
            }
            else
            {
                Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "Ignoring request to restore XR settings for '{0}', there were no saved settings", buildTargetGroup);
            }
        }

        public static bool Validate(BuildTargetGroup buildTargetGroup)
        {
            bool valid = true;
            var openXRSettings = FindOpenXRSettings();
            var buildGroupSettings = openXRSettings?.GetSettingsForBuildTargetGroup(buildTargetGroup);
            if (buildGroupSettings == null)
            {
                valid = false;
                Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "OpenXR validation failure for '{0}'. Could not find OpenXR settings.",
                    buildTargetGroup);
            }
            else
            {
                FeatureHelpers.RefreshFeatures(buildTargetGroup);
                Fix(buildTargetGroup, buildGroupSettings);
                FeatureHelpers.RefreshFeatures(buildTargetGroup);
                Print(buildTargetGroup, buildGroupSettings);
            }

            if (valid)
            {
                valid = CheckValidationRules(buildTargetGroup);
            }

            return valid;
        }

        private static bool CheckValidationRules(BuildTargetGroup buildTargetGroup)
        {
            bool valid = true;
            var failures = new List<OpenXRFeature.ValidationRule>();
            OpenXRProjectValidation.GetCurrentValidationIssues(failures, buildTargetGroup);

            for (int i = 0; i < failures.Count; i++)
            {
                valid = false;
                Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "OpenXR validation failure for '{0}'. {1}",
                    buildTargetGroup, failures[i].message);
            }

            return valid;
        }

        private static IPackageSettings FindOpenXRSettings()
        {
            IPackageSettings result = null;
            string searchText = string.Format("t:OpenXRPackageSettings");
            string[] assets = AssetDatabase.FindAssets(searchText);
            if (assets.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(assets[0]);
                result = AssetDatabase.LoadAssetAtPath(path, typeof(IPackageSettings)) as IPackageSettings;
            }
            return result;
        }

        private static void Print(BuildTargetGroup buildTargetGroup, OpenXRSettings settings)
        {
            var features = settings.GetFeatures();
            var featureString = new StringBuilder();

            featureString.Append("OpenXR settings for '");
            featureString.Append(buildTargetGroup);
            featureString.Append("'");
            featureString.AppendLine();

            featureString.Append("OpenXR render mode '");
            featureString.Append(settings.renderMode);
            featureString.Append("'");
            featureString.AppendLine();

            featureString.Append("OpenXR depth submission mode '");
            featureString.Append(settings.depthSubmissionMode);
            featureString.Append("'");
            featureString.AppendLine();

            featureString.Append("OpenXR features:");
            featureString.AppendLine();
            for (int i = 0; i < features.Length; i++)
            {
                var feature = features[i];
                featureString.Append("    * ");
                featureString.Append(feature.name);
                featureString.Append(" (");
                featureString.Append(feature.GetType().FullName);
                featureString.Append(")");
                featureString.Append(" (");
                featureString.Append(feature.enabled);
                featureString.Append(")");
                featureString.AppendLine();
            }

            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, featureString.ToString());
        }

        private static void Fix(BuildTargetGroup buildTargetGroup, OpenXRSettings settings)
        {
            FixEditor();

            settings.depthSubmissionMode = OpenXRSettings.DepthSubmissionMode.Depth16Bit;
            settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;

            int requiredFeatures = _requiredOpenXRFeatures.Length;
            for (int i = 0; i < requiredFeatures; i++)
            {
                var feature = FeatureHelpers.GetFeatureWithIdForBuildTarget(buildTargetGroup, _requiredOpenXRFeatures[i]);
                if (feature != null)
                {
                    feature.enabled = true;
                }
            }
        }

        /// <summary>
        /// Fix common OpenXR Editor failure
        /// </summary>
        private static void FixEditor()
        {
            var cls = typeof(UnityEngine.InputSystem.InputDevice).Assembly.GetType("UnityEngine.InputSystem.Editor.InputEditorUserSettings");
            if (cls == null) return;
            var prop = cls.GetProperty("lockInputToGameView", BindingFlags.Static | BindingFlags.Public);
            if (prop == null) return;
            prop.SetValue(null, true);
        }

        private static string[] _requiredOpenXRFeatures = new string[]
        {
            EyeGazeInteraction.featureId,
            MicrosoftHandInteraction.featureId,
            // HandTrackingFeaturePlugin.featureId is marked internal
            "com.microsoft.openxr.feature.handtracking",
            // MixedRealityFeaturePlugin.featureId is marked internal
            "com.microsoft.openxr.feature.hololens",
        };
         
    }
}
