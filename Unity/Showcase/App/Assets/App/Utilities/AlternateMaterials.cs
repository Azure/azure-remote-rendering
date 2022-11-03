// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

public class AlternateMaterials : MonoBehaviour
{
    private int currentMaterialIndex = -1;

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
    [Tooltip("Should renderers be recursively searched for.")]
    private bool recursiveSearch = false;

    /// <summary>
    /// Should renderers be recursively searched for.
    /// </summary>
    public bool RecursiveSearch
    {
        get => recursiveSearch;
        set => recursiveSearch = value;
    }

    [SerializeField]
    [Tooltip("The materials to apply.")]
    private Material[] materials = new Material[0];

    /// <summary>
    /// The materials to apply. 
    /// </summary>
    public Material[] Materials
    {
        get => materials;
        set => materials = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Functions
    private void OnValidate()
    {
        if (updateInEditor && !Application.isPlaying)
        {
            UpdateAllRendererMaterials();
        }
    }

    private void Start()
    {
        UpdateAllRendererMaterials();
    }
    #endregion MonoBehavior Functions

    #region Private Functions
    private void UpdateAllRendererMaterials()
    {
        if (materials == null || materials.Length == 0)
        {
            return;
        }

        ResetMaterial();   
        
        if (recursiveSearch)
        {
            UpdateChildrenAndGrandChildren();
        }
        else
        {
            UpdateChildren();
        }
    }

    private void UpdateChildren()
    {
        if (transform.childCount > 0)
        {
            int length = transform.childCount;
            for (int i = 0; i < length; i++)
            {
                var child = transform.GetChild(i).GetComponent<MeshRenderer>();
                if (child != null)
                {
                    UpdateRendererMaterial(child, NextMaterial());
                }
            }
        }
    }

    private void UpdateChildrenAndGrandChildren()
    {
        var children = GetComponentsInChildren<MeshRenderer>();
        if (children != null && children.Length > 0)
        {
            int length = children.Length;
            for (int i = 0; i < length; i++)
            {
                var child = children[i];
                UpdateRendererMaterial(child, NextMaterial());
            }
        }
    }

    private void ResetMaterial()
    {
        currentMaterialIndex = -1;
    }

    private Material NextMaterial()
    {
        currentMaterialIndex = (currentMaterialIndex + 1) % materials.Length;
        return materials[currentMaterialIndex];
    }

    private void UpdateRendererMaterial(Renderer renderer, Material material)
    {
        renderer.sharedMaterial = material;
    }
    #endregion Private Functions
}
