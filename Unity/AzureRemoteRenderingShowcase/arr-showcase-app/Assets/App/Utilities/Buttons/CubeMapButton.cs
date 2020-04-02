// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Rendering;
using Microsoft.MixedReality.Toolkit.Extensions;
using UnityEngine;

[RequireComponent(typeof(SetPointerState))]
public class CubeMapButton : ClickableButton
{
    private MaterialInstance previewMaterial = null;

    #region Serialized Fields
    [Header("Cube Map Settings")]

    [SerializeField]
    [Tooltip("The renderer used to render a preview of the cube map.")]
    private Renderer previewRenderer;

    /// <summary>
    /// The renderer used to render a preview of the cube map.
    /// </summary>
    public Renderer PreviewRenderer
    {
        get => previewRenderer;
        set => previewRenderer = value;
    }

    [SerializeField]
    [Tooltip("The texture name to set with a loaded image.")]
    private string previewTextureName = "_MainTex";

    /// <summary>
    /// The texture name to set with a loaded image.
    /// </summary>
    public string PreviewTextureName
    {
        get => previewTextureName;
        set => previewTextureName = value;
    }

    [SerializeField]
    [Tooltip("The remote cube map to apply when clicked.")]
    private RemoteLightingData remoteCubeMap;

    /// <summary>
    /// The remote cube map to apply when clicked
    /// </summary>
    public RemoteLightingData RemoteCubeMap
    {
        get => remoteCubeMap;
        set => remoteCubeMap = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Methods
    private void OnValidate()
    {
        UpdateLabelText();
    }

    private void Awake()
    {
        if (previewRenderer != null)
        {
            previewMaterial = previewRenderer.EnsureComponent<MaterialInstance>();
        }
    }

    protected override void Start()
    {
        base.Start();

        if (remoteCubeMap == null)
        {
            Debug.LogError($"Remote cube map is not set.");
            return;
        }

        if (previewMaterial != null && remoteCubeMap.Texture != null)
        {
            previewMaterial.Material.SetTexture(previewTextureName, remoteCubeMap.Texture);
        }

        UpdateLabelText();
    }
    #endregion MonoBehavior Methods

    #region Protected Methods
    protected override void OnClicked()
    {
        ApplyCubeMap();
    }
    #endregion Protected Methods

    #region Private Methods
    private void UpdateLabelText()
    {        
        if (remoteCubeMap != null)
        {
            LabelText = remoteCubeMap.name;

            string nameText = $"{remoteCubeMap.name} Button";
            if (name != nameText)
            {
                name = nameText;
            }
        }
    }

    private async void ApplyCubeMap()
    {
        IRemoteRenderingMachine machine = AppServices.RemoteRendering.PrimaryMachine;
        if (machine == null || remoteCubeMap == null)
        {
            return;
        }

        await machine.Actions.SetLighting(remoteCubeMap);
    }
    #endregion Private Methods
}
