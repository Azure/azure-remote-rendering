// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// A helper class containing functions for loading bundles.
/// </summary>

public static class BundleLoader
{
    private static Dictionary<string, Task<AssetBundle>> _loadedAssetBundles
        = new Dictionary<string, Task<AssetBundle>>();

    /// <summary>
    /// Load a model from a given bundle uri.
    /// </summary>
    public static async Task<GameObject> LoadModel(string bundleUri, string modelName)
    {
        GameObject gameObject = null;
        AssetBundle bundle = await Get(bundleUri);

        if (bundle == null)
        {
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  $"Unable to load model '{modelName}' from empty bundle '{bundleUri}'.");
        }
        else if (!bundle.Contains(modelName))
        {
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  $"Unable to load model '{modelName}'. Bundle '{bundleUri}' does not contain model.");
        }
        else
        {
            AssetBundleRequest request = bundle.LoadAssetAsync<GameObject>(modelName);

            bool failure = false;
            try
            {
                await request.AsTask();
            }
            catch (Exception ex)
            {
                failure = true;
                Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  $"Error loading model '{modelName}' from bundle '{bundleUri}'. Reason: {ex.Message}");
            }

            if (!failure)
            {
                gameObject = request.asset as GameObject;
            }
        }

        return gameObject;
    }

    /// <summary>
    /// Load an asset bundle from a local or remote location.
    /// </summary>
    public static Task<AssetBundle> Get(string uri)
    {
        if (string.IsNullOrEmpty(uri))
        {
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  $"Unable to load asset bundle from an empty uri.");
            return Task.FromResult<AssetBundle>(null);
        }

        Task<AssetBundle> result;
        if (_loadedAssetBundles.TryGetValue(uri, out result))
        {
            return result;
        }

        if (uri.StartsWith("http", StringComparison.InvariantCultureIgnoreCase) ||
            uri.StartsWith("https", StringComparison.InvariantCultureIgnoreCase))
        {
            result = GetRemote(uri);
        }
        else
        {
            result = GetLocal(uri);
        }

        _loadedAssetBundles[uri] = result;
        return result;
    }

    /// <summary>
    /// Load an asset bundle from a local file path location.
    /// </summary>
    private static async Task<AssetBundle> GetLocal(string filepath)
    {
        if (string.IsNullOrEmpty(filepath))
        {
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  "Unable to load asset bundle from an empty file path.");
            return null;
        }

        AssetBundleCreateRequest request = AssetBundle.LoadFromFileAsync(filepath);

        bool failure = false;
        try
        {
            await request.AsTask();
        }
        catch (Exception ex)
        {
            failure = true;
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  $"Error making local request for bundle '{filepath}'. Reason: {ex.Message}");
        }

        AssetBundle bundle = null;
        if (!failure)
        {
            bundle = request.assetBundle;
        }

        return bundle;
    }

    /// <summary>
    /// Load an asset bundle from a remote location.
    /// </summary>
    private static async Task<AssetBundle> GetRemote(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  "Unable to load asset bundle from an empty URL.");
            return null;
        }

        UnityWebRequest webRequest = UnityWebRequestAssetBundle.GetAssetBundle(url);

        bool failure = false;
        try
        {
            await webRequest.SendWebRequest().AsTask();
            failure = webRequest.result != UnityWebRequest.Result.Success;
        }
        catch (Exception ex)
        {
            failure = true;
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  $"Error making web request for bundle '{url}'. Reason: {ex.Message}");
        }

        AssetBundle bundle = null;
        if (!failure)
        {
            bundle = DownloadHandlerAssetBundle.GetContent(webRequest);
        }
        else
        {
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  $"Error {webRequest.responseCode} received from web request for bundle '{url}'.");
        }

        return bundle;
    }
}
