// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// A SharingObject component that represents a root parent, and is capable of sharing state for
    /// this game object and its children.
    /// </summary>
    public class SharingObject : SharingObjectBase
    {
        #region Public Properties
        /// <summary>
        /// Get if this is a root
        /// </summary>
        public override sealed bool IsRoot => true;
        #endregion Public Properties

        #region MonoBehaviour Functions
        /// <summary>
        /// Ensure the in-scene components are added to the object
        /// </summary>
        private void OnValidate()
        {
            AppServices.SharingService?.EnsureNetworkObjectComponents(gameObject);
        }

        /// <summary>
        /// Add in other sharing components that are needed
        /// </summary>
        private void Awake()
        {
            AppServices.SharingService?.EnsureNetworkObjectComponents(gameObject);
        }
        #endregion MonoBehavior Functions
    }
}

