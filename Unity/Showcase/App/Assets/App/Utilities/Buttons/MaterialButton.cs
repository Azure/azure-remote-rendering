// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Rendering;
using UnityEngine;

[RequireComponent(typeof(SetPointerState))]
public class MaterialButton : ClickableButton
{
    private MaterialInstance previewMaterial = null;

    #region Serialized Fields
    [Header("Material Settings")]

    [SerializeField]
    [Tooltip("The setter that can change pointer state.")]
    private SetPointerState setter;

    /// <summary>
    /// The setter that can change pointer state.
    /// </summary>
    public SetPointerState Setter
    {
        get => setter;
        set => setter = value;
    }

    [SerializeField]
    [Tooltip("The remote material to apply when clicked.")]
    private RemoteMaterialObject remoteMaterial;

    /// <summary>
    /// The remote material to apply when clicked
    /// </summary>
    public RemoteMaterialObject RemoteMaterial
    {
        get => remoteMaterial;
        set => remoteMaterial = value;
    }

    [SerializeField]
    [Tooltip("The preview tile that show's the material color.")]
    private Renderer previewTile;

    /// <summary>
    /// The preview tile that show's the material color."
    /// </summary>
    public Renderer PreviewTile
    {
        get => previewTile;
        set => previewTile = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Methods
    private void OnValidate()
    {
        UpdateLabelText();
    }

    private void Awake()
    {
        if (previewTile != null)
        {
            previewMaterial = previewTile.EnsureComponent<MaterialInstance>();
        }
    }

    protected override void Start()
    {
        base.Start();

        if (remoteMaterial == null)
        {
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  $"Remote material is not set.");
            return;
        }

        if (setter == null)
        {
            setter = gameObject.EnsureComponent<SetPointerState>();
        }

        setter.Mode = PointerMode.Material;
        setter.ModeData = remoteMaterial;

        if (previewMaterial != null && remoteMaterial.Data != null)
        {
            previewMaterial.AcquireExistingMaterial().color = remoteMaterial.Data.AlbedoColor;
        }

        UpdateLabelText();
    }
    #endregion MonoBehavior Methods

    #region Protected Methods
    protected override void OnClicked()
    {
        setter?.Apply();
    }
    #endregion Protected Methods

    #region Private Methods
    private void UpdateLabelText()
    {
        if (remoteMaterial != null)
        {
            LabelText = remoteMaterial.name;
        }
    }
    #endregion Private Methods
}
