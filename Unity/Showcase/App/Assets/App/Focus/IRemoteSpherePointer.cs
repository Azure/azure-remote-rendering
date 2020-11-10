// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Input;

/// <summary>
/// Represents a sphere pointer that can handle remote objects
/// </summary>
public interface IRemoteSpherePointer : IRemotePointer
{
    /// <summary>
    /// Get or set if this pointer is near a remote grabbable object.
    /// </summary>
    bool IsNearRemoteGrabbable { get; set; }
}
