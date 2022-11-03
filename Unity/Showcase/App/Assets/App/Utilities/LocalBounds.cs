// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using Microsoft.MixedReality.Toolkit;

/// <summary>
/// A helper class for calculating local bounds of objects.
/// </summary>
public static class LocalBounds
{
    /// <summary>
    /// Get a bounds with a zero size.
    /// </summary>
    public static Bounds Zero { get; } = new Bounds(Vector3.zero, Vector3.zero);

    /// <summary>
    /// Get a bounds with a negative infinity size.
    /// </summary>
    public static Bounds NegativeInfinity { get; } = new Bounds(Vector3.zero, Vector3.negativeInfinity);

    /// <summary>
    /// Get a bounds with a positive infinity size.
    /// </summary>
    public static Bounds PositiveInfinity { get; } = new Bounds(Vector3.zero, Vector3.positiveInfinity);


    /// <summary>
    /// Checks if the specified bounds instance is invalid, or is infinitely big or small.
    /// </summary>
    public static bool IsInvalidOrInfinite(this Bounds bounds)
    {
        return !bounds.IsValid() ||
            bounds.size.x == float.PositiveInfinity ||
            bounds.size.y == float.PositiveInfinity ||
            bounds.size.z == float.PositiveInfinity ||
            bounds.size.x == float.NegativeInfinity ||
            bounds.size.y == float.NegativeInfinity ||
            bounds.size.z == float.NegativeInfinity;
    }


    /// <summary>
    /// Calculate the global bounds by looking from renderers.
    /// </summary>
    public static Bounds RendererBounds(this Transform transform)
    {
        Bounds bounds = new Bounds();

        Renderer[] meshes = transform.GetComponentsInChildren<Renderer>();
        int count = meshes?.Length ?? 0;
        for (int i = 0; i < count; i++)
        {
            if (i == 0)
            {
                bounds = meshes[i].bounds;
            }
            else
            {
                bounds.Encapsulate(meshes[i].bounds);
            }
        }        

        return bounds;
    }

    /// <summary>
    /// Calculate the global bounds by looking from renderers.
    /// </summary>
    public static Bounds RendererLocalBounds(this Transform transform)
    {
        Quaternion currentRotation = transform.rotation; 
        transform.rotation = Quaternion.Euler(Vector3.zero);

        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        Renderer[] meshes = transform.GetComponentsInChildren<Renderer>(includeInactive: true);
        int count = meshes?.Length ?? 0;
        for (int i = 0; i < count; i++)
        {
            if (i == 0)
            {
                bounds = meshes[i].bounds;
            }
            else
            {
                bounds.Encapsulate(meshes[i].bounds);
            }
        }
        
        bounds.center = transform.InverseTransformPoint(bounds.center);
        bounds.size = transform.InverseTransformSize(bounds.size);
        transform.rotation = currentRotation;

        return bounds;
    }
}
