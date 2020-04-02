using Microsoft.Azure.RemoteRendering;
using Microsoft.Azure.RemoteRendering.Unity;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RemoteFrameStats : MonoBehaviour
{
    public TextMeshPro FrameStats = null;

    ARRServiceStats arrServiceStats = null;
    ARRServiceUnity arrServiceReference = null;

    string logMessage;
    bool error;

    public void Initialize(ARRServiceUnity arrService)
    {
        arrServiceReference = arrService;
    }

    public void SetLogMessage(string message, bool error)
    {
        logMessage = message;
        this.error = error;
    }

    private void OnEnable()
    {
        arrServiceStats = new ARRServiceStats();
    }

    void Update()
    {
        if (FrameStats != null)
        {
            FrameStats.color = this.error ? Color.red : Color.white;

            FrameStats.text = string.Empty;
            if (!string.IsNullOrEmpty(logMessage))
            {
                FrameStats.text += logMessage + "\n";
            }

            if (RemoteManagerUnity.IsConnected)
            {
                arrServiceStats.Update(RemoteManagerUnity.CurrentSession);

                FrameStats.text += arrServiceStats.GetStatsString();
            }
            else if (RemoteManagerUnity.CurrentSession != null)
            {
                FrameStats.text += $"Session id: '{RemoteManagerUnity.CurrentSession.SessionUUID}' \n";
                FrameStats.text += $"Session status: {arrServiceReference.LastProperties.Status}";

                if (arrServiceReference.LastProperties.Status == RenderingSessionStatus.Starting)
                {
                    FrameStats.text += new string('.', (int)(Time.time % 4.0f));
                    FrameStats.text += "\n(this may take a few minutes)";
                }

                FrameStats.text += $"\nConnection status: {RemoteManagerUnity.CurrentSession.ConnectionStatus}";
            }
        }
    }
}
