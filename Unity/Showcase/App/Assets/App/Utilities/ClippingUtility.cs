// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Rendering;
using Microsoft.MixedReality.Toolkit.Utilities;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// A utility for automatically adding game objects to a Mixed Reality Toolkit (MRTK) ClippingPrimitive.
/// </summary>
public class ClippingUtility : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    [Tooltip("The objects containing children to be clipped.")]
    private GameObject[] containers = new GameObject[0];

    /// <summary>
    /// The objects containing children to be clipped.
    /// </summary>
    public GameObject[] Containers 
    {
        get => containers;
        set => containers = value;
    }

    [SerializeField]
    [Tooltip("The objects containing children that won't be clipped.")]
    private GameObject[] ignoreContainers = new GameObject[0];

    /// <summary>
    /// The objects containing children that won't be clipped.
    /// </summary>
    public GameObject[] IgnoreContainers
    {
        get => ignoreContainers;
        set => ignoreContainers = value;
    }
    [SerializeField]
    [Tooltip("The object containing the children to be clipped.")]
    private ClippingPrimitive clippingPrimitive;

    /// <summary>
    /// The object containing the children to be clipped.s
    /// </summary>
    public ClippingPrimitive ClippingPrimitive 
    {
        get => clippingPrimitive;
        set => clippingPrimitive = value;
    }
    #endregion Serialized Fields

    #region Private Fields
    private List<Renderer> foundRenders;
    #endregion Private Fields

    #region MonoBehavior Functions
    private void Awake()
    {
        UpdateClippedChildren();
    }
    #endregion MonoBehavior Functions

    #region Public Functions
    public void UpdateClippedChildren()
    {
        if (containers == null || containers.Length == 0)
        {
            containers = new GameObject[] { gameObject };
        }

        if (foundRenders != null)
        {
            RemoveRenderers(foundRenders);
            foundRenders = null;
        }

        if (containers != null && clippingPrimitive != null)
        {
            foundRenders = new List<Renderer>();
            foreach (var containerEntry in containers)
            {
                foundRenders.AddRange(containerEntry.GetComponentsInChildren<Renderer>(includeInactive: true));
            }
        }

        if (foundRenders != null)
        {
            RemoveIgnoredRenders(foundRenders);
            AddRenderers(foundRenders);
        }
    }

    public void RemoveClippedChildren(GameObject @object)
    {
        if (clippingPrimitive == null || @object == null)
        {
            return;
        }

        RemoveRenderers(@object.GetComponentsInChildren<Renderer>(includeInactive: true));
    }

    public void AddClippedChildren(GameObject @object)
    {
        if (clippingPrimitive == null || @object == null)
        {
            return;
        }

        AddRenderers(@object.GetComponentsInChildren<Renderer>(includeInactive: true));
    }
    #endregion Public Functions

    #region Private Functions
    private void RemoveIgnoredRenders(IList<Renderer> renderers)
    {
        if (ignoreContainers == null)
        {
            return;
        }

        foreach (var ignoreEntry in ignoreContainers)
        {
            var ignoreRenderers = ignoreEntry.GetComponentsInChildren<Renderer>(includeInactive: true);
            foreach (var remove in ignoreRenderers)
            {
                renderers.Remove(remove);
            }
        }
    }

    private void AddRenderers(IEnumerable<Renderer> renderers)
    {
        foreach (var renderer in renderers)
        {
            // Ensure material instances have been created before disabling
            // MateriaInstance component. 
            var materialInstance = renderer.gameObject.EnsureComponent<MaterialInstance>();
            var material = materialInstance.AcquireExistingMaterial();

            // If a the render is for a textmeshpro component. Use this the material instance
            // material. This is because TestMeshPro will reset the material on enablement changes,
            // if the TextMeshPro.fontMaterial is not set.
            var textMesh = renderer.GetComponent<TextMeshPro>();
            if (textMesh != null)
            {
                textMesh.fontMaterial = material;
            }

            // Add renderer to the MRTK clipping component
            clippingPrimitive.AddRenderer(renderer);

            // Disable material instance after creating material instances, since Update() loop is expensive and 
            // not need for this scenario. The Update() loop is only needed if materials change. 
            materialInstance.enabled = false;
        }
    }

    private void RemoveRenderers(IEnumerable<Renderer> renderers)
    {
        foreach (var renderer in renderers)
        {
            clippingPrimitive.RemoveRenderer(renderer);
        }
    }

    #endregion Private Functions
}
