// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// A behavior for applying color property values to renderer materials.
    /// </summary>
    [RequireComponent(typeof(AvatarComponentCollection))]
    public class AvatarColorProperties : AvatarComponent
    {
        private MaterialPropertyBlock _materialPropertyBlock = null;
        private HashSet<string> _colorParts = new HashSet<string>();
        private Dictionary<string, int> _materialPropertyIds = new Dictionary<string, int>();


        #region Serialized Fields
        [SerializeField]
        [Tooltip("The colorable parts that will be updated with color property changes.")]
        private AvatarColorablePart[] colorableParts = null;

        /// <summary>
        /// The colorable parts that will be updated with color property changes.
        /// </summary>
        public AvatarColorablePart[] ColorableParts
        {
            get => colorableParts;

            set
            {
                colorableParts = value;
                InitializeColorParts();
            }
        }
        #endregion Serialized Fields

        #region MonoBehavior Functions
        private void OnTransformChildrenChanged()
        {
            UpdateNamedColorParts();
        }
        #endregion MonoBehavior Functions

        #region Protected Functions
        /// <summary>
        /// Implement to handle component being intialized
        /// </summary>
        protected override void OnInitialized()
        {
            InitializeColorParts();
        }

        /// <summary>
        /// Implement to handle property changes for the current participant.
        /// </summary>
        protected override void OnPropertyChanged(string name, object value)
        {
            TryUpdateColors(name);
        }
        #endregion Protected Functions

        #region Private Functions
        private void InitializeColorParts()
        {
            _materialPropertyBlock = new MaterialPropertyBlock();
            _colorParts.Clear();
            _materialPropertyIds.Clear();

            int length = colorableParts?.Length ?? 0;
            for (int i = 0; i < length; i++)
            {
                var entry = colorableParts[i];

                if (!string.IsNullOrEmpty(entry.materialPropertyName))
                {
                    _materialPropertyIds[entry.materialPropertyName] = Shader.PropertyToID(entry.materialPropertyName);
                }

                if (!string.IsNullOrEmpty(entry.property))
                {
                    _colorParts.Add(entry.property);
                    if (TryGetProperty(entry.property, out Color value))
                    {
                        UpdateColorPart(ref entry, value);
                    }
                }
            }
        }

        private void UpdateNamedColorParts()
        {
            if (!IsInitialized)
            {
                return;
            }

            int length = colorableParts?.Length ?? 0;
            for (int i = 0; i < length; i++)
            {
                var entry = colorableParts[i];

                if (entry.renderer == null &&
                    !string.IsNullOrEmpty(entry.rendererName) &&
                    !string.IsNullOrEmpty(entry.property) &&
                    TryGetProperty(entry.property, out Color value))
                {
                    UpdateColorPart(ref entry, value);
                }
            }
        }

        private void TryUpdateColors(string propertyName)
        {
            if (_colorParts != null &&
                _colorParts.Contains(propertyName) &&
                TryGetProperty(propertyName, out Color value))
            {
                int length = colorableParts.Length;
                for (int i = 0; i < length; i++)
                {
                    var entry = colorableParts[i];
                    if (entry.property == propertyName)
                    {
                        UpdateColorPart(ref entry, value);
                    }
                }
            }
        }

        public void UpdateColorPart(ref AvatarColorablePart entry, Color value)
        {
            Renderer renderer = entry.renderer;
            if (renderer == null)
            {
                renderer = FindByName<Renderer>(entry.rendererName);
            }

            if (renderer != null && !string.IsNullOrEmpty(entry.materialPropertyName))
            {
                renderer.GetPropertyBlock(_materialPropertyBlock, materialIndex: 0);
                _materialPropertyBlock.SetColor(_materialPropertyIds[entry.materialPropertyName], value);
                renderer.SetPropertyBlock(_materialPropertyBlock, materialIndex: 0);
            }
        }

        private T FindByName<T>(string name) where T : Component
        {
            T result = default;
            if (!string.IsNullOrEmpty(name))
            {
                var components = GetComponentsInChildren<T>(includeInactive: true);
                int length = components.Length;
                for (int i = 0; i < length; i++)
                {
                    if (components[i].name == name)
                    {
                        result = components[i];
                        break;
                    }
                }
            }
            return result;
        }
        #endregion Private Functions
    }

    /// <summary>
    /// Describes colorable avatar part, and the property name that should color this part.
    /// </summary>
    [Serializable]
    public struct AvatarColorablePart
    {
        [Tooltip("The property name to watch for.")]
        public string property;

        [Tooltip("The renderer who's primary material color will be updated using the specified property value.")]
        public Renderer renderer;

        [Tooltip("The name of the renderer who's primary material color will be updated using the specified property value.")]
        public string rendererName;
        
        [Tooltip("The material property to change.")]
        public string materialPropertyName;
    }
}


