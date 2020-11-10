// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities.Editor;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Input.Editor
{
    [CustomEditor(typeof(PointerModeHandler))]
    public class PointerModeHandlerInspector : UnityEditor.Editor
    {
        private static readonly GUIContent RemoveButtonContent = new GUIContent("-", "Remove mode");
        private static readonly GUIContent AddButtonContent = new GUIContent("+", "Add mode");
        private static readonly GUILayoutOption MiniButtonWidth = GUILayout.Width(20.0f);

        private PointerMode[] allPointerModes;

        private SerializedProperty modesProperty;

        #region MonoBehavior Methods
        private void OnEnable()
        {
            modesProperty = serializedObject.FindProperty("pointerModes");
            InitializeAllModes();
        }
        #endregion MonoBehavior Methods

        #region BaseInputHandlerInspector Methods
        public override void OnInspectorGUI()
        {
            bool validModes = allPointerModes != null && allPointerModes.Length != 0;

            // If we should be enabled but there are no valid keywords, alert developer
            if (!validModes)
            {
                InitializeAllModes();
                EditorGUILayout.HelpBox("No modes registered. Some properties may not be editable.", MessageType.Error);
            }

            serializedObject.Update();

            bool wasGUIEnabled = GUI.enabled;
            GUI.enabled = validModes;
            ShowList(modesProperty);
            GUI.enabled = wasGUIEnabled;

            serializedObject.ApplyModifiedProperties();

            // error and warning messages
            if (modesProperty.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No modes have been assigned!", MessageType.Warning);
            }
            else
            {
                var handler = (PointerModeHandler)target;
                var duplicateModes = handler.PointerModes
                    .GroupBy(mode => mode.Mode)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key);

                if (duplicateModes != null && duplicateModes.Count() > 0)
                {
                    EditorGUILayout.HelpBox($"Pointer mode \'{duplicateModes.First()}\' is assigned more than once!", MessageType.Warning);
                }
            }
        }
        #endregion BaseInputHandlerInspector Methods

        #region Private Methods
        private void InitializeAllModes()
        {
            allPointerModes = new PointerMode[(int)PointerMode.Count];
            for (int i = 0; i < (int)PointerMode.Count; i++)
            {
                allPointerModes[i] = (PointerMode)i;
            }
        }

        private void ShowList(SerializedProperty list)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                // remove the keywords already assigned from the registered list
                var handler = (PointerModeHandler)target;
                var availableModes = new PointerMode[0];

                if (handler.PointerModes != null && allPointerModes != null)
                {
                    availableModes = allPointerModes.Except(handler.PointerModes
                        .Select(PointerModeAndResponse => PointerModeAndResponse.Mode))
                        .OrderBy(pointerMode => pointerMode.ToString())
                        .ToArray();
                }

                // keyword rows
                int listSize = list == null ? 0 : list.arraySize;
                for (int index = 0; index < listSize; index++)
                {
                    // the element
                    SerializedProperty pointerModeAndResponseProperty = list.GetArrayElementAtIndex(index);
                    SerializedProperty modeProperty = pointerModeAndResponseProperty.FindPropertyRelative("mode");
                    PointerMode modePropertyValue = (PointerMode)modeProperty.enumValueIndex;

                    // draw element expander
                    GUILayout.BeginHorizontal();

                    bool elementExpanded = EditorGUILayout.PropertyField(
                        pointerModeAndResponseProperty,
                        new GUIContent(modeProperty.enumDisplayNames[(int)modePropertyValue]));

                    GUILayout.FlexibleSpace();

                    // the remove element button
                    bool elementRemoved = GUILayout.Button(RemoveButtonContent, EditorStyles.miniButton, MiniButtonWidth);

                    GUILayout.EndHorizontal();

                    if (elementRemoved)
                    {
                        list.DeleteArrayElementAtIndex(index);

                        if (index == list.arraySize)
                        {
                            EditorGUI.indentLevel--;
                            return;
                        }
                    }

                    bool invalidMode = true;
                    if (allPointerModes != null)
                    {
                        foreach (PointerMode mode in allPointerModes)
                        {
                            if (mode == modePropertyValue)
                            {
                                invalidMode = false;
                                break;
                            }
                        }
                    }

                    if (invalidMode)
                    {
                        EditorGUILayout.HelpBox("Registered mode is not recognized!", MessageType.Error);
                    }

                    if (!elementRemoved && elementExpanded)
                    {
                        PointerMode[] pointerModes = availableModes
                            .Concat(new[] { modePropertyValue })
                            .OrderBy(pointerMode => modeProperty.enumDisplayNames[(int)pointerMode])
                            .ToArray();

                        string[] pointerModeStrings = pointerModes
                            .Select(pointerMode => modeProperty.enumDisplayNames[(int)pointerMode])
                            .ToArray();

                        int previousSelection = ArrayUtility.IndexOf(pointerModes, modePropertyValue);
                        int currentSelection = EditorGUILayout.Popup("Mode", previousSelection, pointerModeStrings);

                        if (currentSelection != previousSelection)
                        {
                            modeProperty.enumValueIndex = (int)pointerModes[currentSelection];
                        }

                        SerializedProperty enabledProperty = pointerModeAndResponseProperty.FindPropertyRelative("enabled");
                        EditorGUILayout.PropertyField(enabledProperty, true);

                        SerializedProperty disabledProperty = pointerModeAndResponseProperty.FindPropertyRelative("disabled");
                        EditorGUILayout.PropertyField(disabledProperty, true);

                        SerializedProperty clickedProperty = pointerModeAndResponseProperty.FindPropertyRelative("clicked");
                        EditorGUILayout.PropertyField(clickedProperty, true);
                    }
                }

                // add button row
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    // the add element button
                    if (GUILayout.Button(AddButtonContent, EditorStyles.miniButton, MiniButtonWidth))
                    {
                        var index = list.arraySize;
                        list.InsertArrayElementAtIndex(index);
                        var elementProperty = list.GetArrayElementAtIndex(index);
                        SerializedProperty modeProperty = elementProperty.FindPropertyRelative("mode");
                        modeProperty.enumValueIndex = (int)PointerMode.Invalid;
                    }
                }
            }
        }
        #endregion Private Methods
    }
}