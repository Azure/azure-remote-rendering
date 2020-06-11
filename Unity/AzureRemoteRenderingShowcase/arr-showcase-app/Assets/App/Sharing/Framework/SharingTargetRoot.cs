// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// A SharingTarget component that represents a root parent, and is capable of sharing state for
    /// this game object and its children.
    /// </summary>
    public class SharingTargetRoot : SharingTarget
    {
        #region Public Properties
        /// <summary>
        /// Get if this is a root
        /// </summary>
        public override sealed bool IsRoot => true;
        #endregion Public Properties

        #region Protected Functions
        /// <summary>
        /// Create an address used to find a child target that is underneath a root target. If null or empty is returned,
        /// it is assumed that is a root target.
        /// </summary>
        protected override sealed int[] CreateAddress()
        {
            return null;
        }
        #endregion Protected Functions
    }
}

