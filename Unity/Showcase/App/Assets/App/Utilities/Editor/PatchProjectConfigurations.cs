// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// This patches file names to using  $(Configuration) to use the $(UnityPlayerConfiguration) property instead.
/// </summary>
public class PatchProjectConfigurations : IPostprocessBuildWithReport
{
    private static PatchEntry[] _replacements = new PatchEntry[]
    {
        new PatchEntry() { fileSearchPattern = "Unity Data.vcxitems*", original = "baselib_UAP_$(PlatformTarget)_$(Configuration)_il2cpp.pdb", replacement = "baselib_UAP_$(PlatformTarget)_$(UnityPlayerConfiguration)_il2cpp.pdb"  }
    };

    /// <summary>
    /// The order in which to call this script
    /// </summary>
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report == null || report.files == null)
        {
            return;
        }

        foreach (var patchEntry in _replacements)
        {
            string[] files = Directory.GetFiles(
                report.summary.outputPath, 
                patchEntry.fileSearchPattern, 
                SearchOption.AllDirectories);

            foreach (var filePath in files)
            {
                DoReplacement(filePath, patchEntry);
            }
        }
    }

    #region Private Functions
    private void DoReplacement(string filePath, PatchEntry patch)
    {
        try
        {
            string text = File.ReadAllText(filePath);
            text = text.Replace(patch.original, patch.replacement);
            File.WriteAllText(filePath, text);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to replace text in file '{filePath}'. Exception: {ex}");
        }
    }
    #endregion Private Functions

    #region Private Structs
    private struct PatchEntry
    {
        public string fileSearchPattern; 
        public string original;
        public string replacement;
    }
    #endregion Private Structs
}
