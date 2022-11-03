// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using TMPro;
using UnityEngine;

public class AlternateTextColors : MonoBehaviour
{
    private int currentColorIndex = -1;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("Should the materials be updated in the editor. If false, colors will only be update at runtime.")]
    private bool updateInEditor = false;

    /// <summary>
    /// Should the materials be updated in the editor. If false, colors will only be update at runtime.
    /// </summary>
    public bool UpdateInEditor
    {
        get => updateInEditor;
        set => updateInEditor = value;
    }

    [SerializeField]
    [Tooltip("The frequency at which the color should be changed.")]
    private uint updateFrequency = 1;

    /// <summary>
    /// The frequency at which the color should be changed.
    /// </summary>
    public uint UpdateFrequency
    {
        get => updateFrequency;
        set => updateFrequency = value;
    }

    [SerializeField]
    [Tooltip("The colors to apply.")]
    private Color[] colors = new Color[0];

    /// <summary>
    /// The colors to apply. 
    /// </summary>
    public Color[] Colors
    {
        get => colors;
        set => colors = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Functions
    private void OnValidate()
    {
        if (updateInEditor && !Application.isPlaying)
        {
            UpdateAllTextColors();
        }
    }

    private void Start()
    {
        UpdateAllTextColors();
    }
    #endregion MonoBehavior Functions

    #region Private Functions
    private void UpdateAllTextColors()
    {
        if (colors == null || colors.Length == 0 || updateFrequency == 0)
        {
            return;
        }

        ResetColor();
        var children = GetComponentsInChildren<TextMeshPro>();
        if (children != null && children.Length > 0)
        {
            int length = children.Length;
            Color color = Color.magenta;
            for (int i = 0; i < length; i++)
            {
                if (i % updateFrequency == 0)
                {
                    color = NextColor();
                }
                UpdateTextColor(children[i], color);
            }
        }
    }

    private void ResetColor()
    {
        currentColorIndex = -1;
    }

    private Color NextColor()
    {
        currentColorIndex = (currentColorIndex + 1) % colors.Length;
        return colors[currentColorIndex];
    }

    private void UpdateTextColor(TextMeshPro textMesh, Color color)
    {
        textMesh.color = color;
    }
    #endregion Private Functions
}
