// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using TMPro;
using UnityEngine;

/// <summary>
/// A helper to show and hide a game object's renderers and text. 
/// </summary>
public class RendererVisibility : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    [Tooltip("The target game objects to also show and hide")]
    private GameObject[] targets = new GameObject[0];

    /// <summary>
    /// The target game objects to also show and hide
    /// </summary>
    public GameObject[] Targets
    {
        get => targets;
        set => targets = value;
    }

    [SerializeField]
    [Tooltip("Auto disable this component at awake, so the avatar is hidden.")]
    private bool autoDisable = true;

    /// <summary>
    /// Auto disable this component at awake, so the avatar is hidden.
    /// </summary>
    public bool AutoDisable
    {
        get => autoDisable;
        set => autoDisable = value;
    }


    [SerializeField]
    [Tooltip("Should text be visible always")]
    private bool textVisibleAlways = false;

    /// <summary>
    /// Should text be visible always
    /// </summary>
    public bool TextVisibleAlways
    {
        get => textVisibleAlways;

        set
        {
            if (textVisibleAlways != value)
            {
                textVisibleAlways = value;
                UpdateEnabled();
            }
        }
    }
    #endregion Serialized Fields

    #region MonoBehavior Functions
    private void Awake()
    {
        if (autoDisable)
        {
            enabled = false;
            UpdateEnabled();
        }
    }

    private void OnEnable()
    {
        UpdateEnabled();
    }

    private void OnDisable()
    {
        UpdateEnabled();
    }
    #endregion MonoBehavior Functions

    #region Public Functions
    public void Refresh()
    {
        UpdateEnabled();
    }
    #endregion Public Functions

    #region Private Functions
    private void UpdateEnabled()
    {
        bool show = isActiveAndEnabled;

        // Only show target game objects if visible and positioned.
        UpdateTargets(isActive: show);

        // Only show renders if visible and positioned
        UpdateEnabledOfRenderer<Renderer>(isEnabled: show);

        // Only use colliders if not local
        UpdateEnabledOfCollider(isEnabled: show);

        // Only hide text when position is still not known. This is so names appears above co-located users
        UpdateEnabledOfTextMeshPro(isEnabled: show || textVisibleAlways);
    }

    private void UpdateTargets(bool isActive)
    {
        if (targets != null)
        {
            int count = targets.Length;
            for (int i = 0; i < count; i++)
            {
                var target = targets[i];
                if (target != null)
                {
                    target.SetActive(isActive);
                }
            }
        }
    }

    private void UpdateEnabledOfRenderer<T>(bool isEnabled) where T : Renderer
    {
        var componenets = gameObject.GetComponentsInChildren<T>(includeInactive: true);
        foreach (var component in componenets)
        {
            component.enabled = isEnabled;
        }
    }

    private void UpdateEnabledOfBehavior<T>(bool isEnabled) where T : Behaviour
    {
        var componenets = gameObject.GetComponentsInChildren<T>(includeInactive: true);
        foreach (var component in componenets)
        {
            component.enabled = isEnabled;
        }
    }

    private void UpdateEnabledOfTextMeshPro(bool isEnabled)
    {
        var componenets = gameObject.GetComponentsInChildren<TextMeshPro>(includeInactive: true);
        foreach (var component in componenets)
        {
            component.enabled = isEnabled;
            component.renderer.enabled = isEnabled;
        }
    }

    private void UpdateEnabledOfCollider(bool isEnabled)
    {
        var componenets = gameObject.GetComponentsInChildren<Collider>(includeInactive: true);
        foreach (var component in componenets)
        {
            component.enabled = isEnabled;
        }
    }
    #endregion Private Functions
}

