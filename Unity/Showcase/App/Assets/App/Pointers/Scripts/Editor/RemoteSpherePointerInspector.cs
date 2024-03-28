// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEditor;

using Microsoft.MixedReality.Toolkit.Input;

namespace Microsoft.Showcase.App.Pointers.Editor
{
    /// <summary>
    /// Provide the same inspector for <see cref="RemoteSpherePointer"/> as for it's
    /// <see cref="SpherePointer"/> base class.
    /// </summary>
    [CustomEditor(typeof(RemoteSpherePointer))]
    public class RemoteSpherePointerInspector : SpherePointerInspector
    {
    }
}
