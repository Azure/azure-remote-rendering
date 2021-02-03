// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.Azure.RemoteRendering.Unity;
using TMPro;
using UnityEngine;

public class RemoteFrameStats : MonoBehaviour
{
    public TextMeshPro FrameStats = null;

    ServiceStatistics arrServiceStats = null;

#if UNITY_EDITOR
    private void OnEnable()
    {
        arrServiceStats = new ServiceStatistics();
    }

    void Update()
    {
        if (!RemoteManagerUnity.IsConnected)
        {
            FrameStats.text = "FrameStats is waiting for connection...";
            return;
        }

        arrServiceStats.Update(RemoteManagerUnity.CurrentSession);

        if (FrameStats != null)
        {
            FrameStats.text = arrServiceStats.GetStatsString();
        }
    }
#else
    private void OnEnable()
    {
        this.gameObject.SetActive(false);
    }
#endif
}
