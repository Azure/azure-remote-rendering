// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

/// <summary>
/// Used to toggle the active state of a specific object
/// </summary>
public class ToggleObject : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    [Tooltip("The object to toggle on and off.")]
    private GameObject targetObject = null;

    /// <summary>
    /// The object to toggle on and off.
    /// </summary>
    public GameObject TargetObject
    {
        get => targetObject;
        set => targetObject = value;
    }
    #endregion Serialized Fields

    #region Public Functions
    public void HideIt()
    {
        SetObjectActive(false);
    }

    public void ShowIt()
    {
        SetObjectActive(true);
    }

    public void Toggle()
    {
        SetObjectActive(!targetObject.activeInHierarchy);
    }

    public void SetObjectActive(bool isActive)
    {
        if (targetObject != null)
        {
            targetObject.SetActive(isActive);
        }
    }
    #endregion Public Functions
}
