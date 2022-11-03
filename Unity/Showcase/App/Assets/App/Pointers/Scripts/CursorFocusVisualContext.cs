// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

/// <summary>
/// The context used in pair with CursorFocusVisual. Attach this component to objects that can change the cursor visual's materials.
/// </summary>
public class CursorFocusVisualContext : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    [Tooltip("The materials to apply to the cursor visual when this object is focused.")]
    private Material[] overrideMaterials = new Material[0];

    /// <summary>
    /// The materials to apply to the cursor visual when this object is focused.
    /// </summary>
    public Material[] OverrideMaterials
    {
        get => overrideMaterials;
        set => overrideMaterials = value;
    }

    [SerializeField]
    [Tooltip("Should the visual be resized when this object is focused.")]
    private bool resize = false;

    /// <summary>
    /// Should the visual be resized when this object is focused.
    /// </summary>
    public bool Resize
    {
        get => resize;
        set => resize = value;
    }

    [SerializeField]
    [Tooltip("The amount to scale the visual by when the object is focused. This is ignored if 'Resize' is false.")]
    private float resizeScale = 1;

    /// <summary>
    /// The amount to scale the visual by when the object is focused. This is ignored if 'Resize' is false.
    /// </summary>
    public float ResizeScale
    {
        get => resizeScale;
        set => resizeScale = value;
    }
    #endregion Serialized Fields
}
