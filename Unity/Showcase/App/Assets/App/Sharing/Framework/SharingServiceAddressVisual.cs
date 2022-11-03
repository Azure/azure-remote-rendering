// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// A behavior for displaying a single sharing address
    /// </summary>
    public class SharingServiceAddressVisual : MonoBehaviour
    {
        #region Public Properties
        /// <summary>
        /// The Anchor used to place this transform
        /// </summary>
        public SharingServiceAddress Address { get; set; }

        /// <summary>
        /// Event raised when this address was selected by the user.
        /// </summary>
        public event Action<SharingServiceAddressVisual, SharingServiceAddress> Selected;
        #endregion Public Properties

        #region MonoBehavior Functions
        private void LateUpdate()
        {
            if (Address != null && Address.IsLocated)
            {
                transform.position = Address.Position;
                transform.rotation = Address.Rotation;
            }
        }
        #endregion MonoBehavior Functions

        #region Public Functions
        /// <summary>
        /// If this component is active and enabled, select it.
        /// </summary>
        public void Select()
        {
            if (Address != null && isActiveAndEnabled)
            {
                Selected?.Invoke(this, Address);
            }
        }
        #endregion Public Function
    }
}
