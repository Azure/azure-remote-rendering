// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

/// <summary>
/// Copies this object's enable state to the target object.
/// </summary>
public class CopyEnabledState : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    [Tooltip("The target whose enabled state will be changed.")]
    private GameObject targetObject;

    /// <summary>
    /// The target whose enabled state will be changed.
    /// </summary>
    public GameObject TargetObject
    {
        get => targetObject;
        set => targetObject = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Methods
    private void OnEnable()
    {
       if (targetObject != null)
       {
            targetObject.SetActive(true);
       }
    }
    private void OnDisable()
    {
        if (targetObject != null)
        {
            targetObject.SetActive(false);
        }
    }
    #endregion MonoBehavior Methods
}
