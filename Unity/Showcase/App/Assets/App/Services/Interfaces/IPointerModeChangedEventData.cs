// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

public interface IPointerModeChangedEventData
{
    /// <summary>
    /// The old pointer mode.
    /// </summary>
    PointerMode OldValue { get; }

    /// <summary>
    /// The old data associated with the old mode.
    /// </summary>
    object OldData { get; }

    /// <summary>
    /// The new pointer mode.
    /// </summary>
    PointerMode NewValue { get; }

    /// <summary>
    /// The new data associated with the new mode.
    /// </summary>
    object NewData { get; }
}
