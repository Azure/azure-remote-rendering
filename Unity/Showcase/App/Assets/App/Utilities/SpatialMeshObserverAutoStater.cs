// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

/// <summary>
/// Whenever this script is enabled, the spatial mesh observer is started. Whenever this script is disabled, 
/// the mesh observer is stopped.
/// </summary>
public class SpatialMeshObserverAutoStater : MonoBehaviour
{
    private bool _quitting = false;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("Should this behaviour stop the mesh observer when disabled.")]
    private bool autoStopOnDisable = false;

    /// <summary>
    /// Should this behaviour stop the mesh observer when disabled.
    /// </summary>
    public bool AutoStopOnDisable
    {
        get => autoStopOnDisable;
        set => autoStopOnDisable = value;
    }

    [SerializeField]
    [Tooltip("The number seconds after which to stop the spatial mesh observer after it was started by this script. Set to a negative or zero value to disable auto stops.")]
    private float autoStopAfterStart = 15.0f;

    /// <summary>
    /// The number seconds after which to stop the spatial mesh observer after it was started by this script. Set to a negative value to disable auto stops.
    /// </summary>
    public float AutoStopAfterStart
    {
        get => autoStopAfterStart;
        set => autoStopAfterStart = value;
    }

    [SerializeField]
    [Tooltip("If true, the mesh renderers will use the 'visible' materia. If false, the mesh renders will use the 'occlusion' material.")]
    private bool meshVisible = false;

    /// <summary>
    /// If true, the mesh renderers will use the 'visible' materia. If false, the mesh renders will use the 'occlusion' material.
    /// </summary>
    public bool MeshVisible
    {
        get => meshVisible;
        set => meshVisible = value;
    }

    [SerializeField]
    [Tooltip("If true, raycasts are ignored by the spatial mesh.")]
    public bool ignoreRaycasts;

    /// <summary>
    /// If true, raycasts are ignored by the spatial mesh.
    /// </summary>
    public bool IgnoreRaycasts
    {
        get => ignoreRaycasts;
        set => ignoreRaycasts = value;
    }

    #endregion Serialized Fields

    #region MonoBehaviour Methods
    private void OnEnable()
    {
        SpatialMeshObserverHelper.SetState(new SpatialMeshObserverHelperState()
        {
            active = true,
            visible = meshVisible,
            ignoreRaycasts = ignoreRaycasts
        });

        if (autoStopAfterStart > 0)
        {
            SpatialMeshObserverHelper.SetStateDelayed(new SpatialMeshObserverHelperState()
            {
                active = false,
                visible = false,
                ignoreRaycasts = true
            }, TimeSpan.FromSeconds(autoStopAfterStart));
        }
    }

    private void OnDisable()
    {
        if (_quitting || !autoStopOnDisable)
        {
            return;
        }

        SpatialMeshObserverHelper.SetState(new SpatialMeshObserverHelperState()
        {
            active = false,
            visible = false,
            ignoreRaycasts = true
        });
    }

    private void OnApplicationQuit()
    {
        _quitting = true;
    }
    #endregion MonoBehaviour Methods
}
