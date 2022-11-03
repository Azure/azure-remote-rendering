// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// A helper that initializes avatar metadata components.
    /// </summary>
    public class AvatarComponentCollection : MonoBehaviour
    {
        #region Public Properties
        /// <summary>
        /// Get if this is initialized.
        /// </summary>
        public bool Initialized { get; private set; } = false;

        /// <summary>
        /// Get the player data for this collection.
        /// </summary>
        public SharingServicePlayerData PlayerData { get; private set; }
        #endregion Public Properties

        #region Public Functions
        public void OnEnable()
        {
            if (!Initialized && transform.parent != null)
            {
                var parent = transform.parent.GetComponentInParent<AvatarComponentCollection>();
                if (parent != null && parent.Initialized)
                {
                    Initialize(parent.PlayerData);
                }    
            }
        }
        #endregion Public Functions

        #region Public Functions
        /// <summary>
        /// Initialize this metadata component.
        /// </summary>
        public void Initialize(SharingServicePlayerData playerData)
        {
            Initialized = true;
            PlayerData = playerData;

            var metadata = GetComponents<AvatarComponent>();
            if (metadata != null)
            {
                int length = metadata.Length;
                for (int i = 0; i < length; i++)
                {
                    metadata[i].Initialize(playerData);
                }
            }
        }
        #endregion Public Functions
    }
}
