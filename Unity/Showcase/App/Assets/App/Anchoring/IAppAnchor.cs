// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// This wraps an Azure Spatial Anchor will specific app logic.
    /// </summary>
    public interface IAppAnchor : IDisposable
    {
        /// <summary>
        /// The debug name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Get the anchor id.
        /// </summary>
        string AnchorId { get; }

        /// <summary>
        /// Get the located native anchor transform.
        /// </summary>
        Transform Transform { get; }

        /// <summary>
        /// Get the position of the anchor
        /// </summary>
        Vector3 Position { get; }

        /// <summary>
        /// Get the rotation of the anchor.
        /// </summary>
        Quaternion Rotation { get; }

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
        /// Get the ARAnchor if it exists
        /// </summary>
        public ARAnchor ArAnchor { get; }

        /// <summary>
        /// Event raise when the cloud anchor has changed.
        /// </summary>
        event Action<IAppAnchor, string> AnchorIdChanged;

        /// <summary>
        /// Try to move the anchor to this new position.
        /// </summary>
        Task Move(Transform transform);

        /// <summary>
        /// Start moving the cloud and/or native anchor to a new position, and complete a task once finished.
        /// </summary>
        Task Move(Pose pose);

        /// <summary>
        /// Delete the native and cloud anchors.
        /// </summary>
        void Delete();
    }
}
