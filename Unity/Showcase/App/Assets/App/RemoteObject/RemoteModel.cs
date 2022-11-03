// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.Storage;
using System;
using System.Xml.Serialization;
using UnityEngine;

[Serializable]
public abstract class RemoteItemBase
{
    /// <summary>
    /// The object's name used for debugging purposes.
    /// </summary>
    public string Name;

    /// <summary>
    /// Is the object enabled in the scene
    /// </summary>
    public bool Enabled = true;

    /// <summary>
    /// The local transform of the model
    /// </summary>
    public ModelTransform Transform = new ModelTransform();

    public bool ShouldSerializeName()
    {
        return !string.IsNullOrEmpty(Name);
    }

    public bool ShouldSerializeEnabled() { return !Enabled; }

    public bool ShouldSerializeTransform()
    {
        return Transform != null &&
            (Transform.ShouldSerializeCenter() ||
            Transform.ShouldSerializePosition() ||
            Transform.ShouldSerializeRotation() ||
            Transform.ShouldSerializeScale());
    }

    /// <summary>
    /// Represents a model's initial local transform.
    /// </summary>
    [Serializable]
    public class ModelTransform
    {
        /// <summary>
        /// Should the model bounds be centered.
        /// </summary>
        public bool Center = false;

        /// <summary>
        /// Local position of the model
        /// </summary>
        public Vector3 Position = Vector3.zero;

        /// <summary>
        /// Local rotation of the model
        /// </summary>
        public Vector3 Rotation = Vector3.zero;

        /// <summary>
        /// Local scale of the model
        /// </summary>
        public Vector3 Scale = Vector3.one;

        /// <summary>
        /// The minimum size, in meters, of the object. If zero, this is ignored.
        /// </summary>
        public Vector3 MinSize = Vector3.zero;

        /// <summary>
        /// The maximum size, in meters, of the object. If zero, this is ignored.
        /// </summary>
        public Vector3 MaxSize = Vector3.zero;

        public bool ShouldSerializeCenter() { return Center; }

        public bool ShouldSerializePosition() { return Position != Vector3.zero; }

        public bool ShouldSerializeRotation() { return Rotation != Vector3.zero; }

        public bool ShouldSerializeScale() { return Scale != Vector3.one; }

        public bool ShouldSerializeMinSize() { return MinSize != Vector3.zero; }

        public bool ShouldSerializeMaxSize() { return MaxSize != Vector3.zero; }
    }
}

[Serializable]
public class RemoteContainer : RemoteItemBase
{
    /// <summary>
    /// The models and lights contained in the container.
    /// </summary>
    [XmlArray]
    [XmlArrayItem(typeof(RemoteModel), ElementName = "Model"),
     XmlArrayItem(typeof(RemotePlaceholderModel), ElementName = "Placeholder"),
     XmlArrayItem(typeof(RemoteDirectionalLight), ElementName = "DirectionalLight"),
     XmlArrayItem(typeof(RemotePointLight), ElementName = "PointLight"),
     XmlArrayItem(typeof(RemoteSpotlight), ElementName = "Spotlight")]
    public RemoteItemBase[] Items = new RemoteItemBase[0];

    /// <summary>
    /// Do any the remote objects have colliders
    /// </summary>
    public bool HasColliders = true;


    /// <summary>
    /// The preview thumbnail image url.
    /// </summary>
    public string ImageUrl;

    /// <summary>
    /// The camera overrides that could be applied to the remote camera.
    /// </summary>
    public RemoteCameraOverrides CameraOverrides = null;

    public bool HasRemoteModel()
    {
        if (Items == null || Items.Length == 0)
        {
            return false;
        }

        bool result = false;
        foreach (var item in Items)
        {
            if (item is RemoteModel)
            {
                result = true;
                break;
            }
        }
        return result;
    }

    public bool ShouldSerializeHasColliders() { return !HasColliders; }

    public bool ShouldSerializeItems()
    {
        return Items != null && Items.Length > 0;
    }

    public bool ShouldSerializeImageUrl()
    {
        return !string.IsNullOrEmpty(ImageUrl);
    }

    public bool ShouldSerializeCameraOverrides()
    {
        return CameraOverrides != null;
    }
}

/// <summary>
/// Represents a single ARR model.
/// </summary>
[Serializable]
public class RemoteModel : RemoteItemBase
{
    /// <summary>
    /// Extracts the path to the blob within the container.
    /// <returns>The blob path if it exists, empty otherwise.</returns>
    /// </summary>
    public string ExtractBlobPath()
    {
        return AzureStorageHelper.GetBlobName(Url);
    }

    /// <summary>
    /// Try to extract the container name from the Url path.
    /// <returns>The container name if the Url path has this information, null otherwise.</returns>
    /// </summary>
    public string ExtractContainerName()
    {
        return AzureStorageHelper.GetContainerName(Url);
    }

    /// <summary>
    /// The remote URL for the object's model file.
    /// </summary>
    public string Url;

    public bool ShouldSerializeUrl()
    {
        return !string.IsNullOrEmpty(Url);
    }
}

/// <summary>
/// Represents a version of a remote model that can be rendered locally.
/// </summary>
[Serializable]
public class RemotePlaceholderModel : RemoteItemBase
{
    /// <summary>
    /// The bundle URL containing the placeholders fbx file.
    /// </summary>
    public string Url;

    /// <summary>
    /// The name of the model inside the bundle.
    /// </summary>
    public string AssetName;

    public bool ShouldSerializeUrl()
    {
        return !string.IsNullOrEmpty(Url);
    }

    public bool ShouldSerializeAssetName()
    {
        return !string.IsNullOrEmpty(AssetName);
    }
}

/// <summary>
/// A base class representing a light source
/// </summary>
[Serializable]
public class RemoteLight : RemoteItemBase
{
    /// <summary>
    /// The remote light's color
    /// </summary>
    public Color Color = Color.white;

    /// <summary>
    /// The remote light's intensity
    /// </summary>
    /// <remarks>
    /// The intensity of the light. This value has no physical measure however it can be considered to be proportional to 
    /// the physical power of the light source. If the light has a fall-off (point and spotlight) this value also defines 
    /// the maximum range of light influence. An intensity of 1000 roughly has a range of 100 world units, but note this
    /// does not scale linearly.
    /// </remarks>
    public float Intensity = 10.0f;

    public bool ShouldSerializeColor()
    {
        return Color != Color.white;
    }
   
    public bool ShouldSerializeIntensity()
    {
        return Intensity != 10.0f;
    }
}

/// <summary>
/// A base class representing a directional light source
/// </summary>
[Serializable]
public class RemoteDirectionalLight : RemoteLight
{
}

/// <summary>
/// A base class representing a point light source
/// </summary>
[Serializable]
public class RemotePointLight : RemoteLight
{
    /// <summary>
    /// The point light's radius
    /// </summary>
    /// <remarks>
    /// If >0 the light emitting shape of the light source is a sphere of given radius as opposed to a point. 
    /// This for instance affects the appearance of specular highlights
    /// </remarks>
    public float Radius = 0.0f;

    /// <summary>
    /// The point light's length
    /// </summary>
    /// <remarks>
    /// If >0 (and also radius > 0) this value defines the length of a light emitting tube. Use case is a neon tube.
    /// </remarks>
    public float Length = 0.0f;

    /// <summary>
    /// The point light's attenuation cut off.
    /// </summary>
    /// <remarks>
    /// Defines a custom interval of min/max distances over which the light's attenuated intensity is scaled linearly 
    /// down to 0. This feature can be used to enforce a smaller range of influence of a specific light. If not defined 
    /// (default), these values are implicitly derived from the light's intensity.
    /// </remarks>
    public Vector2 AttenuationCutoff = Vector2.zero;

    /// <summary>
    /// A URL pointing to the point light's projected cube map texture.
    /// </summary>
    /// <remarks>
    /// In case a valid cubemap texture is passed here, the cubemap is projected using the orientation of the light.
    /// The cubemap's color is modulated with the light's color.
    /// </remarks>
    public string ProjectedCubeMap = null;

    public bool ShouldSerializeRadius()
    {
        return Radius != 0.0f;
    }

    public bool ShouldSerializeLength()
    {
        return Length != 0.0f;
    }

    public bool ShouldSerializeAttenuationCutoff()
    {
        return AttenuationCutoff != Vector2.zero;
    }

    public bool ShouldSerializeProjectedCubeMap()
    {
        return !string.IsNullOrEmpty(ProjectedCubeMap);
    }
}

/// <summary>
/// A base class representing a spotlight 
/// /// </summary>
[Serializable]
public class RemoteSpotlight : RemoteLight
{
    /// <summary>
    /// The light's radius
    /// </summary>
    /// <remarks>
    /// If >0 the light emitting shape of the light source is a sphere of given radius as opposed to a point. 
    /// This for instance affects the appearance of specular highlights
    /// </remarks>
    public float Radius = 0.0f;

    /// <summary>
    /// The light's attenuation cut off.
    /// </summary>
    /// <remarks>
    /// Defines a custom interval of min/max distances over which the light's attenuated intensity is scaled linearly 
    /// down to 0. This feature can be used to enforce a smaller range of influence of a specific light. If not defined 
    /// (default), these values are implicitly derived from the light's intensity.
    /// </remarks> 
    public Vector2 AttenuationCutoff = Vector2.zero;

    /// <summary>
    /// The spotlight's angle in degrees
    /// </summary>
    /// <remarks>
    /// This interval defines the inner and outer angle of the spot light cone both measured in degree. Everything
    /// within the inner angle is illuminated by the full brightness of the spot light source and a falloff is 
    /// applied towards the outer angle which generates a penumbra-like effect.
    /// </remarks>
    public Vector2 Angle = new Vector2(25.0f, 35.0f);

    /// <summary>
    /// The spotlight's falloff exponent
    /// </summary>
    /// <remarks>
    /// This interval defines the inner and outer angle of the spot light cone both measured in degree. Everything
    /// within the inner angle is illuminated by the full brightness of the spot light source and a falloff is 
    /// applied towards the outer angle which generates a penumbra-like effect.
    /// </remarks>
    public float Falloff = 1.0f;

    /// <summary>
    /// A URL pointing to the spotlight's projected 2D texture.
    /// </summary>
    /// <remarks>	
    /// In case a valid 2D texture is passed here, the texture is projected. The texture's color is modulated with the light's color.
    /// </remarks>
    public string Projected2DTexture = null;

    public bool ShouldSerializeRadius()
    {
        return Radius != 0.0f;
    }

    public bool ShouldSerializeAttenuationCutoff()
    {
        return AttenuationCutoff != Vector2.zero;
    }

    public bool ShouldSerializeAngle()
    {
        return Angle.x != 25.0f || Angle.y != 35.0f;
    }
    public bool ShouldSerializeFalloff()
    {
        return Falloff != 1.0f;
    }

    public bool ShouldSerializeProjected2DTexture()
    {
        return !string.IsNullOrEmpty(Projected2DTexture);
    }
}

[Serializable]
[XmlRootAttribute(ElementName = "Models")]
public class RemoteModelFile
{
    /// <summary>
    /// The containers stored within the file.
    /// </summary>
    [XmlArrayItem(ElementName = "Container")]
    public RemoteContainer[] Containers = new RemoteContainer[0];

    public bool ShouldSerializeContainers()
    {
        return Containers != null && Containers.Length > 0;
    }
}

/// <summary>
/// Represents camera overrides that could be applied to the remote camera.
/// </summary>
[Serializable]
public class RemoteCameraOverrides
{
    /// <summary>
    /// The near clip plane distance. If zero or less, value is ignored.
    /// </summary>
    public float NearClipPlane;

    /// <summary>
    /// The far clip plane distance. If zero or less, value is ignored.
    /// </summary>
    public float FarClipPlane;

    public bool ShouldSerializeNearClip()
    {
        return NearClipPlane > 0.0f;
    }

    public bool ShouldSerializeFarClip()
    {
        return FarClipPlane > 0.0f;
    }
}


