// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.Input;
using System.Collections.Generic;
using UnityEngine;

public class PointerModeHandler : MonoBehaviour, IMixedRealityPointerHandler
{
    private readonly Dictionary<PointerMode, PointerModeAndResponse> _pointerResponses = new Dictionary<PointerMode, PointerModeAndResponse>();

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The pointer modes to be recognized.")]
    private PointerModeAndResponse[] pointerModes = new PointerModeAndResponse[0];

    /// <summary>
    /// The pointer modes to be recognized.
    /// </summary>
    public PointerModeAndResponse[] PointerModes => pointerModes;
    #endregion Serialized Fields

    #region IMixedRealityPointerHandler Methods
    public void OnPointerDown(MixedRealityPointerEventData eventData)
    {
    }

    public void OnPointerDragged(MixedRealityPointerEventData eventData)
    {
    }

    public void OnPointerUp(MixedRealityPointerEventData eventData)
    {
    }

    public void OnPointerClicked(MixedRealityPointerEventData eventData)
    {
        HandleClick(eventData);
    }
    #endregion IMixedRealityPointerHandler Methods

    #region MonoBehaviour Implementation
    private void Start()
    {
        // Convert the struct array into a dictionary, with the keywords and the methods as the values.
        // This helps easily link the pointer mode to the UnityEvent to be invoked.
        int pointerModesCount = pointerModes.Length;
        for (int index = 0; index < pointerModesCount; index++)
        {
            PointerModeAndResponse pointerModeAndResponse = pointerModes[index];
            if (_pointerResponses.ContainsKey(pointerModeAndResponse.Mode))
            {
                Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  $"Duplicate pointer mode \'{pointerModeAndResponse.Mode}\' specified in \'{gameObject.name}\'.");
            }
            else
            {
                _pointerResponses.Add(pointerModeAndResponse.Mode, pointerModeAndResponse);
            }
        }

        // Listen for mode changes
        AppServices.PointerStateService.ModeChanged += PointerStateService_ModeChanged;

        // Notify listeners of initial deactivated state
        for (int i = 0; i < (int)PointerMode.Count; i++)
        {
            PointerMode mode = (PointerMode)i;
            if (mode != AppServices.PointerStateService.Mode)
            {
                HandleDisabledMode(mode, null);
            }
        }

        // Notify listeners of initial activated state
        HandleEnabledMode(AppServices.PointerStateService.Mode, AppServices.PointerStateService.ModeData);
    }
    #endregion MonoBehavior Implementation

    #region Private Methods
    private void HandleEnabledMode(PointerMode mode, object modeData)
    {
        PointerModeAndResponse response = default;
        if (_pointerResponses.TryGetValue(mode, out response))
        {
            response.Enabled?.Invoke(mode, modeData);
        }
    }

    private void HandleDisabledMode(PointerMode mode, object modeData)
    {
        PointerModeAndResponse response = default;
        if (_pointerResponses.TryGetValue(mode, out response))
        {
            response.Disabled?.Invoke(mode, modeData);
        }
    }

    private void HandleClick(MixedRealityPointerEventData eventData)
    {
        PointerModeAndResponse response = default;
        if (_pointerResponses.TryGetValue(AppServices.PointerStateService.Mode, out response))
        {
            response.Clicked?.Invoke(eventData, AppServices.PointerStateService.ModeData);
        }
    }

    private void PointerStateService_ModeChanged(object sender, IPointerModeChangedEventData e)
    {
        HandleDisabledMode(e.OldValue, e.OldData);
        HandleEnabledMode(e.NewValue, e.NewData);
    }
    #endregion Private Methods
}
