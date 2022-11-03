// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// A helper class that enables code that depends on Photon. If Photon is installed, dependent code is then enabled. 
/// If Photon is not installed, dependent code is disabled.
/// </summary>
[InitializeOnLoad]
public class PhotonEnabler
{
    /// <summary>
    /// Scripting defines to add when Photon is being enabled.
    /// </summary>
    private static ScriptDefineSettings[] enableSettings = new ScriptDefineSettings[]
    { 
        // Standalone = 1
        new ScriptDefineSettings()
        {
            Target = BuildTargetGroup.Standalone,
            Defines = new string[] { "PHOTON_INSTALLED" }
        },

        // WSA = 14
        new ScriptDefineSettings()
        {
            Target = BuildTargetGroup.WSA,
            Defines = new string[] { "PHOTON_INSTALLED" }
        }
    };

    /// <summary>
    /// Scripting defines to add when Photon is being disabled.
    /// </summary>
    /// <remarks>
    /// Photon automatically adds its own scripting defines when being added. This is why there are
    /// more scripting defines when disabling. 
    /// </remarks>
    private static ScriptDefineSettings[] disableSettings = new ScriptDefineSettings[]
    {
        // Standalone = 1
        new ScriptDefineSettings()
        {
            Target = BuildTargetGroup.Standalone,
            Defines = new string[] { "PHOTON_INSTALLED", "CROSS_PLATFORM_INPUT", "PHOTON_UNITY_NETWORKING", "PUN_2_0_OR_NEWER", "PUN_2_OR_NEWER", "PUN_2_19_OR_NEWER" }
        },
        
        // iOS = 4
        new ScriptDefineSettings()
        {
            Target = BuildTargetGroup.iOS,
            Defines = new string[] { "PHOTON_INSTALLED", "CROSS_PLATFORM_INPUT", "MOBILE_INPUT", "PHOTON_UNITY_NETWORKING", "PUN_2_0_OR_NEWER", "PUN_2_OR_NEWER", "PUN_2_19_OR_NEWER" }
        },
        
        // Android = 7
        new ScriptDefineSettings()
        {
            Target = BuildTargetGroup.Android,
            Defines = new string[] { "PHOTON_INSTALLED", "CROSS_PLATFORM_INPUT", "MOBILE_INPUT", "PHOTON_UNITY_NETWORKING", "PUN_2_0_OR_NEWER", "PUN_2_OR_NEWER", "PUN_2_19_OR_NEWER" }
        },
        
        // WebGL = 13
        new ScriptDefineSettings()
        {
            Target = BuildTargetGroup.WebGL,
            Defines = new string[] { "PHOTON_INSTALLED", "PHOTON_UNITY_NETWORKING", "PUN_2_0_OR_NEWER", "PUN_2_OR_NEWER", "PUN_2_19_OR_NEWER" }
        },
        
        // WSA = 14
        new ScriptDefineSettings()
        {
            Target = BuildTargetGroup.WSA,
            Defines = new string[] { "PHOTON_INSTALLED", "MOBILE_INPUT", "PHOTON_UNITY_NETWORKING", "PUN_2_0_OR_NEWER", "PUN_2_OR_NEWER", "PUN_2_19_OR_NEWER" }
        },
        
        // PS4 = 19
        new ScriptDefineSettings()
        {
            Target = BuildTargetGroup.PS4,
            Defines = new string[] { "PHOTON_INSTALLED", "PHOTON_UNITY_NETWORKING", "PUN_2_0_OR_NEWER", "PUN_2_OR_NEWER", "PUN_2_19_OR_NEWER" }
        },
        
        // XboxOne = 21
        new ScriptDefineSettings()
        {
            Target = BuildTargetGroup.XboxOne,
            Defines = new string[] { "PHOTON_INSTALLED", "PHOTON_UNITY_NETWORKING", "PUN_2_0_OR_NEWER", "PUN_2_OR_NEWER", "PUN_2_19_OR_NEWER" }
        },
        
        // tvOS = 25
        new ScriptDefineSettings()
        {
            Target = BuildTargetGroup.tvOS,
            Defines = new string[] { "PHOTON_INSTALLED", "PHOTON_UNITY_NETWORKING", "PUN_2_0_OR_NEWER", "PUN_2_OR_NEWER", "PUN_2_19_OR_NEWER" }
        },
        
        // Switch = 27
        new ScriptDefineSettings()
        {
            Target = BuildTargetGroup.Switch,
            Defines = new string[] { "PHOTON_INSTALLED", "PHOTON_UNITY_NETWORKING", "PUN_2_0_OR_NEWER", "PUN_2_OR_NEWER", "PUN_2_19_OR_NEWER" }
        },
        
        // Lumin = 28
        new ScriptDefineSettings()
        {
            Target = BuildTargetGroup.Lumin,
            Defines = new string[] { "PHOTON_INSTALLED", "PHOTON_UNITY_NETWORKING", "PUN_2_0_OR_NEWER", "PUN_2_OR_NEWER", "PUN_2_19_OR_NEWER" }
        },
        
        // Stadia = 29
        new ScriptDefineSettings()
        {
            Target = BuildTargetGroup.Stadia,
            Defines = new string[] { "PHOTON_INSTALLED", "PHOTON_UNITY_NETWORKING", "PUN_2_0_OR_NEWER", "PUN_2_OR_NEWER", "PUN_2_19_OR_NEWER" }
        },
        
        // CloudRendering = 30
        new ScriptDefineSettings()
        {
            Target = BuildTargetGroup.CloudRendering,
            Defines = new string[] { "PHOTON_INSTALLED", "PHOTON_UNITY_NETWORKING", "PUN_2_0_OR_NEWER", "PUN_2_OR_NEWER", "PUN_2_19_OR_NEWER" }
        },

        // GameCoreXboxSeries = 31
        new ScriptDefineSettings()
        {
            Target = BuildTargetGroup.GameCoreXboxSeries,
            Defines = new string[] { "PHOTON_INSTALLED", "PHOTON_UNITY_NETWORKING", "PUN_2_0_OR_NEWER", "PUN_2_OR_NEWER", "PUN_2_19_OR_NEWER" }
        },
        
        // GameCoreXboxOne = 32
        new ScriptDefineSettings()
        {
            Target = BuildTargetGroup.GameCoreXboxOne,
            Defines = new string[] { "PHOTON_INSTALLED", "PHOTON_UNITY_NETWORKING", "PUN_2_0_OR_NEWER", "PUN_2_OR_NEWER", "PUN_2_19_OR_NEWER" }
        },
        
        // PS5 = 33
        new ScriptDefineSettings()
        {
            Target = BuildTargetGroup.PS5,
            Defines = new string[] { "PHOTON_INSTALLED", "PHOTON_UNITY_NETWORKING", "PUN_2_0_OR_NEWER", "PUN_2_OR_NEWER", "PUN_2_19_OR_NEWER" }
        },
    };

    static PhotonEnabler()
    {
        string photonDirectory = $"{Application.dataPath}/Photon";
        if (Directory.Exists(photonDirectory)) 
        {
            AddPhotonDefineSymbols(enableSettings);
        }
        else
        {
            RemovePhotonDefineSymbols(disableSettings);
        }
    }

    #region Private Functions
    /// <summary>
    /// Ensure that the build targets contain the Photon scripting defines.
    /// </summary>
    private static void AddPhotonDefineSymbols(ScriptDefineSettings[] settings)
    {
        foreach (var entry in settings)
        {
            AddPhotonDefineSymbols(entry);
        }
    }

    /// <summary>
    /// Ensure that the build target contains the Photon scripting defines.
    /// </summary>
    private static void AddPhotonDefineSymbols(ScriptDefineSettings settings)
    {
        List<string> defineSymbols = GetDefineSymbols(settings.Target);
        if (settings.Defines != null)
        {
            foreach (string defineSymbol in settings.Defines)
            {
                if (!defineSymbols.Contains(defineSymbol))
                {
                    defineSymbols.Add(defineSymbol);
                }
            }
        }
        SetDefineSymbols(settings.Target, defineSymbols);
    }

    /// <summary>
    /// Ensure that the build targets does not contain the Photon scripting defines.
    /// </summary>
    private static void RemovePhotonDefineSymbols(ScriptDefineSettings[] settings)
    {
        foreach (var entry in settings)
        {
            RemovePhotonDefineSymbols(entry);
        }
    }

    /// <summary>
    /// Ensure that the build target does not contain the Photon scripting defines.
    /// </summary>
    private static void RemovePhotonDefineSymbols(ScriptDefineSettings settings)
    {
        List<string> defineSymbols = GetDefineSymbols(settings.Target);
        if (settings.Defines != null)
        { 
            foreach (string defineSymbol in settings.Defines)
            {
                defineSymbols.Remove(defineSymbol);
            }
        }
        SetDefineSymbols(settings.Target, defineSymbols);
    }

    private static List<string> GetDefineSymbols(BuildTargetGroup target)
    {
        List<string> defineSymbols;
        string defineSymbolsString = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);

        if (string.IsNullOrEmpty(defineSymbolsString))
        {
            defineSymbols = new List<string>();
        }
        else
        {
            defineSymbols = defineSymbolsString.Split(';').ToList();
        }

        return defineSymbols;
    }

    private static void SetDefineSymbols(BuildTargetGroup target, IList<string> defineSymbols)
    {
        var defineSymbolsString = string.Join(";", defineSymbols);
        PlayerSettings.SetScriptingDefineSymbolsForGroup(target, defineSymbolsString);
    }
    #endregion Private Function 

    #region Private Structs
    private struct ScriptDefineSettings
    {
        public BuildTargetGroup Target { get; set; }

        public string[] Defines { get; set; }
    }
    #endregion Private Structs
}
