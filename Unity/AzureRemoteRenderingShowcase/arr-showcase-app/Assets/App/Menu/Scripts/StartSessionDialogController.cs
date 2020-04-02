// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using UnityEngine;

/// <summary>
/// This will show a "create session" dialog, if there is no known Azure Remote Rendering (ARR) session.
/// </summary>
public class StartSessionDialogController : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    [Tooltip("The dialog used to confirm creation of a new remote session.")]
    private AppDialog dialog = null;

    /// <summary>
    /// The dialog used to confirm creation of a new remote session.
    /// </summary>
    public AppDialog Dialog
    {
        get => dialog;
        set => dialog = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Functions
    private void Start()
    {
        if (dialog == null)
        {
            DestroyController();
        }
        else
        {
            dialog.Close(false);
        }
    }

    private void Update()
    {
        if (dialog == null)
        {
            DestroyController();
            return;
        }

        // Remote rendering object maybe null at start-up, but will be created later.
        if (AppServices.RemoteRendering == null ||
            dialog.isActiveAndEnabled)
        {
            return;
        }

        if (AppServices.RemoteRendering.Status == RemoteRenderingServiceStatus.NoSession)
        {
            ShowDialog();
        }
        else if (AppServices.RemoteRendering.Status != RemoteRenderingServiceStatus.Unknown)
        {
            DestroyController();
        }
    }
    #endregion MonoBehavior Functions

    #region Private Functions
    private async void ShowDialog()
    {
        if (dialog == null)
        {
            DestroyController();
            return;
        }


        dialog.Open();
        bool startSession = await dialog.DiaglogTask;
        DestroyController();

        if (startSession)
        {
            await AppServices.RemoteRendering.StopAll();
            var machine = await AppServices.RemoteRendering.Create();
            machine?.Session.Connection.Connect();
        }
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
