// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseRemoteSky : MonoBehaviour
{
    public abstract event Action<bool> CanSetSkyChanged;
    public abstract event Action<string> SkyChanged;
    public abstract string CurrentSky { get; protected set; }

    public abstract Dictionary<string, LoadTextureFromSasOptions> AvailableCubemaps { get; }

    public abstract void SetSky(string skyKey);

    public abstract bool CanSetSky { get; set; }

}
