// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using System;
using UnityEngine;

using Remote = Microsoft.Azure.RemoteRendering;

[Serializable]
public class RemoteMaterial
{
    [Header("General Settings")]

    [Tooltip("The display name of the material.")]
    public string Name = null;

    [Tooltip("The type of the material.")]
    public MaterialType Type = MaterialType.Pbr;

    [Header("Pbr and Color Settings")]

    [Tooltip("The albedo color of a material defines the unlit diffuse color. It can originate from a constant color, from a texture or both. In the latter case the texture color is modulated with the constant color, so in order to use the unmodified texture color, this albedo color should be left to white. The alpha component of this color is used for the opacity level in case the material is flagged as transparent.")]
    public Color AlbedoColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);

    [Tooltip("The albedo texture for this material.")]
    public string AlbedoTextureUrl;

    [Tooltip("The alpha clip threshold.  If a pixel's final alpha value falls below the threshold value, the pixel is clipped thus causing a hard cutout. Note that the material's Flags::AlphaClipped flag has to be set, otherwise this threshold has no effect.")]
    [UnityEngine.Range(0, 1)]
    public float AlphaClipThreshold = 0.5f;

    [Tooltip("This is very similar to changing the albedo's alpha on a transparent material, however this function internally manages the transparent flag for values of 1.0 (fully opaque) and smaller than 1.0 (semi-transparent) respectively.")]
    [UnityEngine.Range(0, 1)]
    public float FadeOut = 1.0f;

    [Tooltip("An offset is normalized to [0..1] range regardless of texture size, so (0.5, 0.5) points to the middle of a texture. The offset can be changed over frames to scroll a texture. The offset is applied likewise to all defined material textures.")]
    public Vector2 TexCoordOffset = Vector2.zero;

    [Tooltip("A two-component value to scale U and V independently. For instance, passing (4,4) will apply 4x4 tiling to the texture. Tiling is applied likewise to all defined material textures.")]
    public Vector2 TexCoordScale = Vector2.one;

    [Header("Pbr Settings")]

    [Tooltip("A valid AO texture is a grayscale texture that defines scalars that are combined with the constant ambient occlusion value (AOScale). Constant Constants.ObjectId_Invalid is used to denote 'no ambient occlusion texture'.")]
    public string AOMapUrl;

    [Tooltip("The scalar amount for ambient occlusion in [0..1] range.")]
    [UnityEngine.Range(0, 1)]
    public float AOScale = 1.0f;

    [Tooltip("A constant metalness value on this material. Metalness is a scalar in the [0..1] range, which in most of the use cases is either 0.0 (non-metallic) or 1.0 (metallic).")]
    [UnityEngine.Range(0, 1)]
    public float Metalness = 0.0f;

    [Tooltip("A valid metalness texture is a grayscale texture that defines scalars that are combined with the constant metalness value (Metalness properties).")]
    public string MetalnessMapUrl;

    [Tooltip("This function accepts any texture but note that a valid normalmap encodes the normal vector into RGB portions rather than being a grayscale heightmap.")]
    public string NormalMapUrl;

    [Tooltip("The pbr material's flags")]
    public RemotePbrMaterialFlags PbrFlags = RemotePbrMaterialFlags.DoubleSided | RemotePbrMaterialFlags.SpecularEnabled;

    [Tooltip("This function has no effect if the mesh does not provide vertex colors or if the UseVertexColor flag is not set.")]
    public PbrVertexAlphaMode VertexAlphaMode = PbrVertexAlphaMode.Occlusion;

    [Tooltip("A constant roughness value on this material. Roughness is a scalar in the [0..1] range.")]
    [UnityEngine.Range(0, 1)]
    public float Roughness = 0.75f;

    [Tooltip("A valid roughness texture is a grayscale texture that defines scalars that are combined with the constant roughness value (Roughness property).")]
    public string RoughnessMapUrl;

    [Header("Color Settings")]

    [Tooltip("The color material's color flags")]
    public RemoteColorMaterialFlags ColorFlags = RemoteColorMaterialFlags.DoubleSided;

    [Tooltip("The color material's transparency mode")]
    public ColorTransparencyMode ColorTransparencyMode = ColorTransparencyMode.Opaque;

    [Tooltip("This scalar defines how much the mesh's vertex color mixes into the final color. If 0.0, the vertex color does not contribute at all, if 1.0 it will be fully multiplied with the albedo color.")]
    [Range(0, 1)]
    public float VertexMix;

    #region Public Functions
    public override bool Equals(object other)
    {
        if (!(other is RemoteMaterial) || other == null)
        {
            return false;
        }

        RemoteMaterial remoteMaterial = (RemoteMaterial)other;
        return this.AlbedoColor == remoteMaterial.AlbedoColor &&
            this.AlbedoTextureUrl == remoteMaterial.AlbedoTextureUrl &&
            this.AlphaClipThreshold == remoteMaterial.AlphaClipThreshold &&
            this.AOMapUrl == remoteMaterial.AOMapUrl &&
            this.AOScale == remoteMaterial.AOScale &&
            this.ColorFlags == remoteMaterial.ColorFlags &&
            this.ColorTransparencyMode == remoteMaterial.ColorTransparencyMode &&
            this.Name == remoteMaterial.Name &&
            this.FadeOut == remoteMaterial.FadeOut &&
            this.Metalness == remoteMaterial.Metalness &&
            this.MetalnessMapUrl == remoteMaterial.MetalnessMapUrl &&
            this.NormalMapUrl == remoteMaterial.NormalMapUrl &&
            this.PbrFlags == remoteMaterial.PbrFlags &&
            this.Roughness == remoteMaterial.Roughness &&
            this.RoughnessMapUrl == remoteMaterial.RoughnessMapUrl &&
            this.TexCoordOffset == remoteMaterial.TexCoordOffset &&
            this.TexCoordScale == remoteMaterial.TexCoordScale &&
            this.Type == remoteMaterial.Type &&
            this.VertexAlphaMode == remoteMaterial.VertexAlphaMode &&
            this.VertexMix == remoteMaterial.VertexMix;
    }

    public override int GetHashCode()
    {
        return this.AlbedoColor.GetHashCode() ^
            GetHashCode(this.AlbedoTextureUrl) ^
            this.AlphaClipThreshold.GetHashCode() ^
            GetHashCode(AOMapUrl) ^
            this.AOScale.GetHashCode() ^
            this.ColorFlags.GetHashCode() ^
            this.ColorTransparencyMode.GetHashCode() ^
            GetHashCode(this.Name) ^
            this.FadeOut.GetHashCode() ^
            this.Metalness.GetHashCode() ^
            GetHashCode(this.MetalnessMapUrl) ^
            GetHashCode(this.NormalMapUrl) ^
            this.PbrFlags.GetHashCode() ^
            this.Roughness.GetHashCode() ^
            GetHashCode(this.RoughnessMapUrl) ^
            this.TexCoordOffset.GetHashCode() ^
            this.TexCoordScale.GetHashCode() ^
            this.Type.GetHashCode() ^
            this.VertexAlphaMode.GetHashCode() ^
            this.VertexMix.GetHashCode();
    }
    #endregion Public Functions

    #region Private Functions
    private int GetHashCode(string value)
    {
        if (value == null)
        {
            return string.Empty.GetHashCode();
        }
        else
        {
            return value.GetHashCode();
        }
    }
    #endregion Private Functions
}

[Flags]
public enum RemotePbrMaterialFlags
{
    /// The material is transparent (alpha-blended), where the level of transparency is defined by albedo colors' alpha and optionally vertex colors' alpha.
    TransparentMaterial = 1,

    ///  Use/ignore the vertex color (if provided by the mesh). Needs to be enabled so that PbrMaterial.PbrVertexColorMode/PbrMaterial.PbrVertexAlphaMode has any effect.
    UseVertexColor = 2,

    /// The material is rendered double-sided, otherwise back faces are culled.
    DoubleSided = 4,

    /// Enables specular highlights for this material.
    SpecularEnabled = 8,

    /// Enables hard cut-outs on a per-pixel basis based on the alpha value being below a threshold. This works for opaque materials as well.
    AlphaClipped = 16,

    /// If enabled, this material fades to black as opposed to fading to transparent when used with SetFadeOut. Fading to black has the same effect
    /// on see-through devices like HoloLens but has less GPU cost associated with it.
    FadeToBlack = 32
};

[Flags]
public enum RemoteColorMaterialFlags
{
    /// Use/ignore the vertex color if provided by the mesh.
    UseVertexColor = 1,

    /// The material is rendered double-sided, otherwise back faces are culled.
    DoubleSided = 2,

    /// If enabled, this material fades to black as opposed to fading to transparent when used with SetFadeOut. Fading to black has the same effect
    /// on see-through devices like HoloLens but has less GPU cost associated with it.
    FadeToBlack = 4,

    /// Enables hard cut-outs on a per-pixel basis based on the alpha value being below a threshold. This works for opaque materials as well.
    AlphaClipped = 8
};

public static class RemoteMaterialFlagsExtensions
{
    public static PbrMaterialFeatures toRemote(this RemotePbrMaterialFlags flags)
    {
        Remote.PbrMaterialFeatures pbrFlags = PbrMaterialFeatures.None;
        if ((RemotePbrMaterialFlags.AlphaClipped & flags) == RemotePbrMaterialFlags.AlphaClipped)
        {
            pbrFlags |= Remote.PbrMaterialFeatures.AlphaClipped;
        }

        if ((RemotePbrMaterialFlags.DoubleSided & flags) == RemotePbrMaterialFlags.DoubleSided)
        {
            pbrFlags |= Remote.PbrMaterialFeatures.DoubleSided;
        }

        if ((RemotePbrMaterialFlags.FadeToBlack & flags) == RemotePbrMaterialFlags.FadeToBlack)
        {
            pbrFlags |= Remote.PbrMaterialFeatures.FadeToBlack;
        }

        if ((RemotePbrMaterialFlags.SpecularEnabled & flags) == RemotePbrMaterialFlags.SpecularEnabled)
        {
            pbrFlags |= Remote.PbrMaterialFeatures.SpecularHighlights;
        }

        if ((RemotePbrMaterialFlags.TransparentMaterial & flags) == RemotePbrMaterialFlags.TransparentMaterial)
        {
            pbrFlags |= Remote.PbrMaterialFeatures.TransparentMaterial;
        }

        if ((RemotePbrMaterialFlags.UseVertexColor & flags) == RemotePbrMaterialFlags.UseVertexColor)
        {
            pbrFlags |= Remote.PbrMaterialFeatures.UseVertexColor;
        }

        return pbrFlags;
    }

    public static ColorMaterialFeatures toRemote(this RemoteColorMaterialFlags flags)
    {
        ColorMaterialFeatures colorFlags = ColorMaterialFeatures.None;

        if ((RemoteColorMaterialFlags.AlphaClipped & flags) == RemoteColorMaterialFlags.AlphaClipped)
        {
            colorFlags |= ColorMaterialFeatures.AlphaClipped;
        }

        if ((RemoteColorMaterialFlags.DoubleSided & flags) == RemoteColorMaterialFlags.DoubleSided)
        {
            colorFlags |= ColorMaterialFeatures.DoubleSided;
        }

        if ((RemoteColorMaterialFlags.FadeToBlack & flags) == RemoteColorMaterialFlags.FadeToBlack)
        {
            colorFlags |= ColorMaterialFeatures.FadeToBlack;
        }

        if ((RemoteColorMaterialFlags.UseVertexColor & flags) == RemoteColorMaterialFlags.UseVertexColor)
        {
            colorFlags |= ColorMaterialFeatures.UseVertexColor;
        }

        return colorFlags;
    }
}
