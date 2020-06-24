// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Experimental.UI;
using Microsoft.MixedReality.Toolkit.Input;
using UnityEngine;

public class ObjectManipulatorEventsRedirect : MonoBehaviour, IMixedRealityPointerHandler, IMixedRealityFocusChangedHandler
{
    public ObjectManipulator redirectTarget;

    public void OnPointerDown(MixedRealityPointerEventData eventData)
    {
        redirectTarget.OnPointerDown(eventData);
    }

    public void OnPointerDragged(MixedRealityPointerEventData eventData)
    {
        redirectTarget.OnPointerDragged(eventData);
    }

    public void OnPointerUp(MixedRealityPointerEventData eventData)
    {
        redirectTarget.OnPointerUp(eventData);
    }

    public void OnPointerClicked(MixedRealityPointerEventData eventData)
    {
        redirectTarget.OnPointerClicked(eventData);
    }

    public void OnBeforeFocusChange(FocusEventData eventData)
    {
        redirectTarget.OnBeforeFocusChange(eventData);
    }

    public void OnFocusChanged(FocusEventData eventData)
    {
        redirectTarget.OnFocusChanged(eventData);
    }
}