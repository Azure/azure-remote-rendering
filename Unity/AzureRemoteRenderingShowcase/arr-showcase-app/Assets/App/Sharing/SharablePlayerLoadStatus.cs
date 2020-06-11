// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

/// <summary>
/// A class for receiving the loading state of non-local players.
/// </summary>
public class SharablePlayerLoadStatus : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    [Tooltip("The material to apply when a player has loaded all models.")]
    private Material loadedMaterial = null;

    /// <summary>
    /// The material to apply when a player has loaded all models.
    /// </summary>
    public Material LoadedMaterial
    {
        get => loadedMaterial;
        set => loadedMaterial = value;
    }

    [SerializeField]
    [Tooltip("The material to apply when a player is loading a model.")]
    private Material loadingMaterial = null;

    /// <summary>
    /// The material to apply when a player is loading a model.
    /// </summary>
    public Material LoadingMaterial
    {
        get => loadingMaterial;
        set => loadingMaterial = value;
    }

    [SerializeField]
    [Tooltip("The renderer whose material will be changed.")]
    private Renderer materialRenderer = null;

    /// <summary>
    /// The render whose material will be changed.
    /// </summary>
    public Renderer MaterialRenderer
    {
        get => materialRenderer;
        set => materialRenderer = value;
    }

    [SerializeField]
    [Tooltip("The player which will expose property changes.")]
    private SharablePlayer player = null;

    /// <summary>
    /// The player which will expose property changes.
    /// </summary>
    public SharablePlayer Player
    {
        get => player;
        set => player = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Properties
    private void Start()
    {
        if (player == null)
        {
            player = GetComponent<SharablePlayer>();
        }

        UpdateLoading(false);

        if (player != null)
        {
            player.PropertyChanged += OnPropertyChanged;
        }
    }

    private void OnDestroy()
    {
        if (player != null)
        {
            player.PropertyChanged -= OnPropertyChanged;
        }
    }
    #endregion MonoBehavior Properties

    #region Private Functions    
    /// <summary>
    /// Handle property changes received from the server.
    /// </summary>
    private void OnPropertyChanged(string property, object value)
    {
        if (property == SharableStrings.PlayerIsLoading && value is bool)
        {
            UpdateLoading((bool)value);
        }
    }

    /// <summary>
    /// Receive the player's loading status from the server, and update the renderer's shared material based on loading status.
    /// </summary>
    private void UpdateLoading(bool isLoading)
    {
        if (materialRenderer == null)
        {
            return;
        }

        materialRenderer.sharedMaterial = isLoading ? loadingMaterial : loadedMaterial;
    }
    #endregion Private Functions
}

