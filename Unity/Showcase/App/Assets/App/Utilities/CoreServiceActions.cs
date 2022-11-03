// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit;
using UnityEngine;

/// <summary>
/// This MonoBehaviour contains MRTK service actions that can be accessed via
/// public methods or properties. This can be used for handling events within
/// the Inspector window, such as with the MRTK's speech input handlers.
/// </summary>
public class CoreServiceActions : MonoBehaviour
{
    #region Public Properties
    /// <summary>
    /// Get or set if the hand mesh is visible.
    /// </summary>
    public bool IsHandMeshVisible
    {
        get
        {
            var handTrackingProfile = CoreServices.InputSystem?.InputSystemProfile?.HandTrackingProfile;
            return handTrackingProfile?.EnableHandMeshVisualization ?? false;
        }

        set
        {
            var handTrackingProfile = CoreServices.InputSystem?.InputSystemProfile?.HandTrackingProfile;
            if (handTrackingProfile != null)
            {
                handTrackingProfile.EnableHandMeshVisualization = value;
            }
        }
    }

    /// <summary>
    /// Get or set if the hand mesh is visible.
    /// </summary>
    public bool IsLocalProfilerVisible
    {
        get
        {
            var diagnosticsSystem = CoreServices.DiagnosticsSystem;
            return diagnosticsSystem?.ShowProfiler ?? false;
        }

        set
        {
            var diagnosticsSystem = CoreServices.DiagnosticsSystem;
            if (diagnosticsSystem != null)
            {
                diagnosticsSystem.ShowProfiler = value;
            }
        }
    }
    #endregion Public Properties

    #region Public Methods
    public void ToggleProfiler()
    {
        IsLocalProfilerVisible = !IsLocalProfilerVisible;
    }
    #endregion Public Methods
}
