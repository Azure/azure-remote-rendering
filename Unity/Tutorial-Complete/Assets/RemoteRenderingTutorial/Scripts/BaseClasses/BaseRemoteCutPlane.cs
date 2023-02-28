// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

public abstract class BaseRemoteCutPlane : MonoBehaviour
{
    public abstract event Action<bool> CutPlaneReadyChanged;
    public abstract bool CutPlaneReady { get; set; }
    public abstract void CreateCutPlane();
    public abstract void DestroyCutPlane();
}

