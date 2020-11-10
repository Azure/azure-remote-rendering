// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Input
{
    /// <summary>
    /// Object that represents a mouse cursor in 3D space 
    /// </summary>
    public class UnityMouseCursor : MeshCursor
    {
        #region Serialized Fields
        [Header("Mouse Cursor Settings")]

        [SerializeField]
        [Tooltip("Should the mesh be shown with the Unity editor.")]
        private bool showTargetRendererInEditor = false;

        /// <summary>
        /// Should the mesh be shown with the Unity editor.
        /// </summary>
        public bool ShowTargetRendererInEditor
        {
            get => showTargetRendererInEditor;
            set => showTargetRendererInEditor = value;
        }

        [SerializeField]
        [Tooltip("Should the mesh be shown with the Unity player.")]
        private bool showTargetRendererInPlayer = true;

        /// <summary>
        /// Should the mesh be shown with the Unity editor.
        /// </summary>
        public bool ShowTargetRendererInPlayer
        {
            get => showTargetRendererInPlayer;
            set => showTargetRendererInPlayer = value;
        }
        #endregion Serialized Fields

        #region MonoBehavior Functions
        protected override void OnEnable()
        {
            base.OnEnable();
            if (Application.isEditor)
            {
                TargetRenderer.enabled = showTargetRendererInEditor;
            }
            else
            {
                TargetRenderer.enabled = showTargetRendererInPlayer;
            }
        }
        #endregion MonoBehavior Functions
    }
}
