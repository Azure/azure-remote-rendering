// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A utility for automatically adding game objects to a Mixed Reality Toolkit (MRTK) ClippingPrimitive.
/// </summary>
public class ClippingUtility : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    [Tooltip("The object containing the children to be clipped.")]
    private GameObject container;

    /// <summary>
    /// The object containing the children to be clipped.
    /// </summary>
    public GameObject Container 
    {
        get => container;
        set => container = value;
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
        if (container == null)
        {
            container = gameObject;
        }

        if (clippingPrimitive == null)
        {
            clippingPrimitive = container?.GetComponent<ClippingPrimitive>();
        }

        if (foundRenders != null)
        {
            foreach (var renderer in foundRenders)
            {
                clippingPrimitive.RemoveRenderer(renderer);
            }
            foundRenders = null;
        }

        if (container != null && clippingPrimitive != null)
        {
            foundRenders = new List<Renderer>(container.GetComponentsInChildren<Renderer>(true));
        }

        if (foundRenders != null)
        {
            foreach (var renderer in foundRenders)
            {
                clippingPrimitive.AddRenderer(renderer);
            }
        }
    }

    public void RemoveClippedChildren(GameObject @object)
    {
        if (clippingPrimitive == null || @object == null)
        {
            return;
        }

        var renderers = @object.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            clippingPrimitive.RemoveRenderer(renderer);
        }
    }

    public void AddClippedChildren(GameObject @object)
    {
        if (clippingPrimitive == null || @object == null)
        {
            return;
        }

        var renderers = @object.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            clippingPrimitive.AddRenderer(renderer);
        }
    }
    #endregion Public Functions
}
