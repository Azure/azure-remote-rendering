// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DragValue))]
public class DragValueInspector : UnityEditor.Editor
{
    private static GUIStyle labelStyle;

    private void OnSceneGUI()
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.white;
        }

        DragValue dragValue = target as DragValue;
        if (dragValue != null)
        {
            Handles.color = Color.cyan;
            Vector3 startPos = dragValue.DragStartPosition;
            Vector3 endPos = dragValue.DragEndPosition;
            Handles.DrawLine(startPos, endPos);

            EditorGUI.BeginChangeCheck();

            float handleSize = HandleUtility.GetHandleSize(startPos) * 0.15f;
            dragValue.DragStartPosition = Handles.FreeMoveHandle(startPos,
                Quaternion.identity,
                handleSize,
                Vector3.zero,
                Handles.SphereHandleCap);
            dragValue.DragEndPosition = Handles.FreeMoveHandle(endPos,
                Quaternion.identity,
                handleSize,
                Vector3.zero,
                Handles.SphereHandleCap);

            if (EditorGUI.EndChangeCheck())
            {
                var dragStartSerialized = serializedObject.FindProperty("dragStartDistance");
                var dragEndSerialized = serializedObject.FindProperty("dragEndDistance");
                dragStartSerialized.floatValue = dragValue.DragStartDistance;
                dragEndSerialized.floatValue = dragValue.DragEndDistance;
                serializedObject.ApplyModifiedProperties();
            }

            var direction = dragValue.DragTrackDirection.normalized;
            var axis = direction;
            if (direction == Vector3.up || direction == -Vector3.up) axis = Vector3.right;
            else if (direction == Vector3.right || direction == -Vector3.right) axis = Vector3.up;
            else if (direction == Vector3.forward || direction == -Vector3.forward) axis = Vector3.up;
            
            DrawLabelWithDottedLine(startPos + (axis * handleSize * 10f), startPos, handleSize, "drag start");
            DrawLabelWithDottedLine(endPos + (axis * handleSize * 10f), endPos, handleSize, "drag end");
        }
    }

    private void DrawLabelWithDottedLine(Vector3 labelPos, Vector3 dottedLineStart, float handleSize, string labelText)
    {
        Handles.color = Color.white;
        Handles.Label(labelPos + Vector3.up * handleSize, labelText, labelStyle);
        Handles.DrawDottedLine(dottedLineStart, labelPos, 5f);
    }
}