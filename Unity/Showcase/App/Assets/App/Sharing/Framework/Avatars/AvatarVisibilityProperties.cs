// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// A behavior for setting the visibility using boolean property values.
    /// </summary>
    [RequireComponent(typeof(AvatarComponentCollection))]
    public class AvatarVisibilityProperties : AvatarComponent
    {
        private HashSet<string> _parts = new HashSet<string>();

        #region Serialized Fields
        [SerializeField]
        [Tooltip("The game objects to show or hide based on a participant's bool property value.")]
        private AvatarShowOrHidePart[] showOrHideParts = null;

        /// <summary>
        /// The game objects to show or hide based on a participant's bool property value.
        /// </summary>
        public AvatarShowOrHidePart[] ShowOrHideParts
        {
            get => showOrHideParts;

            set
            {
                showOrHideParts = value;
                InitializeParts();
            }
        }
        #endregion Serialized Fields

        #region MonoBehavior Functions
        #endregion MonoBehavior Functions

        #region Protected Functions
        /// <summary>
        /// Implement to handle component being intialized
        /// </summary>
        protected override void OnInitialized()
        {
            InitializeParts();
        }

        /// <summary>
        /// Implement to handle property changes for the current participant.
        /// </summary>
        protected override void OnPropertyChanged(string name, object value) 
        {
            TryUpdatePart(name);
        }
        #endregion Protected Functions

        #region Private Functions
        private void InitializeParts()
        {
            _parts.Clear();
            int length = showOrHideParts?.Length ?? 0;
            for (int i = 0; i < length; i++)
            {
                var entry = showOrHideParts[i];

                if (!string.IsNullOrEmpty(entry.property))
                {
                    _parts.Add(entry.property);
                    if (TryGetProperty(entry.property, out bool value))
                    {
                        UpdateTextField(ref entry, value);
                    }
                    else if (entry.autoHide)
                    {
                        UpdateTextField(ref entry, value);
                    }
                }
            }
        }

        private void TryUpdatePart(string propertyName)
        {
            if (_parts.Contains(propertyName) &&
                TryGetProperty(propertyName, out bool value))
            {
                int length = showOrHideParts.Length;
                for (int i = 0; i < length; i++)
                {
                    var entry = showOrHideParts[i];
                    if (entry.property == propertyName)
                    {
                        UpdateTextField(ref entry, value);
                    }
                }
            }
        }

        public void UpdateTextField(ref AvatarShowOrHidePart entry, bool value)
        {
            if (entry.gameObject != null)
            {
                entry.gameObject.SetActive(value);
            }
        }
        #endregion Private Functions
    }

    /// <summary>
    /// Hold a game object that will be shown or hidden based on a participant's bool property value.
    /// </summary>
    [Serializable]
    public struct AvatarShowOrHidePart
    {
        [Tooltip("The boolend property name to watch for.")]
        public string property;

        [Tooltip("The game object to show if property is true, and hide if property is false.")]
        public GameObject gameObject;

        [Tooltip("Should the game object be hidden if property is missing.")]
        public bool autoHide;
    }
}

