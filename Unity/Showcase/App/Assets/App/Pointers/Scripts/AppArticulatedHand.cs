// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using UnityEngine;

/// <summary>
/// A behavior which changes the user's hand mesh color to match the "user's instance color". The
/// user's instance color is typical used during sharing experiences.
/// </summary>
public class AppArticulatedHand : MonoBehaviour
{
    private Material _colorMaterialInstance = null;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The target of material changes.")]
    private Renderer target;

    /// <summary>
    /// The target of material changes.
    /// </summary>
    public Renderer Target
    {
        get => target;
        set => target = value;
    }

    [SerializeField]
    [Tooltip("The default material used when there is no user instance color.")]
    private Material defaultMaterial;

    /// <summary>
    /// The default material used when there is no user instance color.
    /// </summary>
    public Material DefaultMaterial
    {
        get => defaultMaterial;
        set => defaultMaterial = value;
    }

    [SerializeField]
    [Tooltip("The material used when there is a user instance color.")]
    private Material colorMaterial;

    /// <summary>
    /// The material used when there is a user instance color.
    /// </summary>
    public Material ColorMaterial
    {
        get => colorMaterial;
        set => colorMaterial = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Functions
    private void Start()
    {
        AppServices.AppSettingsService.InstanceUserColorChanged += OnInstanceUserColorChanged;
        SetMaterialColor(AppServices.AppSettingsService.InstanceUserColor);
    }

    private void OnDestroy()
    {
        AppServices.AppSettingsService.InstanceUserColorChanged -= OnInstanceUserColorChanged;
    }
    #endregion MonoBehavior Functions

    private void OnInstanceUserColorChanged(IAppSettingsService sender, Color instanceColor)
    {
        SetMaterialColor(instanceColor);
    }

    private void SetMaterialColor(Color instanceColor)
    {
        if (target == null)
        {
            return;
        }

        Material useMaterial;
        if (instanceColor.a == 0)
        {
            useMaterial = defaultMaterial;
        }
        else
        {
            if (_colorMaterialInstance == null && colorMaterial != null)
            {
                _colorMaterialInstance = new Material(colorMaterial);
            }

            if (_colorMaterialInstance != null)
            {
                _colorMaterialInstance.color = instanceColor;
            }

            useMaterial = _colorMaterialInstance;
        }

        if (useMaterial != null)
        {
            target.sharedMaterial = useMaterial;
        }
    }
}
