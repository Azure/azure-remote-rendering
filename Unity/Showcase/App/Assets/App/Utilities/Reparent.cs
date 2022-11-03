// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

/// <summary>
/// Used to reparent the current transform to the target parent.
/// </summary>
public class Reparent : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    [Tooltip("The parent target to be reparented to.")]
    private ReparentTargetId targetId = ReparentTargetId.Unknown;

    /// <summary>
    /// The parent target to be reparented to.
    /// </summary>
    public ReparentTargetId TargetId
    {
        get => targetId;
        set => targetId = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Functions
    private void OnEnable()
    {
        ReparentNow();
    }

    private void OnDestroy()
    {
    }
    #endregion MonoBehavior Functions

    #region Public Functions
    #endregion Public Functions

    #region Private Functions
    private void ReparentNow()
    {
        ReparentTarget.Add(targetId, transform);
    }

    private void UnparentNow()
    {
        ReparentTarget.Remove(transform);
    }
    #endregion Private Functions
}
