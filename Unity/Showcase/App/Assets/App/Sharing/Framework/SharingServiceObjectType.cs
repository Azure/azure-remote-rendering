// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// The type of the sharing object.
    /// </summary>
    /// <remarks>
    /// Do not change the integer values of the enum values, as Unity serializing the intergers and any changes to these
    /// values will break Unity projects.
    /// </remarks>
    [Serializable]
    public enum SharingServiceObjectType
    {
        /// <summary>
        /// The target's type hasn't been correctly set, and is unknown.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// A root target that is dynamic, and instances of it will be created at runtime. There maybe 0 or more instances of this target.
        /// </summary>
        Dynamic = 1,

        /// <summary>
        /// A root target that is static object that is always with the shared scene.
        /// </summary>
        Static = 2,

        /// <summary>
        /// A child target whose type is inheritted from its root.
        /// </summary>
        Child = 3,

        /// <summary>
        /// A root target that is dynamic location, and instances of it will be created at runtime. There maybe 0 or more instances of this target.
        /// </summary>
        Location = 4,
    }
}
