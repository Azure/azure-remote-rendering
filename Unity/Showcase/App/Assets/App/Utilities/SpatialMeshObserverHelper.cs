// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.SpatialAwareness;
using Microsoft.MixedReality.Toolkit.XRSDK;
using Microsoft.MixedReality.Toolkit.XRSDK.WindowsMixedReality;
using System;
using System.Threading;
using UnityEngine;

public struct SpatialMeshObserverHelperState
{
    /// <summary>
    /// Should the spatial mesh objserver be active, and updating the message. If not active, the mesh renderers are also disabled.
    /// </summary>
    public bool active;

    /// <summary>
    /// If true, the mesh renderers will use the 'visible' materia. If false, the mesh renders will use the 'occlusion' material.
    /// See the Mixed Reality Toolkit configuration for more details on these materials.
    /// </summary>
    public bool visible;

    /// <summary>
    /// If true, raycasts are ignored by the spatial mesh.
    /// </summary>
    public bool ignoreRaycasts;
}

/// <summary>
/// This behavior helps with turning the spatial mesh on and off
/// </summary>
/// <remarks>
/// This is expected to be called from the main Unity thread.
/// </remarks>
public static class SpatialMeshObserverHelper
{
    private static IMixedRealitySpatialAwarenessMeshObserver _meshObserver;
    private static Timer _timer;
    private static string ignoreRaycastLayer = "Ignore Raycast";


    #region Public Functions
    /// <summary>
    /// Set spatial mesh observer state now. This will cancel delayed requests.
    /// </summary> 
    public static void SetState(SpatialMeshObserverHelperState state)
    {
        StopDelayed();

        if (!InitializeMeshObserver())
        {
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[SpatialMeshObserverHelper] no mesh observer");
            return;
        }

        _meshObserver.DisplayOption = state.visible ? SpatialAwarenessMeshDisplayOptions.Visible : SpatialAwarenessMeshDisplayOptions.Occlusion;
        _meshObserver.MeshPhysicsLayer = state.ignoreRaycasts ? LayerMask.NameToLayer(ignoreRaycastLayer) : _meshObserver.DefaultPhysicsLayer;

        if (CoreServices.SpatialAwarenessSystem.SpatialAwarenessObjectParent != null)
        {
            CoreServices.SpatialAwarenessSystem.SpatialAwarenessObjectParent.SetActive(state.active);
        }

        if (state.active)
        {
            CoreServices.SpatialAwarenessSystem.ResumeObservers<GenericXRSDKSpatialMeshObserver>();
        }
        else
        {
            CoreServices.SpatialAwarenessSystem.SuspendObservers<GenericXRSDKSpatialMeshObserver>();
        }
    }

    /// <summary>
    /// Set spatial mesh observer state after a delay. This will cancel other delayed requests.
    /// </summary> 
    public static void SetStateDelayed(SpatialMeshObserverHelperState state, TimeSpan delay)
    {
        StopDelayed();
        var callback = new TimerCallback((object s) => SetState(state));
        _timer = new Timer(callback, state: null, delay, TimeSpan.FromMilliseconds(-1));
    }
    #endregion Public Functions

    #region Private Functions
    /// <summary>
    /// Stop the last delayed set state operation
    /// </summary>
    private static void StopDelayed()
    {
        if (_timer != null)
        {
            _timer.Dispose();
            _timer = null;
        }
    }

    /// <summary>
    /// Ensure spatial awareness system has been created.
    /// </summary>
    private static void EnsureSpatialAwarenessSystem()
    {
        if (CoreServices.SpatialAwarenessSystem == null)
        {
            object[] args = { MixedRealityToolkit.Instance.ActiveProfile.SpatialAwarenessSystemProfile };
            if (!MixedRealityToolkit.Instance.RegisterService<IMixedRealitySpatialAwarenessSystem>(
                MixedRealityToolkit.Instance.ActiveProfile.SpatialAwarenessSystemSystemType, args: args) && CoreServices.SpatialAwarenessSystem != null)
            {
                Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}", "Failed to start the Spatial Awareness System!");
            }
        }
    }

    private static bool InitializeMeshObserver()
    {
        if (_meshObserver != null)
        {
            return true;
        }

        EnsureSpatialAwarenessSystem();

        // the spatial awareness mesh is configured to start out invisible.
        // cache the mesh observer so we can toggle the display state during placement.  
        var spatialAwarenessService = CoreServices.SpatialAwarenessSystem;
        var dataProviderAccess = spatialAwarenessService as IMixedRealityDataProviderAccess;
        _meshObserver = dataProviderAccess?.GetDataProvider<IMixedRealitySpatialAwarenessMeshObserver>();

        return _meshObserver != null;
    }
    #endregion Private Functions

}
