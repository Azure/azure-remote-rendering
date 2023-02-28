// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using System;
using UnityEngine;

public abstract class BaseRemoteRenderedModel : MonoBehaviour
{
    public abstract string ModelDisplayName { get; set; }
    public abstract string ModelPath { get; set; }
    public abstract ModelState CurrentModelState { get; protected set; }
    public abstract Entity ModelEntity { get; protected set; }

    public abstract event Action<float> LoadProgress;
    public abstract event Action<ModelState> ModelStateChange;

    public abstract void SetLoadingProgress(float progressValue);
    public abstract void LoadModel();
    public abstract void UnloadModel();
}