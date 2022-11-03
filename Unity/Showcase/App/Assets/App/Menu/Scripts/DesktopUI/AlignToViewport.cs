// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;

public class AlignToViewport : MonoBehaviour
{
    private int width;
    private int height;
    private float menuDepth;
    private Camera uiCamera;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The viewport position.")]
    private Vector2 viewportPos;

    /// <summary>
    /// The viewport position.
    /// </summary>
    public Vector2 ViewportPos
    {
        get => viewportPos;
        set => viewportPos = value;
    }

    [SerializeField]
    [Tooltip("The offset in the scene.")]
    public Vector3 offset;

    /// <summary>
    /// The offset in the scene.
    /// </summary>
    public Vector2 Offset
    {
        get => offset;
        set => offset = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Functions
    private void Awake()
    {
        uiCamera = CameraCache.Main;
        menuDepth = uiCamera.transform.InverseTransformPoint(transform.position).z;
    }

    private void Start()
    {
        ConfigureAlignment();
    }

    private void Update()
    {
        if (width != Screen.width || height != Screen.height)
        {
            ConfigureAlignment();
        }
    }
    #endregion MonoBehavior Functions

    #region Private Functions
    private void ConfigureAlignment()
    {
        Vector3 viewport = viewportPos;
        viewport.z = menuDepth;
        transform.position = uiCamera.ViewportToWorldPoint(viewport);
        transform.localPosition += offset;
        width = Screen.width;
        height = Screen.height;
    }
    #endregion Private Functions
}
