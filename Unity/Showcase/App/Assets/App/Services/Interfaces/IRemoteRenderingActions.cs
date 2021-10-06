// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Azure.RemoteRendering;

using Remote = Microsoft.Azure.RemoteRendering;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// The rendering actions that can be performed on the remote machine
    /// </summary>
    public interface IRemoteRenderingActions
    {
        /// <summary>
        /// Is this current action object still valid.
        /// </summary>
        bool IsValid();

        /// <summary>
        /// Asynchronously loads a model from a linked storage account or a publicly accessible container.
        /// This call will return immediately with an object that will emit an event when the model load has completed on the server.
        /// </summary>
        /// <param name="model">The model to load.</param>
        /// <param name="parent">The parent of the model.</param>
        /// <returns></returns>
        Task<LoadModelResult> LoadModelAsyncAsOperation(RemoteModel model, Entity parent, ModelProgressStatus progress);

        /// <summary>
        /// Loads a model from a linked storage account or a publicly accessible container.
        /// </summary>
        /// <param name="model">The model to load.</param>
        /// <param name="parent">The parent of the model.</param>
        /// <returns></returns>
        Task<LoadModelResult> LoadModelAsync(RemoteModel model, Entity parent);

        /// <summary>
        /// Asynchronously loads a texture from a linked storage account or a publicly accessible container.
        /// This call will return immediately with an object that will emit an event when the texture load has completed on the server.
        /// </summary>
        /// <param name="storageAccountName">The plain storage account name, .e.g., 'mystorageaccount'.</param>
        /// <param name="containerName">The name of the container within the storage account, e.g, 'mycontainer'.</param>
        /// <param name="blobPath">The path to the texture within the container, e.g., 'path/to/file/myFile.arrAsset.</param>
        /// <param name="textureType">The type of texture to load.</param>
        /// <returns></returns>
        Task<Remote.Texture> LoadTextureAsync(string storageAccountName, string containerName, string blobPath, TextureType textureType);

        /// <summary>
        /// Asynchronously perform a raycast query on the remote scene.  This call will return immediately with an object that will emit an event when the raycast has returned from the server.
        /// The raycast will be performed on the server against the state of the world on the frame that the raycast was issued on.  Results will be sorted by distance, with the closest
        /// intersection to the user being the first item in the array.
        /// </summary>
        /// <param name="cast">Outgoing RayCast.</param>
        /// <returns></returns>
        Task<RayCastQueryResult> RayCastQueryAsync(RayCast cast);

        /// <summary>
        ///  Create a new entity on the server. The new entity can be inserted into the scenegraph and have components added to it.
        /// </summary>
        /// <returns>Newly created entity.</returns>
        Entity CreateEntity();

        /// <summary>
        ///  Create a new material on the server. The new material can be set to mesh components.
        /// </summary>
        /// <param name="type">Type of created material.</param>
        /// <returns>Newly created material.</returns>
        Remote.Material CreateMaterial(MaterialType type);

        /// <summary>
        ///  Create a new component locally and on the server. This call can fail if the entity already has a component of componentType on it.
        /// </summary>
        /// <param name="componentType">Component type to create.</param>
        /// <param name="owner">Owner of the component.</param>
        /// <returns>A newly created component or null if the call failed.</returns>
        ComponentBase CreateComponent(ObjectType componentType, Entity owner);

        /// <summary>
        /// Returns global camera settings.
        /// </summary>
        CameraSettings GetCameraSettings();

        /// <summary>
        /// Returns global sky reflection settings.
        /// </summary>
        SkyReflectionSettings GetSkyReflectionSettings();

        /// <summary>
        /// Returns global outline settings.
        /// </summary>
        OutlineSettings GetOutlineSettings();

        /// <summary>
        /// Returns global z-fighting mitigation state.
        /// </summary>
        ZFightingMitigationSettings GetZFightingMitigationSettings();

        /// <summary>
        /// Load a remote material, from a data object.
        /// </summary>
        /// <remarks>
        /// Move this to material factory...Session should know about RemoteMaterial, as it leads to a circular data flow.
        /// </remarks>
        Task<Remote.Material> LoadMaterial(RemoteMaterial material);

        /// <summary>
        /// Load a 2D texture.
        /// </summary>
        Task<Remote.Texture> LoadTexture2D(string url);

        /// <summary>
        /// Load a cube map texture.
        /// </summary>
        Task<Remote.Texture> LoadTextureCubeMap(string url);

        /// <summary>
        /// Load and set cube map
        /// </summary>
        Task SetLighting(string url);
    }
}
