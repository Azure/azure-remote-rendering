using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Rendering;
using System;
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
    private string loadedTextureUrl = null;
    private MaterialInstance imageMaterial = null;

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
            imageMaterial = imageRenderer.EnsureComponent<MaterialInstance>();
        }
    }

    private void OnEnable()
    {
        UpdateInfo(objectData);
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

    public void LoadModel()
    {
        listItem?.SetSelection(true);
        if (objectData != null && stage != null)
        {
            stage.Load(objectData);
        }
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
        if (assetName != null)
        {
            assetName.text = objectData.Name ?? "Unknown";
        }

        if (isActiveAndEnabled)
        {
            StartCoroutine(LoadTexture(objectData.ImageUrl));
        }
    }

    /// <summary>
    /// Load a texture from the given image data
    /// </summary>
    private IEnumerator LoadTexture(string imageUrl)
    {
        // if there is no renderer defined, exit early
        if (imageMaterial == null || !isActiveAndEnabled)
        {
            yield break;
        }

        // load remote image, if not loaded yet
        if (string.IsNullOrEmpty(imageUrl))
        {
            loadedTexture = imageFallback;
            loadedTextureUrl = null;
        }
        else if (loadedTextureUrl != imageUrl || loadedTexture == null)
        {
            // set fallback while waiting for things to load
            imageMaterial.Material.SetTexture(imageTextureName, imageFallback);
            loadedTextureUrl = null;
            UnityWebRequest www = UnityWebRequestTexture.GetTexture(imageUrl);
            yield return www.SendWebRequest();

            DownloadHandlerTexture downloadedTexture = null;
            if (www.isNetworkError || www.isHttpError)
            {
                Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, $"Failed to load image '{imageUrl}' ({www.error})");
            }
            else
            {
                downloadedTexture = www.downloadHandler as DownloadHandlerTexture;
            }
            loadedTexture = downloadedTexture?.texture ?? imageFallback;
            loadedTextureUrl = loadedTexture == null ? null : imageUrl;
        }

        // verify there is still a renderer to use
        if (imageMaterial != null && isActiveAndEnabled)
        {
            imageMaterial.Material.SetTexture(imageTextureName, loadedTexture);
        }
    }
    #endregion Private Methods
}
