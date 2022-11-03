// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// Search for materials on child renderers.  Then change the shader of the materials to the specified one.
    /// </summary>
    public class AvatarMigrateMaterialShaders : MonoBehaviour
    {
        #region Serialized Fields
        [SerializeField]
        [Tooltip("The shader to migrate the found materials to.")]
        private Shader destinationShader = null;

        /// <summary>
        /// The shader to migrate the found materials to.
        /// </summary>
        public Shader DestinationShader
        {
            get => destinationShader;
            set => destinationShader = value;
        }
        #endregion Serialized Fields

        #region MonoBehavior Functions
        private void OnEnable()
        {
            MigrateMaterials();
        }

        private void OnTransformChildrenChanged()
        {
            MigrateMaterials();
        }
        #endregion MonoBehavior Functions

        #region Private Functions
        private void MigrateMaterials()
        {
            if (destinationShader == null)
            {
                return;
            }

            var renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers == null)
            {
                return;
            }

            foreach (Renderer renderer in renderers)
            {
                if (ShouldMigrateMaterials(renderer.sharedMaterials))
                {
                    MigrateMaterials(renderer.materials);
                }
            }
        }

        private bool ShouldMigrateMaterials(Material[] materials)
        {
            if (materials == null)
            {
                return false;
            }

            bool migrate = false;
            foreach (Material material in materials)
            {
                if (material.shader != destinationShader)
                {
                    migrate = true;
                    break;
                }
            }
            return migrate;
        }

        private void MigrateMaterials(Material[] materials)
        {
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i].shader = destinationShader;
            }
        }
        #endregion Private Functions
    }
}
