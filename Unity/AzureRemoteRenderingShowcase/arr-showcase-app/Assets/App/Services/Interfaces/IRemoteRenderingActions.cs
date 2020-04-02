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
        /// Asynchronously load a model. This call will return immediately with an object that will emit an event when the model load has completed on the server.
        /// </summary>
        /// <param name="modelId">String identifier for the model.</param>
        LoadModelAsync LoadModelAsyncAsOperation(LoadModelFromSASParams inputParams);

        /// <summary>
        /// Load model with extended parameters
        /// </summary>
        /// <returns>Model details</returns>
        Task<LoadModelResult> LoadModelAsync(LoadModelFromSASParams inputParams);

        /// <summary>
        /// Asynchronously load a texture. This call will return immediately with an object that will emit an event when the texture load has completed on the server.
        /// </summary>
        /// <param name="textureId">String identifier for the texture.</param>
        /// <returns></returns>
        Task<Remote.Texture> LoadTextureAsync(LoadTextureFromSASParams inputParams);

        /// <summary>
        /// Asynchronously perform a raycast query on the remote scene.  This call will return immediately with an object that will emit an event when the raycast has returned from the server.
        /// The raycast will be performed on the server against the state of the world on the frame that the raycast was issued on.  Results will be sorted by distance, with the closest
        /// intersection to the user being the first item in the array.
        /// </summary>
        /// <param name="cast">Outgoing RayCast.</param>
        /// <returns></returns>
        Task<RayCastHit[]> RayCastQueryAsync(RayCast cast);

        /// <summary>
        /// Retrieves the remote focus point that will be used to present the current frame if FocusPointReprojectionMode is set to FocusPointMode.UseRemoteFocusPoint.
        /// </summary>
        /// <param name="position">Position in world space of the remote focus point. Only valid if return value != FocusPointResult.Invalid.</param>
        /// <param name="normal">Normal in world space of the remote focus point. Only valid if return value != FocusPointResult.Invalid.</param>
        /// <param name="velocity">Velocity in world space of the remote focus point. Only valid if return value != FocusPointResult.Invalid.</param>
        /// <returns>How to interpret the point data. If FocusPointResult.Invalid is returned, the data should not be used.</returns>
        FocusPointResult GetRemoteFocusPoint(System.IntPtr coordinateSystem, out Float3 position, out Float3 normal, out Float3 velocity);

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
        Task SetLighting(RemoteLightingData remoteLightingData);
    }
}
