// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using UnityEngine.Events;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// Represents a stage that help with positioning objects.
    /// </summary>
    public interface IRemoteObjectStage
    {
        /// <summary>
        /// Event raised when the staged object has changed.
        /// </summary>
        UnityEvent<RemoteObject> StagedObjectChanged { get; }

        /// <summary>
        /// Event raised when the unstaged objects have changed.
        /// </summary>
        UnityEvent<RemoteObject> UnstagedObjectsChanged { get; }

        /// <summary>
        /// Get if the stage visual is visible.
        /// </summary>
        bool IsStageVisible { get; }

        /// <summary>
        /// Stage the given object. This will delete the old staged object.
        /// </summary>
        void StageObject(GameObject item, bool reposition);

        /// <summary>
        /// Move this object to the unstaged area
        /// </summary>
        void UnstageObject(GameObject item, bool reposition);
    }
}
