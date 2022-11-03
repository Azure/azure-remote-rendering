// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

using Microsoft.MixedReality.Toolkit.Input.Editor;
using UnityEditor;

namespace Microsoft.MixedReality.Toolkit.Input
{
    /// <summary>
    /// A custom editor inspector for the RemoteSpherePointer behavior.
    /// </summary>
    [CustomEditor(typeof(RemoteSpherePointer))]
    public class RemoteSpherePointerInspector : SpherePointerInspector
    {
    }
}
