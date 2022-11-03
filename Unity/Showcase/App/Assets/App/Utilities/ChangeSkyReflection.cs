// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using System;
using UnityEngine;
using UnityEngine.Events;

public class ChangeSkyReflection : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    [Tooltip("The url of the remote cube map to apply to the sky refelection.")]
    private string cubeMapUrl = null;

    /// <summary>
    /// The url of the remote cube map to apply to the sky refelection.
    /// </summary>
    public string CubeMapUrl
    {
        get => cubeMapUrl;

        set
        {
            if (cubeMapUrl != value)
            {
                cubeMapUrl = value;
                ApplyCubeMap();
            }
        }
    }

    [Header("Events")]

    [SerializeField]
    [Tooltip("Event raised when the sky reflection cube map is being applied by this class.")]
    private UnityEvent<string> skyReflectionApplying = new SkyReflectionEvent();

    /// <summary>
    /// Event raised when the sky reflection cube map is being applied by this class.
    /// </summary>
    public UnityEvent<string> SkyReflectionApplying => skyReflectionApplying;
    #endregion Serialized Fields

    #region MonoBehaviour Functions
    private void Start()
    {
        AppServices.RemoteRendering.StatusChanged += RemoteRenderingStatusChanged;
        ApplyCubeMap();
    }
    #endregion MonoBehaviour Functions

    #region Private Functions
    private void RemoteRenderingStatusChanged(object sender, IRemoteRenderingStatusChangedArgs e)
    {
        ApplyCubeMap();
    }

    private async void ApplyCubeMap()
    {
        if (string.IsNullOrEmpty(cubeMapUrl))
        {
            return;
        }

        skyReflectionApplying?.Invoke(cubeMapUrl);
        if (AppServices.RemoteRendering.Status == RemoteRenderingServiceStatus.SessionReadyAndConnected)
        {
            try
            {
                await AppServices.RemoteRendering.PrimaryMachine.Actions.SetLighting(cubeMapUrl);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to set light cube map.\r\nException: {ex.ToString()}");
            }
        }
    }
    #endregion Private Functions

    #region Private Class
    [SerializeField]
    private class SkyReflectionEvent : UnityEvent<string>
    {
    }
    #endregion Private Class
}

