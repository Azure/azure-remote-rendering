// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

[CreateAssetMenu(fileName = "RemoteLighting", menuName = "Remoting/Lighting", order = 1)]
[Serializable]
public class RemoteLightingData : ScriptableObject
{
    [Tooltip("A name used to display in some UI field.")]
    public string ObjectName;

    [Tooltip("A remote url pointing to an ARR texture that will be loaded by the ARR session.")]
    public string Url;

    [Tooltip("A local texture used to preview the sky-map.")]
    public Texture2D Texture;
}


