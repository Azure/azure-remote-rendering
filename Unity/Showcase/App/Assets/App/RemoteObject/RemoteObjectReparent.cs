// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using UnityEngine;

/// <summary>
/// When loaded, automatically move this object to the app's main stage
/// </summary>
public class RemoteObjectReparent : MonoBehaviour
{
    #region Public Properties
    #endregion Public Properties

    #region MonoBehaviour Functions
    #endregion MonoBehavior Functions

    #region Public Functions
    public async void Reparent(bool reposition, OperationType operation)
    {
        var stage = await AppServices.RemoteObjectStageService.GetRemoteStage();

        if (operation == OperationType.Staged)
        {
            stage.StageObject(gameObject, reposition: reposition);
        }
        else
        {
            stage.UnstageObject(gameObject, reposition: reposition);
        }
    }
    #endregion Public Functions

    #region Public Enum
    public enum OperationType
    {
        /// <summary>
        /// Parent this object to the stage.
        /// </summary>
        Staged,

        /// <summary>
        /// Parent this object to the "unstaged" origin
        /// </summary>
        Unstaged
    }

    #endregion Public Enum
}
