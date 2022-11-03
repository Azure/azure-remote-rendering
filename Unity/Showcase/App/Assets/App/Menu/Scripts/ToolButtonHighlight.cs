// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine;

/// <summary>
/// A tool's menu button within the application's tool menu.
/// </summary>
[RequireComponent(typeof(Interactable))]
public class ToolButtonHighlight : MonoBehaviour
{
    private Interactable _interactable = null;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The 'pointer mode' of this tool button.")]
    private PointerMode pointerMode;

    /// <summary>
    /// The game object to activate when the pointer is in the set 'pointer mode'.
    /// </summary>
    public PointerMode PointerMode
    {
        get => pointerMode;
        set => pointerMode = value;
    }

    [SerializeField]
    [Tooltip("The game object to activate when the pointer is in the set 'pointer mode'.")]
    private GameObject highlightObject;

    /// <summary>
    /// The game object to activate when the pointer is in the set 'pointer mode'.
    /// </summary>
    public GameObject HighlightObject
    {
        get => highlightObject;
        set => highlightObject = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Functions
    private void Start()
    {
        _interactable = GetComponent<Interactable>();

        if (_interactable != null)
        {
            _interactable.OnClick.AddListener(SetPointerMode);
        }
    }

    private void Update()
    {
        if (AppServices.PointerStateService.Mode == pointerMode)
        {
            if (!highlightObject.activeInHierarchy)
            {
                highlightObject.SetActive(true);
            }
        }
        else
        {
            if (highlightObject.activeInHierarchy)
            {
                highlightObject.SetActive(false);
            }
        }
    }

    private void OnDestroy()
    {
        if (_interactable != null)
        {
            _interactable.OnClick.RemoveListener(SetPointerMode);
        }
    }
    #endregion MonoBehavior Functions

    #region Private Functions
    private void SetPointerMode()
    {
        if (AppServices.PointerStateService.Mode == pointerMode)
        {
            AppServices.PointerStateService.Mode = PointerMode.None;
        }
        else
        {
            AppServices.PointerStateService.Mode = pointerMode;
        }
    }
    #endregion Private Functions
}
