// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using UnityEngine;

/// <summary>
/// This class helps with setting camera settings from a loaded RemoteObject. The RemoteObject data
/// can request specific camera settings. This class will apply those settings.  If the RemoteObject
/// data doesn't request specific settings, the local camera settings will be applied to the remote
/// camera.
/// </summary>
public class RemoteCameraSettings : MonoBehaviour
{
    private static LogHelper<RemoteCameraSettings> _log = new LogHelper<RemoteCameraSettings>();
    private RemoteObject _primaryObject = null;
    private CameraSettings _overrideSettingsInLateUpdate = null;
    private float _nearClipPlane = 0.0f;
    private float _farClipPlane = 0.0f;

    #region MonoBehaviour Functions
    private void LateUpdate()
    {
        // Need to override settings every frame. This is a workaround since the ARR SDK forces
        // the local and remote camera planes to be the same during each Update() loop
        ApplyNearAndFar(_overrideSettingsInLateUpdate);
    }

    private void Start()
    {
        AppServices.RemoteRendering.StatusChanged += OnRemoteRenderingStatusChanged;
        UpdateCameraSettings();
    }

    private void OnDestroy()
    {
        AppServices.RemoteRendering.StatusChanged -= OnRemoteRenderingStatusChanged;
        SetPrimaryObject(remoteObject: null);
    }
    #endregion MonoBehaviour Functions

    #region Public Functions
    public void SetPrimaryObject(RemoteObject remoteObject)
    {
        if (_primaryObject != null)
        {
            _primaryObject.Deleted.RemoveListener(OnPrimaryObjectDeleted);
            _primaryObject.DataChanged.RemoveListener(OnPrimaryObjectDataChanged);
        }

        _primaryObject = remoteObject;

        if (_primaryObject != null)
        {
            _primaryObject.Deleted.AddListener(OnPrimaryObjectDeleted);
            _primaryObject.DataChanged.AddListener(OnPrimaryObjectDataChanged);
        }

        UpdateCameraSettings();
    }
    #endregion Public Functions

    #region Private Functions

    private void OnRemoteRenderingStatusChanged(object sender, IRemoteRenderingStatusChangedArgs e)
    {
        UpdateCameraSettings();
    }

    private void OnPrimaryObjectDeleted(RemoteObjectDeletedEventData arg)
    {
        SetPrimaryObject(null);
    }

    private void OnPrimaryObjectDataChanged(RemoteObjectDataChangedEventData ags)
    {
        UpdateCameraSettings();
    }

    private void UpdateCameraSettings()
    {
        _overrideSettingsInLateUpdate = null;
        _nearClipPlane = CameraCache.Main.nearClipPlane;
        _farClipPlane = CameraCache.Main.farClipPlane;

        if (AppServices.RemoteRendering.Status != RemoteRenderingServiceStatus.SessionReadyAndConnected)
        {
            return;
        }

        var remoteCameraSettings = AppServices.RemoteRendering.PrimaryMachine?.Actions?.GetCameraSettings();
        if (remoteCameraSettings == null)
        {
            return;
        }
        
        RemoteContainer remoteContainer = null;
        if (_primaryObject != null)
        {
            remoteContainer = _primaryObject.Data as RemoteContainer;
        }

        RemoteCameraOverrides remoteCameraOverrides = remoteContainer?.CameraOverrides;
        if (remoteCameraOverrides != null)
        {
            if (remoteCameraOverrides.NearClipPlane > 0.0f)
            {
                _overrideSettingsInLateUpdate = remoteCameraSettings;
                _nearClipPlane = remoteCameraOverrides.NearClipPlane;
            }

            if (remoteCameraOverrides.FarClipPlane > 0.0f)
            {
                _overrideSettingsInLateUpdate = remoteCameraSettings;
                _farClipPlane = remoteCameraOverrides.FarClipPlane;
            }
        }

        ApplyNearAndFar(remoteCameraSettings);
    }

    private void ApplyNearAndFar(CameraSettings remoteCameraSettings)
    {
        if (remoteCameraSettings == null)
        {
            return;
        }

        try
        {
            remoteCameraSettings.SetNearAndFarPlane(_nearClipPlane, _farClipPlane);
        }
        catch (Exception ex)
        {
            _log.LogError("Failed to setting remote camera settings. Exception: {0}", ex);
            _overrideSettingsInLateUpdate = null;
        }
    }
    #endregion Private Functions
}
