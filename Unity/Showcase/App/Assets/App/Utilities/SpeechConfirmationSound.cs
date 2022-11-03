// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;

public class SpeechConfirmationSound : BaseInputHandler, IMixedRealitySpeechHandler
{
    #region Serialized Fields
    [SerializeField]
    [Tooltip("The audio source used to play the given audio clip when a speech command is recognized.")]
    private AudioSource audioSource = null;

    /// <summary>
    /// The audio source used to play the given audio clip when a speech command is recognized.
    /// </summary>
    public AudioSource AudioSource
    {
        get => audioSource;
        set => audioSource = value;
    }

    [SerializeField]
    [Tooltip("The audio clip to play when a speech command is recognized.")]
    private AudioClip audioClip = null;

    /// <summary>
    /// The audio clip to play when a speech command is recognized.
    /// </summary>
    public AudioClip AudioClip
    {
        get => audioClip;
        set => audioClip = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Functions
    protected override void Start()
    {
        base.Start();

        if (audioSource == null)
        {
            // Add audio source on main camera.
            audioSource = CameraCache.Main.gameObject.AddComponent<AudioSource>();
        }
    }
    #endregion MonoBehavior Functions

    #region InputSystemGlobalHandlerListener Implementation
    protected override void RegisterHandlers()
    {
        CoreServices.InputSystem?.RegisterHandler<IMixedRealitySpeechHandler>(this);
    }

    protected override void UnregisterHandlers()
    {
        CoreServices.InputSystem?.UnregisterHandler<IMixedRealitySpeechHandler>(this);
    }
    #endregion InputSystemGlobalHandlerListener Implementation

    #region IMixedRealitySpeechHandler Implementation
    void IMixedRealitySpeechHandler.OnSpeechKeywordRecognized(SpeechEventData eventData)
    {
        if (audioSource != null && audioClip != null)
        {
            audioSource.PlayOneShot(audioClip);
        }
    }
    #endregion  IMixedRealitySpeechHandler Implementation
}
