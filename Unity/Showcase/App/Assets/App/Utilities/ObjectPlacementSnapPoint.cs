// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// When a ObjectPlacement behaviour is focusing this snap point, the ObjectPlacement behavior will jump to this transform's pose.
/// </summary>
public class ObjectPlacementSnapPoint : MonoBehaviour
{
    #region Serialized Fields
    [Header("Events")]

    [SerializeField]
    [Tooltip("Event raised when this snap point was snapped to.")]
    private UnityEvent snapped = new UnityEvent();

    /// <summary>
    /// Event raised when this snap point was snapped to.
    /// </summary>
    public UnityEvent Snapped => snapped;

    [SerializeField]
    [Tooltip("Event raised when this snap point was unsnapped from.")]
    private UnityEvent unsnapped = new UnityEvent();

    /// <summary>
    /// Event raised when this snap point was unsnapped from.
    /// </summary>
    public UnityEvent Unsnapped => unsnapped;

    [SerializeField]
    [Tooltip("Event raised when this snap point was selected by the user.")]
    private UnityEvent selected = new UnityEvent();

    /// <summary>
    /// Event raised when this address was selected by the user.
    /// </summary>
    public UnityEvent Selected => selected;
    #endregion Serialized Fields

    #region Public Functions
    /// <summary>
    /// If this component is active and enabled, select it.
    /// </summary>
    public void Select()
    {
        if (isActiveAndEnabled)
        {
            selected?.Invoke();
        }
    }

    /// <summary>
    /// Snap focus to this object.
    /// </summary>
    public void Snap()
    {
        snapped?.Invoke();
    }

    /// <summary>
    /// Unsnap focus from this object.
    /// </summary>
    public void Unsnap()
    {
        unsnapped?.Invoke();
    }
    #endregion Public Function
}
