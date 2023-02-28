// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using System;
using System.Collections.Generic;
using UnityEngine;

public class RemoteSky : BaseRemoteSky
{
    public override Dictionary<string, LoadTextureFromSasOptions> AvailableCubemaps => builtInTextures;

    private bool canSetSky;
    public override bool CanSetSky
    {
        get => canSetSky;
        set
        {
            canSetSky = value;
            CanSetSkyChanged?.Invoke(canSetSky);
        }
    }

    private string currentSky = "DefaultSky";
    public override string CurrentSky
    {
        get => currentSky;
        protected set
        {
            currentSky = value;
            SkyChanged?.Invoke(value);
        }
    }

    private Dictionary<string, LoadTextureFromSasOptions> builtInTextures = new Dictionary<string, LoadTextureFromSasOptions>()
    {
        {"Autoshop",new LoadTextureFromSasOptions("builtin://Autoshop", TextureType.CubeMap)},
        {"BoilerRoom",new LoadTextureFromSasOptions("builtin://BoilerRoom", TextureType.CubeMap)},
        {"ColorfulStudio",new LoadTextureFromSasOptions("builtin://ColorfulStudio", TextureType.CubeMap)},
        {"Hangar",new LoadTextureFromSasOptions("builtin://Hangar", TextureType.CubeMap)},
        {"IndustrialPipeAndValve",new LoadTextureFromSasOptions("builtin://IndustrialPipeAndValve", TextureType.CubeMap)},
        {"Lebombo",new LoadTextureFromSasOptions("builtin://Lebombo", TextureType.CubeMap)},
        {"SataraNight",new LoadTextureFromSasOptions("builtin://SataraNight", TextureType.CubeMap)},
        {"SunnyVondelpark",new LoadTextureFromSasOptions("builtin://SunnyVondelpark", TextureType.CubeMap)},
        {"Syferfontein",new LoadTextureFromSasOptions("builtin://Syferfontein", TextureType.CubeMap)},
        {"TearsOfSteelBridge",new LoadTextureFromSasOptions("builtin://TearsOfSteelBridge", TextureType.CubeMap)},
        {"VeniceSunset",new LoadTextureFromSasOptions("builtin://VeniceSunset", TextureType.CubeMap)},
        {"WhippleCreekRegionalPark",new LoadTextureFromSasOptions("builtin://WhippleCreekRegionalPark", TextureType.CubeMap)},
        {"WinterRiver",new LoadTextureFromSasOptions("builtin://WinterRiver", TextureType.CubeMap)},
        {"DefaultSky",new LoadTextureFromSasOptions("builtin://DefaultSky", TextureType.CubeMap)}
    };

    public UnityBoolEvent OnCanSetSkyChanged;
    public override event Action<bool> CanSetSkyChanged;

    public UnityStringEvent OnSkyChanged;
    public override event Action<string> SkyChanged;

    public void Start()
    {
        // Hook up the event to the Unity event
        CanSetSkyChanged += (canSet) => OnCanSetSkyChanged?.Invoke(canSet);
        SkyChanged += (key) => OnSkyChanged?.Invoke(key);

        RemoteRenderingCoordinator.CoordinatorStateChange += ApplyStateToView;
        ApplyStateToView(RemoteRenderingCoordinator.instance.CurrentCoordinatorState);
    }

    private void OnDestroy()
    {
        RemoteRenderingCoordinator.CoordinatorStateChange -= ApplyStateToView;
    }

    private void ApplyStateToView(RemoteRenderingCoordinator.RemoteRenderingState state)
    {
        switch (state)
        {
            case RemoteRenderingCoordinator.RemoteRenderingState.RuntimeConnected:
                CanSetSky = true;
                break;
            default:
                CanSetSky = false;
                break;
        }
    }

    public override async void SetSky(string skyKey)
    {
        if (!CanSetSky)
        {
            Debug.Log("Unable to set sky right now");
            return;
        }

        if (AvailableCubemaps.ContainsKey(skyKey))
        {
            Debug.Log("Setting sky to " + skyKey);
            //Load the texture into the session
            var texture = await RemoteRenderingCoordinator.CurrentSession.Connection.LoadTextureFromSasAsync(AvailableCubemaps[skyKey]);

            //Apply the texture to the SkyReflectionSettings
            RemoteRenderingCoordinator.CurrentSession.Connection.SkyReflectionSettings.SkyReflectionTexture = texture;
            SkyChanged?.Invoke(skyKey);
        }
        else
        {
            Debug.Log("Invalid sky key");
        }
    }
}
