// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using System;
using UnityEngine;

public abstract class BaseRemoteLight : MonoBehaviour
{
    public abstract event Action<bool> LightReadyChanged;
    public abstract bool LightReady { get; set; }
    public abstract void CreateLight();
    public abstract void DestroyLight();
    public abstract void UpdateRemoteLightSettings();
    public abstract void RecreateLight();
    public abstract ObjectType RemoteLightType { get; }
    public abstract void SetIntensity(float intensity);
    public abstract void SetColor(Color color);
}