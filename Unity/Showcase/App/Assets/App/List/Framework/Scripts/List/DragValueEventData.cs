// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Input;

/// <summary>
/// Event data given during drag value events.
/// </summary>
public class DragValueEventData
{
    public DragValueEventData(float o, float n, IMixedRealityPointer pointer, DragValue dragValue)
    {
        OldValue = o;
        NewValue = n;
        Pointer = pointer;
        DragValue = dragValue;
    }

    /// <summary>
    /// The previous value of the slider
    /// </summary>
    public float OldValue { get; }

    /// <summary>
    /// The current value of the slider
    /// </summary>
    public float NewValue { get; }

    /// <summary>
    /// The slider that triggered this event
    /// </summary>
    public DragValue DragValue { get; }

    /// <summary>
    /// The currently active pointer manipulating / hovering the slider,
    /// or null if no pointer is manipulating the slider.
    /// Note: OnSliderUpdated is called with Pointer == null
    /// OnStart, so always check if this field is null before using!
    /// </summary>
    public IMixedRealityPointer Pointer { get; }
}

