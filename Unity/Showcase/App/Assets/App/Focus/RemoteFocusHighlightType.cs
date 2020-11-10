// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

/// <summary>
/// Indicates what part of the model should be highlighted when selected or focused (whole model or piece of model).
/// </summary>
public enum RemoteFocusHighlightType

{
    /// <summary>
    /// No visual for selection highlighting
    /// </summary>
    None,

    /// <summary>
    /// Draw a highlight on or around the whole of the selected (or focused) object
    /// </summary>
    Whole,

    /// <summary>
    /// Draw a highlight on or around the piece of the selected (or focused) object
    /// </summary>
    Piece,
}
