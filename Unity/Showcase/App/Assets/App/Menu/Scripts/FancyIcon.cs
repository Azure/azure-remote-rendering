// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// This will holds a set of default fancy icons, and will load one of the icons based on a selection setting.
/// </summary>
public class FancyIcon : MonoBehaviour
{
    private LogHelper<FancyIcon> _logger = new LogHelper<FancyIcon>();

    private static Dictionary<FancyIconType, FancyIconData> _options = new Dictionary<FancyIconType, FancyIconData>
    {
        { FancyIconType.Manipulate, new FancyIconData("Models/Icon_MoveAll")},
        { FancyIconType.ManipulatePiece, new FancyIconData("Models/Icon_MovePiece")},
        { FancyIconType.Explode, new FancyIconData("Models/Icon_Explode")},
        { FancyIconType.Delete, new FancyIconData("Models/Icon_Erase")},
        { FancyIconType.Reset, new FancyIconData("Models/Icon_Revert")},
        { FancyIconType.Clip, new FancyIconData("Models/Icon_Slice")},
        { FancyIconType.Color, new FancyIconData("Models/Icon_Color")},
        { FancyIconType.Plus, new FancyIconData("Models/Icon_AddModel")},
        { FancyIconType.BackArrow, new FancyIconData("Models/Icon_Back")},
        { FancyIconType.NextArrow, new FancyIconData("Models/Icon_Back") { rotation = new Vector3(0, 0, 180) } },
        { FancyIconType.Disconnected, new FancyIconData("Models/Icon_EndSession")},
        { FancyIconType.Connected, new FancyIconData("Models/Icon_StartSession")},
        { FancyIconType.People, new FancyIconData("Models/Icon_Group") { rotation = new Vector3(90, 0, 0), scale = new Vector3(0.00013f, 0.00013f, 0.00013f) } },
        { FancyIconType.PeopleAdd, new FancyIconData("Models/Icon_Group_Add") { rotation = new Vector3(90, 0, 0), scale = new Vector3(0.00013f, 0.00013f, 0.00013f) } },
        { FancyIconType.Light, new FancyIconData("Models/Icon_Lighting")},
        { FancyIconType.ResetCamera, new FancyIconData("Models/Icon_ResetCamera") { rotation = new Vector3(90, 0, 0), scale = new Vector3(0.3f, 0.3f, 0.3f) } },
        { FancyIconType.Exit, new FancyIconData("Models/Icon_Quit") { rotation = new Vector3(90, 0, 0), scale = new Vector3(2.25f, 2.25f, 2.25f) } },
        { FancyIconType.Globe, new FancyIconData("Models/Icon_Session")},
        { FancyIconType.Share, new FancyIconData("Models/Icon_Share") { scale = new Vector3(0.3f, 0.3f, 0.3f) } },
        { FancyIconType.Stats, new FancyIconData("Models/Icon_Stats")},
        { FancyIconType.Tool, new FancyIconData("Models/Icon_Tool")},
        { FancyIconType.Upload, new FancyIconData("Models/Icon_Upload") { position = new Vector3(0.0122f, 0.0122f, 0), rotation = new Vector3(90, 0, 0), scale = new Vector3(0.3f, 0.3f, 0.3f) } },
        { FancyIconType.Close, new FancyIconData("Models/Icon_AddModel") { rotation = new Vector3(0, 0, 45) } },
    };

    #region Serialized Fields
    [SerializeField]
    [HideInInspector]
    [Tooltip("Field used to track what icon was preloaded.")]
    private string loadedPath = null;

    [SerializeField]
    [HideInInspector]
    [Tooltip("Field used to track what mesh filter was preloaded.")]
    private MeshFilter loadedFilter = null;

    [Header("Settings")]

    [SerializeField]
    [Tooltip("The icon type to load into the target container.")]
    private FancyIconType selected = FancyIconType.Unknown;

    /// <summary>
    /// The icon type to load into the target container.
    /// </summary>
    public FancyIconType Selected
    {
        get => selected;
        set
        {
            if (selected != value)
            {
                selected = value;
                LoadIcon();
            }
        }
    }

    [SerializeField]
    [Tooltip("The target mesh filter to set with the fancy icon.")]
    private MeshFilter meshFilter = null;

    /// <summary>
    /// The target mesh filter to set with the fancy icon.
    /// </summary>
    public MeshFilter MeshFilter
    {
        get => meshFilter;
        set
        {
            if (meshFilter != value)
            {
                meshFilter = value;
                LoadIcon();
            }
        }
    }

    [SerializeField]
    [Tooltip("Should the icon be updated while in editting mode.")]
    private bool editorUpdate = false;

    /// <summary>
    /// Should the icon be updated while in editting mode.
    /// </summary>
    public bool EditorUpdate
    {
        get => editorUpdate;
        set => editorUpdate = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Functions
    private void Awake()
    {
        LoadIcon();
    }
    #endregion MonoBehavior Functions

    #region Private Functions
    private bool IconLoadNeeded()
    {
        bool loadNeeded = false;

        if (_options.ContainsKey(selected))
        {
            var option = _options[selected];
            loadNeeded = option.iconPath != loadedPath || meshFilter != loadedFilter;
        }
        else
        {
            loadNeeded = loadedPath != null || loadedFilter != null;
        }

        return loadNeeded;
    }

    private void LoadIcon()
    {
        if (_options.ContainsKey(selected))
        {
            var option = _options[selected];
            LoadIcon(meshFilter, option.iconPath, option.position, Quaternion.Euler(option.rotation), option.scale);
        }
        else
        {
            ClearIcon(meshFilter);
        }
    }   
    
    private void LoadIcon(MeshFilter meshFilter, string path, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (meshFilter == null)
        {
            return;
        }

        if (loadedPath == path && loadedFilter == meshFilter)
        {
            return;
        }

        GameObject prefabObject = null;

        try
        {
            prefabObject = Resources.Load<GameObject>(path);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to load icon recourse '{0}'. Exception {1}", path, ex);
        }

        var prefabMeshFilter = prefabObject.GetComponentInChildren<MeshFilter>();
        if (prefabMeshFilter != null)
        {
            loadedPath = path;
            loadedFilter = meshFilter;

            meshFilter.transform.localPosition = position;
            meshFilter.transform.localRotation = rotation;
            meshFilter.transform.localScale = scale;
            meshFilter.sharedMesh = prefabMeshFilter.sharedMesh;
        }
    }

    private void ClearIcon(MeshFilter meshFilter)
    {
        loadedPath = null;
        loadedFilter = null;

        if (meshFilter != null)
        {
            meshFilter.sharedMesh = null;
        }
    }
    #endregion Private Functions

#if UNITY_EDITOR
    [CustomEditor(typeof(FancyIcon))]
    private class FancyIconEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            FancyIcon fancyIcon = (FancyIcon)target;

            if (fancyIcon.EditorUpdate && fancyIcon.IconLoadNeeded())
            {
                fancyIcon.LoadIcon();
            }
        }
    }
#endif
}

[Serializable]
public enum FancyIconType
{
    // DO NOT CHANGE THE VALUES OF THESE ENUM VALUES

    Unknown = 0,
    Manipulate = 1,
    ManipulatePiece = 2,
    Explode = 3,
    Delete = 4,
    Reset = 5,
    Clip = 6,
    Color = 7,
    Plus = 8, 
    BackArrow = 9,
    Disconnected = 10,
    Connected = 11,
    People = 12,
    PeopleAdd = 13,
    Light = 14,
    Exit = 15,
    ResetCamera = 16,
    Globe = 17,
    Share = 18,
    Stats = 19,
    Tool = 20,
    Upload = 21,
    Close = 22,
    NextArrow = 23,

    //
    // Add new values above "Count" and "Invalid"
    //

    /// <summary>
    /// Used for iterating over all action type. Keep at end, before 'Invalid'
    /// </summary>
    Count,

    /// <summary>
    /// And invalid pointer mode
    /// </summary>
    Invalid,
}

[Serializable]
public struct FancyIconData
{
    public FancyIconData(string path)
    {
        iconPath = path;
        position = Vector3.zero;
        rotation = Vector3.zero;
        scale = Vector3.one; 
    }

    [Tooltip("The path to the icon prefab")]
    public string iconPath;

    [Tooltip("The position to apply to the icon")]
    public Vector3 position;

    [Tooltip("The rotation to apply to the icon")]
    public Vector3 rotation;

    [Tooltip("The scale to apply to the icon")]
    public Vector3 scale;
}


