// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

[RequireComponent(typeof(Renderer))]
[RequireComponent(typeof(MeshFilter))]
public class UVScroll : MonoBehaviour
{
    private const float _boundwidth = 5.0f;
    private Renderer _renderer;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The speed at which to scroll the UV coordinates.")]
    private float scrollSpeed = 0.05F;

    /// <summary>
    /// The speed at which to scroll the UV coordinates.
    /// </summary>
    public float ScrollSpeed
    {
        get => scrollSpeed;
        set => scrollSpeed = value;
    }
    #endregion Serialized Fields


    #region MonoBehavior Methods
    private void Start()
    {
        _renderer = GetComponent<Renderer>();
        var mesh = GetComponent<MeshFilter>().mesh;
        mesh.bounds = new Bounds(new Vector3(0, 0, 0), Vector3.one * _boundwidth);
    }

    private void Update()
    {
        if (_renderer == null)
        {
            return;
        }

        var offset = Mathf.Repeat(Time.time * scrollSpeed, 4);
        _renderer.sharedMaterial.SetVector("_Offset", new Vector4(offset, offset * 0.05f, 0, 0));
    }
    #endregion MonoBehavior Methods
}
