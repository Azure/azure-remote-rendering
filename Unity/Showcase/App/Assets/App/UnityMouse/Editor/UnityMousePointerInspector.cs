// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.﻿

using Microsoft.MixedReality.Toolkit.Input;
using UnityEditor;

namespace Microsoft.MixedReality.Toolkit.Utilities.Editor
{
    [CustomEditor(typeof(UnityMousePointer))]
    public class UnityMousePointerInspector : BaseMousePointerInspector
    {
        private SerializedProperty systemCursorVisibilityChanges;
        private bool mousePointerWithAutoHideFoldout = true;

        protected override void OnEnable()
        {
            base.OnEnable();
            systemCursorVisibilityChanges = serializedObject.FindProperty("systemCursorVisibilityChanges");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            mousePointerWithAutoHideFoldout = EditorGUILayout.Foldout(mousePointerWithAutoHideFoldout, "Unity Mouse Pointer Hide Settings", true);

            if (mousePointerWithAutoHideFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(systemCursorVisibilityChanges);
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
