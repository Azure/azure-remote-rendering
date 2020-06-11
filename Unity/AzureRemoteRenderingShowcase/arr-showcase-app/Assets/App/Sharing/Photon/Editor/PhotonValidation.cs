// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// A helper class that ensures Photon is installed, and adds or removes the PHOTON_INSTALLED define symbol
/// </summary>
[InitializeOnLoad]
public class PhotonValidation
{
    private static string photonDefineSymbol = "PHOTON_INSTALLED";

    static PhotonValidation()
    {
        string photonDirectory = $"{Application.dataPath}/Photon";
        if (Directory.Exists(photonDirectory)) 
        {
            AddPhotonDefineSymbol(BuildTargetGroup.WSA);
        }
        else
        {
            RemovePhotonDefineSymbol(BuildTargetGroup.WSA);
        }
    }

    #region Private Functions
    /// <summary>
    /// Ensure that the build target contains the Photon scripting define symbol.
    /// </summary>
    private static void AddPhotonDefineSymbol(BuildTargetGroup buildTargetGroup)
    {
        string defineSymbolsString = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);
        string[] defineSymbols;

        if (string.IsNullOrEmpty(defineSymbolsString))
        {
            defineSymbols = new string[] { photonDefineSymbol };
        }
        else
        {
            defineSymbols = defineSymbolsString.Split(';');
            if (!defineSymbols.Contains(photonDefineSymbol))
            {
                defineSymbols = defineSymbols.Append(photonDefineSymbol).ToArray();
            }
        }

        defineSymbolsString = string.Join(";", defineSymbols);
        PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, defineSymbolsString);
    }

    /// <summary>
    /// Ensure that the build target does not contain the Photon scripting define symbol.
    /// </summary>
    private static void RemovePhotonDefineSymbol(BuildTargetGroup buildTargetGroup)
    {
        string defineSymbolsString = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);

        if (!string.IsNullOrEmpty(defineSymbolsString))
        {
            string[] defineSymbols = defineSymbolsString.Split(';');
            defineSymbols = defineSymbols.Where(entry => entry != photonDefineSymbol).ToArray();
            defineSymbolsString = string.Join(";", defineSymbols);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, defineSymbolsString);
        }
    }
    #endregion Private Function 
}
