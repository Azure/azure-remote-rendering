// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

/// <summary>
/// A helper class to move the app's stage objects
/// </summary>
public class RemoteObjectStageMoveHelper : MonoBehaviour
{
    #region Public Functions
    /// <summary>
    /// Move the first stage object found in the scene
    /// </summary>
    public void Move()
    {
        var stage = FindObjectOfType<RemoteObjectStage>();
        if (stage != null)
        {
            stage.MoveStage();
        }
    }
    #endregion Public Functions
}
