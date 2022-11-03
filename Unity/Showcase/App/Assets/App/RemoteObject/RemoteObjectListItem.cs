// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.Rendering;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class RemoteObjectListItem : ListItemEventHandler
{
    private RemoteContainer objectData = null;
    private ListItem listItem = null;
    private RemoteObjectStage stage = null;
    private Texture2D loadedTexture = null;
    private string loadingTextureUrl = null;
    private string loadedTextureUrl = null;
    private UnityWebRequest loadingTextureRequest = null;
    private Material imageMaterial = null;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("This should be lest untouched, and is meant for debugging only. This is the index of the list item.")]
    private int index = -1;

    [SerializeField]
    [Tooltip("This is the label field that holds the asset name.")]
    private TextMeshPro assetName = null;

    /// <summary>
    /// This is the label field that holds the asset name.
    /// </summary>
    public TextMeshPro AssetName
    {
        get => assetName;
        set => assetName = value;
    }

    [SerializeField]
    [Tooltip("The renderer containing the asset preview image.")]
    private Renderer imageRenderer = null;

    /// <summary>
    /// The renderer containing the asset preview image.
    /// </summary>
    public Renderer ImageRenderer
    {
        get => imageRenderer;
        set => imageRenderer = value;
    }

    [SerializeField]
    [Tooltip("The texture name to set with a loaded image.")]
    private string imageTextureName = "_MainTex";

    /// <summary>
    /// The texture name to set with a loaded image.
    /// </summary>
    public string ImageTextureName
    {
        get => imageTextureName;
        set => imageTextureName = value;
    }

    [SerializeField]
    [Tooltip("The texture used if image fails to load from data object.")]
    private Texture2D imageFallback = null;

    /// <summary>
    /// The texture used if image fails to load from data object.
    /// </summary>
    public Texture2D ImageFallback
    {
        get => imageFallback;
        set => imageFallback = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Methods
    private void Awake()
    {
        if (imageRenderer != null)
        {
            var materialInstance = imageRenderer.EnsureComponent<MaterialInstance>();

            // Get material so to set image later. 
            imageMaterial = materialInstance.AcquireExistingMaterial();
        }
    }

    private void OnEnable()
    {
        StartLoadingTexture();
    }

    private void OnDisable()
    {
        loadingTextureUrl = null;
        DestoryRequest(loadingTextureRequest);
    }

    private void OnDestroy()
    {
        // Manually destroy previous web texture to prevent memory leak
        DestroyTexture(loadedTexture);
    }
    #endregion MonoBehavior Methods

    #region Public Methods
    public override void OnDataSourceChanged(ListItem item, System.Object oldValue, System.Object newValue)
    {
        UpdateInfo(newValue as RemoteContainer);
        listItem = item;
        stage = item?.Parent?.GetComponent<RemoteObjectsList>()?.Stage;
    }

    public override void OnIndexChanged(ListItem item, int oldValue, int newValue)
    {
        index = newValue;
    }

    public async void LoadModel()
    {
        listItem?.SetSelection(true);
        await RemoteObjectHelper.Spawn(objectData);
    }
    #endregion Public Methods

    #region Private Methods
    private void UpdateInfo(RemoteContainer data)
    {
        if (data == null)
        {
            return;
        }

        objectData = data;
        UpdateText();
        StartLoadingTexture();
    }

    /// <summary>
    /// Update text is text render is displaying something different
    /// </summary>
    private void UpdateText()
    {
        string assetNameString = objectData?.Name ?? "Unknown";
        if (assetName != null &&
            assetName.text != assetNameString)
        {
            assetName.text = assetNameString;
        }
    }

    /// <summary>
    /// Start loading text if component is enabled.
    /// </summary>
    private void StartLoadingTexture()
    {
        if (isActiveAndEnabled)
        {
            StartCoroutine(LoadTexture(objectData?.ImageUrl));
        }
    }

    /// <summary>
    /// Load a texture from the given image data
    /// </summary>
    private IEnumerator LoadTexture(string imageUrl)
    {
        // Use empty string if null
        if (imageUrl == null)
        {
            imageUrl = string.Empty;
        }

        // If no change exit early
        if (imageUrl == loadingTextureUrl || imageUrl == loadedTextureUrl)
        {
            yield break;
        }

        // If there is no renderer defined, exit early
        if (imageMaterial == null)
        {
            yield break;
        }

        // Capture a material to change
        Material destinationMaterial = imageMaterial; 
        if (destinationMaterial == null)
        {
            yield break;
        }

        // Save old texture to destroy later if needed
        Texture2D oldLoadedTexture = loadedTexture;
        Texture2D newLoadedTexture = null;

        // Set fallback while waiting for things to load
        destinationMaterial.SetTexture(imageTextureName, imageFallback);
        loadedTexture = imageFallback;

        // And set loaded url to avoid double loads
        loadingTextureUrl = imageUrl;

        DestoryRequest(loadingTextureRequest);
        if (!string.IsNullOrEmpty(loadingTextureUrl))
        {
            var www = loadingTextureRequest = UnityWebRequestTexture.GetTexture(loadingTextureUrl);
            yield return www.SendWebRequest();

            DownloadHandlerTexture downloadedTexture = null;
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}", $"Failed to load image '{imageUrl}' ({www.error}) ({www.result})");
            }
            else
            {
                downloadedTexture = www.downloadHandler as DownloadHandlerTexture;
            }

            if (downloadedTexture != null && downloadedTexture.texture != null)
            {
                newLoadedTexture = downloadedTexture.texture;
            }

            DestoryRequest(www);
        }

        // Verify things are still active, and texture is still being loaded
        if (isActiveAndEnabled && newLoadedTexture != null && destinationMaterial != null && loadingTextureUrl == imageUrl)
        {
            destinationMaterial.SetTexture(imageTextureName, newLoadedTexture);
            loadedTexture = newLoadedTexture;
            loadedTextureUrl = loadingTextureUrl;
        }
        else
        {
            DestroyTexture(newLoadedTexture);
        }

        // Clear loading url as needed
        if (loadingTextureUrl == imageUrl)
        {
            loadingTextureUrl = null;
        }

        // Manually destroy previous web texture to prevent memory leak
        DestroyTexture(oldLoadedTexture);
    }

    private void DestroyTexture(Texture2D texture)
    {
        if (texture != null && texture != imageFallback)
        {
            Destroy(texture);

            if (loadedTexture == texture)
            {
                loadedTexture = null;
            }
        }
    }

    private void DestoryRequest(UnityWebRequest request)
    {
        if (request != null)
        {
            if (!request.isDone)
            {
                Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", $"Aborting image load. url: {request.url}");
                request.Abort();
            }

            request.Dispose();

            if (request == loadingTextureRequest)
            {
                loadingTextureRequest = null;
            }
        }
    }
    #endregion Private Methods
}
