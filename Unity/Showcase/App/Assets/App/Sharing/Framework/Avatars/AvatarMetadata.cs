// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// A behavior for displaying simple text based metadata.
    /// </summary>
    [RequireComponent(typeof(AvatarComponentCollection))]
    public class AvatarMetadata : AvatarComponent
    {
        private HashSet<string> _textFields = new HashSet<string>();

        #region Serialized Fields
        [SerializeField]
        [Tooltip("The text fields that will be updated with string property changes.")]
        private SharingServicePlayerMetadataTextField[] textFields = null;

        /// <summary>
        /// The text fields that will be updated with string property changes.
        /// </summary>
        public SharingServicePlayerMetadataTextField[] TextFields
        {
            get => textFields;

            set
            {
                textFields = value;
                InitializeTextFields();
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
            InitializeTextFields();
        }

        /// <summary>
        /// Implement to handle property changes for the current participant.
        /// </summary>
        protected override void OnPropertyChanged(string name, object value) 
        {
            TryUpdatePropertyField(name);
        }

        /// <summary>
        /// Implement to handle when display name changes.
        /// </summary>
        protected override void OnDisplayNameChanged(string name) 
        {
            InitializeTextFields();
        }
        #endregion Protected Functions

        #region Private Functions
        private void InitializeTextFields()
        {
            _textFields.Clear();
            int length = textFields?.Length ?? 0;
            for (int i = 0; i < length; i++)
            {
                var entry = textFields[i];

                if (entry.type == SharingServicePlayerMetadataTextFieldType.DisplayName)
                {
                    UpdateTextField(ref entry, PlayerData.DisplayName);
                }
                else if (!string.IsNullOrEmpty(entry.property))
                {
                    _textFields.Add(entry.property);
                    if (TryGetProperty(entry.property, out string value))
                    {
                        UpdateTextField(ref entry, value);
                    }
                    else
                    {
                        UpdateTextField(ref entry, string.Empty);
                    }
                }
            }
        }

        private void TryUpdatePropertyField(string propertyName)
        {
            if (_textFields.Contains(propertyName) &&
                TryGetProperty(propertyName, out string value))
            {
                int length = textFields.Length;
                for (int i = 0; i < length; i++)
                {
                    var entry = textFields[i];
                    if (entry.property == propertyName)
                    {
                        UpdateTextField(ref entry, value);
                    }
                }
            }
        }

        public void UpdateTextField(ref SharingServicePlayerMetadataTextField entry, string value)
        {
            if (entry.text != null)
            {
                if (string.IsNullOrEmpty(value))
                {
                    value = entry.fallback;
                }

                entry.text.text = value;

                if (entry.autoHide)
                {
                    entry.text.gameObject.SetActive(!string.IsNullOrEmpty(value));
                }
            }
        }
        #endregion Private Functions
    }

    /// <summary>
    /// For simple metadata fields, render the property values to this.
    /// </summary>
    [Serializable]
    public struct SharingServicePlayerMetadataTextField
    {
        [Tooltip("The property name to watch for.")]
        public string property;

        [Tooltip("Defines where the field value will come from.")]
        public SharingServicePlayerMetadataTextFieldType type;

        [Tooltip("The text fields to update with the property value.")]
        public TextMeshPro text;

        [Tooltip("Should the text object be hidden if property is missing, empty, or null.")]
        public bool autoHide;

        [Tooltip("The fallback value to use if property is missing, empty, or null.")]
        public string fallback;
    }

    /// <summary>
    /// Defines where the field value will come from.
    /// </summary>
    public enum SharingServicePlayerMetadataTextFieldType
    {
        [Tooltip("The value will come from the defined property name.")]
        Property,

        [Tooltip("The value will come from player's display name.")]
        DisplayName
    }
}

