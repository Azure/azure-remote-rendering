// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEditor;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// A custom inspector for rendering a button to a sharing room.
    /// </summary>
    [CustomEditor(typeof(SharingServiceJoinRoomHelper)), CanEditMultipleObjects]
    public class SharingServiceJoinRoomHelperInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (Application.isPlaying)
            {
                base.OnInspectorGUI();

                GUILayout.Space(10.0f);
                if (GUILayout.Button("Join Room"))
                {
                    SharingServiceJoinRoomHelper helper = (SharingServiceJoinRoomHelper)target;
                    helper.Join();
                }
            }
        }
    }
}
