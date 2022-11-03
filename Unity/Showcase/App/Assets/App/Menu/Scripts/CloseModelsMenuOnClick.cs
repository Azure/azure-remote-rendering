// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine;

/// <summary>
/// Logic to set the hand menu state after a new model has been selected to be placed.
/// </summary>
public class CloseModelsMenuOnClick : MonoBehaviour
{
    Interactable _interactable = null;

    #region MonoBehavior Functions
    private void Start()
    {
        _interactable = GetComponent<Interactable>();
        if (_interactable != null)
        {
            _interactable.OnClick.AddListener(CloseModelsMenu);
        }
    }

    private void OnDestroy()
    {
        if (_interactable != null)
        {
            _interactable.OnClick.RemoveListener(CloseModelsMenu);
            _interactable = null;
        }
    }
    #endregion MonoBehavior Functions

    #region Private Functions
    private void CloseModelsMenu()
    {
        HandMenuHooks handMenuHooks = GetComponentInParent<HandMenuHooks>();
        if (handMenuHooks)
        {
            // close menu
            handMenuHooks.ClearMenu();

            // set current mode to "none" so you don't accidentally delete or move the model
            AppServices.PointerStateService.Mode = PointerMode.None;
        }
    }
    #endregion Private Functions
}
