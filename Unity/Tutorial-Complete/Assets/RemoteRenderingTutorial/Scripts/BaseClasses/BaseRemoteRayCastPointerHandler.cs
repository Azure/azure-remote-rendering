// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using System;
using UnityEngine;

public abstract class BaseRemoteRayCastPointerHandler : MonoBehaviour
{
    public abstract event Action<Entity> RemoteEntityClicked;
}