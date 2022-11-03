// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// Open the main menu's user panel when an interactable is clicked.
    /// </summary>
    public class AvatarMoreInformation : AvatarComponent
    {
        #region Serialized Fields
        [SerializeField]
        [Tooltip("The interactable that when clicked will open the user's panel.")]
        private Interactable interactable;

        /// <summary>
        /// The interactable that when clicked will open the user's panel.
        /// </summary>
        public Interactable Interactable
        {
            get => interactable;
            set => interactable = value;
        }
        #endregion Serialized Fields

        #region MonoBehavior Functions
        private void Start()
        {
            if (interactable)
            {
                interactable = gameObject.EnsureComponent<Interactable>();
            }

            if (interactable != null)
            {
                interactable.OnClick.AddListener(OpenUserPanel);
            }
        }
        #endregion MonoBehavior Functions

        #region Public Functions
        public void OpenUserPanel()
        {
            if (string.IsNullOrEmpty(PlayerData.PlayerId))
            {
                return;
            }

            AppServices.AppSettingsService.GetMainMenu<HandMenuHooks>()?.OpenPlayerPanel(PlayerData.PlayerId);
        }
        #endregion Public Functions
    }
}
