// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.MixedReality.Toolkit.Input
{
    /// <summary>
    /// Represents a pointer that is able to handle remote ray cast results.
    /// </summary>
    public interface IRemotePointer
    {
        /// <summary>
        /// Get or set the most recent remote pointer result.
        /// </summary>
        IRemotePointerResult RemoteResult { get; set; }
    }
}
