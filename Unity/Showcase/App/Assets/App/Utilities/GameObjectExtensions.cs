// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

/// <summary>
/// Application GameObject Extensions
/// </summary>
public static class GameObjectExtensions 
{
    /// <summary>
    /// Is the game object a prefab game object, this only functions in editor
    /// </summary>
    public static bool IsPrefab(this GameObject gameObject)
    {
        if (gameObject == null)
        {
            return false;
        }

#if UNITY_EDITOR
        return 
            UnityEditor.Experimental.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) != null ||
            UnityEditor.EditorUtility.IsPersistent(gameObject);
#else
        return false;
#endif
    }
}
