// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This MonoBehaviour contains application wide actions that can be accessed 
/// via public methods. This can be used for handling events within the Inspector
/// window, such as with the MRTK's speech input handlers.
/// </summary>
public class AppActions : MonoBehaviour
{
    #region Serialized Fields
    [Header("Perfabs")]

    [SerializeField]
    [Tooltip("The dialog shown when confirm application quitting.")]
    private AppDialog quitConfirmationDialog;

    /// <summary>
    /// The dialog shown when confirm application quitting.
    /// </summary>
    public AppDialog QuitConfirmationDialog
    {
        get => quitConfirmationDialog;
        set => quitConfirmationDialog = value;
    }
    #endregion Serialized Fields

    #region Public Functions
    /// <summary>
    /// Quit the current application.
    /// </summary>
    public void QuitApplication()
    {
        QuitApplicationWorker();
    }

    /// <summary>
    /// Set pointer to None
    /// </summary>
    public void SetPointerMode_None()
    {
        AppServices.PointerStateService.Mode = PointerMode.None;
    }

    /// <summary>
    /// Set pointer to ClipBar
    /// </summary>
    public void SetPointerMode_ClipBar()
    {
        AppServices.PointerStateService.Mode = PointerMode.ClipBar;
    }

    /// <summary>
    /// Set pointer to Delete
    /// </summary>
    public void SetPointerMode_Delete()
    {
        AppServices.PointerStateService.Mode = PointerMode.Delete;
    }

    /// <summary>
    /// Set pointer to Explode
    /// </summary>
    public void SetPointerMode_Explode()
    {
        AppServices.PointerStateService.Mode = PointerMode.Explode;
    }

    /// <summary>
    /// Set pointer to Manipulate
    /// </summary>
    public void SetPointerMode_Manipulate()
    {
        AppServices.PointerStateService.Mode = PointerMode.Manipulate;
    }

    /// <summary>
    /// Set pointer to ManipulatePiece
    /// </summary>
    public void SetPointerMode_ManipulatePiece()
    {
        AppServices.PointerStateService.Mode = PointerMode.ManipulatePiece;
    }

    /// <summary>
    /// Set pointer to Material
    /// </summary>
    public void SetPointerMode_Material(RemoteMaterial material)
    {
        AppServices.PointerStateService.SetModeWithData(PointerMode.Material, material);
    }

    /// <summary>
    /// Set pointer to Reset
    /// </summary>
    public void SetPointerMode_Reset()
    {
        AppServices.PointerStateService.Mode = PointerMode.Reset;
    }

    /// <summary>
    /// Reload the app settings file.
    /// </summary>
    public void ReloadAppSettings()
    {
        AppServices.AppSettingsService.Reload();
    }
    #endregion Public Functions

    #region Private Functions
    /// <summary>
    /// The function that actually quits the application
    /// </summary>
    private bool quitting = false;
    private async void QuitApplicationWorker()
    {
        if (!quitting)
        {
            bool quit = true;
            quitting = true;
            if (quitConfirmationDialog != null)
            {
                GameObject dialogObject = null;
                try
                {
                    dialogObject = Instantiate(quitConfirmationDialog.gameObject);
                    quit = await dialogObject.GetComponent<AppDialog>().Open() != AppDialog.AppDialogResult.Cancel;
                }
                finally
                {
                    quitting = false;
                    if (dialogObject != null)
                    {
                        GameObject.Destroy(dialogObject);
                    }
                }
            }

            if (quit)
            {
                Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", "Quitting application per request...");
                Application.Quit();
            }
        }
    }
    #endregion Private Functions
}
