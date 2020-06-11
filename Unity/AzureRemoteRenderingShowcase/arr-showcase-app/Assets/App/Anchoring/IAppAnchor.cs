// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// This wraps an Azure Spatial Anchor will specific app logic.
    /// </summary>
    public interface IAppAnchor : IDisposable
    {
        /// <summary>
        /// Get the anchor id.
        /// </summary>
        string AnchorId { get; }

        /// <summary>
        /// Get the located native anchor transform.
        /// </summary>
        Transform Transform { get; }

        /// <summary>
        /// Did this anchor start from a native anchor. If true, the anchor was initialized from a native anchor.
        /// If false, the anchor was initialized from a cloud anchor id. 
        /// </summary>
        bool FromNative { get; }

        /// <summary>
        /// Did this anchor start from a cloud anchor. If true, the anchor was initialized from a cloud anchor id.
        /// If false, the anchor was initialized from a native anchor. 
        /// </summary>
        bool FromCloud { get; }

        /// <summary>
        /// Get if the anchor has been located
        /// </summary>
        bool IsLocated { get; }

        /// <summary>
        /// Event raised when the cloud anchor has been located.
        /// </summary>
        event Action<IAppAnchor> Located;

        /// <summary>
        /// Event raise when the cloud anchor has changed.
        /// </summary>
        event Action<IAppAnchor, string> AnchorIdChanged;

        /// <summary>
        /// Try to move the anchor to this new position.
        /// </summary>
        void Move(Transform transform);

        /// <summary>
        /// Delete the native and cloud anchors.
        /// </summary>
        void Delete();
    }
}
