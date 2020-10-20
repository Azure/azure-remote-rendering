// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using System.Collections;
using System.Threading.Tasks;
using HoloToolkit.Unity;
using UnityEngine;

/// <summary>
/// This will show a "create session" dialog, if there is no known Azure Remote Rendering (ARR) session.
/// </summary>
public class StartSessionDialogController : MonoBehaviour
{
    private int _startDelay = 5000;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The dialog used to confirm creation of a new remote session.")]
    private AppDialog dialogPrefab = null;

    /// <summary>
    /// The dialog used to confirm creation of a new remote session.
    /// </summary>
    public AppDialog DialogPrefab
    {
        get => dialogPrefab;
        set => dialogPrefab = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Functions
    
    /// <summary>
    /// Delay showing the dialog slightly.
    /// </summary>
    private async void Start()
    {
        if (_startDelay > 0)
        {
            await Task.Delay(_startDelay);
        }

        // Wait for microphone permission dialog to close
        await MicrophoneHelper.GetMicrophoneStatus();

        AppServices.RemoteRendering.StatusChanged += RemoteRendering_StatusChanged;
        if (dialogPrefab == null || AppServices.RemoteRendering == null)
        {
            DestroyController();
        }
        else
        {
            CheckRemoteRenderingStatus();
        }
    }

    private void OnDestroy()
    {
        AppServices.RemoteRendering.StatusChanged -= RemoteRendering_StatusChanged;
    }
    #endregion MonoBehavior Functions

    #region Private Functions
    private void RemoteRendering_StatusChanged(object sender, IRemoteRenderingStatusChangedArgs e)
    {
        CheckRemoteRenderingStatus();
    }

    /// <summary>
    /// Check the remote rendering service's status, to decide if a dialog should be shown or destroyed.
    /// </summary>
    private void CheckRemoteRenderingStatus()
    {
        // If connected to a room with more than one user, skip showing a dialog and start ARR session
        if (AppServices.SharingService.IsConnected && AppServices.SharingService.Players.Count > 1)
        {
            CreateAndConnect();
            DestroyController();
        }
        else if (AppServices.RemoteRendering.Status == RemoteRenderingServiceStatus.NoSession)
        {
            ShowDialog();
        }
        else if (AppServices.RemoteRendering.Status != RemoteRenderingServiceStatus.Unknown)
        {
            DestroyController();
        }
    }

    private async void ShowDialog()
    {
        if (dialogPrefab == null)
        {
            DestroyController();
            return;
        }

        AppDialog.AppDialogResult startSession;
        GameObject dialogObject = null;
        try
        {
            dialogObject = Instantiate(dialogPrefab.gameObject, transform);
            startSession = await dialogObject.GetComponent<AppDialog>().Open();
        }
        finally
        {
            if (dialogObject != null)
            {
                GameObject.Destroy(dialogObject);
            }
        }

        if (startSession == AppDialog.AppDialogResult.Ok)
        {
            CreateAndConnect();
        }

        DestroyController();
    }

    private async void CreateAndConnect()
    {
        await AppServices.RemoteRendering.AutoConnect();
    }

    private void DestroyController()
    {
        if (gameObject != null)
        {
            GameObject.Destroy(gameObject);
        }
    }
    #endregion Private Functions
}
