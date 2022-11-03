// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

/// <summary>
/// The types of modes you can put a pointer into. These are generally actions that can be taken on objects.
/// </summary>
/// <remarks>
/// If adding a new enum value, place before "Count" and add a number value. Do not change the other number values,
/// as this could break serialized data.
/// </remarks>
[Serializable]
public enum PointerMode
{
    // DO NOT CHANGE THE VALUES OF THESE ENUM VALUES

    None = 0,

    /// <summary>
    /// Manipulate the whole object.
    /// </summary>
    Manipulate = 1,

    /// <summary>
    /// Manipulate pieces of the object.
    /// </summary>
    ManipulatePiece = 2,

    /// <summary>
    /// Explode the object.
    /// </summary>
    Explode = 3,

    /// <summary>
    /// Delete the object.
    /// </summary>
    Delete = 4,

    /// <summary>
    /// Reset the target objects
    /// </summary>
    Reset = 5,

    /// <summary>
    /// Start clipping all objects
    /// </summary>
    ClipBar = 6,
    ClipHands = 7,

    /// <summary>
    /// Change the material of the object
    /// </summary>
    Material = 8,

    /// <summary>
    /// Turn on the draw tool
    /// </summary>
    Draw = 9,

    //
    // Add new values above "Count" and "Invalid"
    //

    /// <summary>
    /// Used for iterating over all action type. Keep at end, before 'Invalid'
    /// </summary>
    Count,

    /// <summary>
    /// And invalid pointer mode
    /// </summary>
    Invalid,
}
