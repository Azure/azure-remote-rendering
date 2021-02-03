// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.Azure.RemoteRendering.Unity;
using Microsoft.MixedReality.Toolkit.Extensions;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Remote = Microsoft.Azure.RemoteRendering;

public class RemoteMaterialCache
{
    private IRemoteRenderingActions _actions;
    private Dictionary<string, Task<Remote.Material>> m_nameToMaterial = new Dictionary<string, Task<Remote.Material>>();
    private Dictionary<string, Task<Remote.Texture>> m_urlToTexture = new Dictionary<string, Task<Remote.Texture>>();

    public RemoteMaterialCache(IRemoteRenderingActions actions)
    {
        _actions = actions ?? throw new ArgumentNullException("Remote actions can't be null");
    }

    public Task<Remote.Material> LoadMaterial(RemoteMaterial material)
    {
        if (material == null)
        {
            return Task.FromResult<Remote.Material>(null);
        }

        Task<Remote.Material> remoteMaterial = null;
        lock (m_nameToMaterial)
        {
            if (!string.IsNullOrEmpty(material.Name) &&
                m_nameToMaterial.ContainsKey(material.Name))
            {
                remoteMaterial = m_nameToMaterial[material.Name];
                if (remoteMaterial.IsCanceled || remoteMaterial.IsFaulted)
                {
                    remoteMaterial = null;
                }
            }
        }

        if (remoteMaterial == null)
        {
            remoteMaterial = InsertAndLoadRemoteMaterial(material);
        }

        return remoteMaterial;
    }

    private Task<Remote.Material> InsertAndLoadRemoteMaterial(RemoteMaterial material)
    {
        Task<Remote.Material> result = null;
        lock (m_nameToMaterial)
        {
            if (material.Type == MaterialType.Pbr)
            {
                result = InitializePhysicalMaterial(material);
            }
            else
            {
                result = InitializeColorMaterial(material);
            }

            if (!string.IsNullOrEmpty(material.Name))
            {
                m_nameToMaterial[material.Name] = result;
            }
        }
        return result;
    }

    private async Task<Remote.Material> InitializePhysicalMaterial(RemoteMaterial material)
    {
        var remoteMaterial = RemoteManagerUnity.CurrentSession?.Connection.CreateMaterial(MaterialType.Pbr);
        remoteMaterial.Name = material.Name;

        PbrMaterial pbrMaterial = remoteMaterial as PbrMaterial;
        pbrMaterial.AlbedoColor = material.AlbedoColor.toRemoteColor4();
        pbrMaterial.AlphaClipThreshold = material.AlphaClipThreshold;
        pbrMaterial.AOScale = material.AOScale;
        pbrMaterial.FadeOut = material.FadeOut;
        pbrMaterial.Metalness = material.Metalness;
        pbrMaterial.PbrFlags = material.PbrFlags.toRemote();
        pbrMaterial.PbrVertexAlphaMode = material.VertexAlphaMode;
        pbrMaterial.Roughness = material.Roughness;
        pbrMaterial.TexCoordOffset = material.TexCoordOffset.toRemote();
        pbrMaterial.TexCoordScale = material.TexCoordScale.toRemote();

        Task<Texture>[] textureLoads = new Task<Texture>[]
        {
            LoadTextureFromCache(material.AlbedoTextureUrl),
            LoadTextureFromCache(material.AOMapUrl),
            LoadTextureFromCache(material.MetalnessMapUrl),
            LoadTextureFromCache(material.NormalMapUrl),
            LoadTextureFromCache(material.RoughnessMapUrl)
        };

        Texture[] textures = await Task.WhenAll(textureLoads);

        int textureIndex = 0;
        pbrMaterial.AlbedoTexture = textures[textureIndex++];
        pbrMaterial.AOMap = textures[textureIndex++];
        pbrMaterial.MetalnessMap = textures[textureIndex++];
        pbrMaterial.NormalMap = textures[textureIndex++];
        pbrMaterial.RoughnessMap = textures[textureIndex++];

        return remoteMaterial;
    }

    private async Task<Remote.Material> InitializeColorMaterial(RemoteMaterial material)
    {
        var remoteMaterial = RemoteManagerUnity.CurrentSession?.Connection.CreateMaterial(MaterialType.Color);
        remoteMaterial.Name = material.Name;

        ColorMaterial colorMaterial = remoteMaterial as ColorMaterial;
        colorMaterial.AlbedoColor = material.AlbedoColor.toRemoteColor4();
        colorMaterial.AlphaClipThreshold = material.AlphaClipThreshold;
        colorMaterial.ColorFlags = material.ColorFlags.toRemote();
        colorMaterial.ColorTransparencyMode = material.ColorTransparencyMode;
        colorMaterial.FadeOut = material.FadeOut;
        colorMaterial.TexCoordOffset = material.TexCoordOffset.toRemote();
        colorMaterial.TexCoordScale = material.TexCoordScale.toRemote();
        colorMaterial.VertexMix = material.VertexMix;

        if (!String.IsNullOrEmpty(material.AlbedoTextureUrl))
        {
            colorMaterial.AlbedoTexture = await LoadTextureFromCache(material.AlbedoTextureUrl);
        }

        return remoteMaterial;
    }

    /// <summary>
    /// Load a texture and place it in our cache
    /// </summary>
    private Task<Texture> LoadTextureFromCache(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return Task.FromResult<Texture>(null);
        }

        Task<Remote.Texture> result = null;
        lock (m_urlToTexture)
        {
            if (!m_urlToTexture.TryGetValue(url, out result))
            {
                m_urlToTexture[url] = result = SafeLoadTexture(url);
            }
        }
        return result;
    }

    /// <summary>
    /// Ignore any failures during texture load
    /// </summary>
    private async Task<Texture> SafeLoadTexture(string url)
    {
        if (_actions == null ||
            !_actions.IsValid())
        {
            return null;
        }

        Texture result = null;
        try
        {
            result = await _actions.LoadTexture2D(url);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogFormat(UnityEngine.LogType.Warning, UnityEngine.LogOption.NoStacktrace, null, "{0}",  $"Failed to load texture '{url}'. Reason: {ex.Message}");
        }

        return result;
    }
}
