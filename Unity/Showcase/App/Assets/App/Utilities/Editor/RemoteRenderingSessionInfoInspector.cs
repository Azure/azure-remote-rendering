// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Editor;
using Microsoft.MixedReality.Toolkit.Extensions;
using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RemoteRenderingSessionInfo))]
public class RemoteRenderingSessionInfoInspector : Editor
{
    private GUIStyle boldText = null;
    private GUIContent sessionOverrideLabel = null;
    private GUIContent preferredDomainLabel = null;

    public override void OnInspectorGUI()
    {
        IRemoteRenderingService service = AppServices.RemoteRendering;
        BaseRemoteRenderingServiceProfile profile = service?.ConfigurationProfile as BaseRemoteRenderingServiceProfile;

        if (boldText == null)
        {
            boldText = new GUIStyle(GUI.skin.label);
            boldText.fontStyle = FontStyle.Bold;
        }

        GUILayout.Space(10.0f);
        GUILayout.Label("Current Session Info", boldText);
        EditorGUI.indentLevel++;
        if (service?.PrimaryMachine == null)
        {
            DrawNoMachine();
        }
        else
        {
            DrawMachingInformation(service.PrimaryMachine);
        }
        EditorGUI.indentLevel--;

        if (service != null)
        {
            GUILayout.Space(10.0f);
            GUILayout.Label("Session Controls", boldText);
            DrawSessionControls(service);

            if (Application.isPlaying)
            {
                GUILayout.Space(10.0f);
                GUILayout.Label("Connection Controls", boldText);
                DrawConnectionControls(profile, service);

                GUILayout.Space(10.0f);
                GUILayout.Label("Temporary Overrides", boldText);
                DrawPlayingSettings(service);
            }
        }
    }

    private void DrawNoMachine()
    {
        EditorGUILayout.LabelField("no active machine");
    }

    private void DrawMachingInformation(IRemoteRenderingMachine machine)
    {
        EditorGUILayout.LabelField("id", machine.Session.Id);
        EditorGUILayout.LabelField("elapsed time", machine.Session.ElapsedTime.ToString());
        EditorGUILayout.LabelField("max lease time", machine.Session.MaxLeaseTime.ToString());
        EditorGUILayout.LabelField("expiration", machine.Session.Expiration.ToLocalTime().ToString());
        EditorGUILayout.LabelField("size", machine.Session.Size.ToString());
        EditorGUILayout.LabelField("session status", machine.Session.Status.ToString());
        EditorGUILayout.LabelField("connection status", machine.Session.Connection.ConnectionStatus.ToString());
        EditorGUILayout.LabelField("connection error", machine.Session.Connection.ConnectionError.ToString());
        EditorGUILayout.LabelField("message", machine.Session.Message);
    }

    private void DrawSessionControls(IRemoteRenderingService service)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        GUILayout.BeginVertical();
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Start Session"))
        {
            StartSession(service);
        }

        if (GUILayout.Button("Extend Session"))
        {
            ExtendSession(service);
        }

        if (GUILayout.Button("Stop Session"))
        {
            StopSession(service);
        }

        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Refresh Status"))
        {
            RefreshSessionStatus(service);
        }

        if (GUILayout.Button("Inspector") && service.PrimaryMachine?.Session.Status == Microsoft.Azure.RemoteRendering.RenderingSessionStatus.Ready)
        {
            ConnectToInspector(service);
        }

        if (GUILayout.Button("Forget Session"))
        {
            ForgetSession(service);
        }

        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private void DrawPlayingSettings(IRemoteRenderingService service)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        BaseRemoteRenderingServiceProfile loadedProfile = service.LoadedProfile;

        if (sessionOverrideLabel == null)
        {
            sessionOverrideLabel = new GUIContent(
                "Session Id",
                "A session guid to connect to. If specified, the app will attempt to connect to this session. If a override guid is suppied, the corresponding domain must also be set.");
        }

        if (preferredDomainLabel == null)
        {
            preferredDomainLabel = new GUIContent(
                "Preferred Domain",
                "The preferred to domain to connect to; for example, westus2.mixedreality.azure.com.");
        }

        loadedProfile.PreferredDomain =
            EditorGUILayout.TextField(preferredDomainLabel, loadedProfile.PreferredDomain);

        loadedProfile.SessionOverride = 
            EditorGUILayout.TextField(sessionOverrideLabel, loadedProfile.SessionOverride);
    }

    private void DrawConnectionControls(BaseRemoteRenderingServiceProfile profile, IRemoteRenderingService service)
    {
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Connect"))
        {
            Connect(service);
        }

        if (GUILayout.Button("Disconnect"))
        {
            Disconnect(service);
        }

        GUILayout.EndHorizontal();
    }

    /// <summary>
    /// Re-apply settings to ensure the buttons act on the lastest settings.
    /// </summary>
    private static async Task ApplySettings(IRemoteRenderingService service)
    {
        // Re-apply settings that have been entered during playmode.
        string lastSessionOverride = null;
        string lastPreferredDomain = null;
        if (Application.isPlaying)
        {
            lastSessionOverride = service.LoadedProfile.SessionOverride;
            lastPreferredDomain = service.LoadedProfile.PreferredDomain;
        }

        try
        {
            await service.ReloadProfile();
        }
        catch (Exception ex)
        {
            Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}",  $"Failed to reload profile settings. Reason: {ex.Message}");
        }

        if (Application.isPlaying)
        {
            if (!string.IsNullOrEmpty(lastSessionOverride))
            {
                service.LoadedProfile.SessionOverride = lastSessionOverride;
            }

            if (!string.IsNullOrEmpty(lastPreferredDomain))
            {
                service.LoadedProfile.PreferredDomain = lastPreferredDomain;
            }
        }
    }

    private static async void RefreshSessionStatus(IRemoteRenderingService service)
    {
        if (service.PrimaryMachine != null)
        {
            await service.PrimaryMachine.Session.UpdateProperties();
        }
    }

    private static async void StartSession(IRemoteRenderingService service)
    {
        await ApplySettings(service);
        await service.StopAll();
        var machine = await service.Create();
        if (machine != null)
        {
            await machine.Session.Connection.Connect();
        }
    }

    private static async void ExtendSession(IRemoteRenderingService service)
    {
        if (service.PrimaryMachine != null)
        {
            await service.PrimaryMachine.Session.Renew(TimeSpan.FromMinutes(30));
        }
    }

    private static async void StopSession(IRemoteRenderingService service)
    {
        await service.StopAll();
    }

    private static async void ForgetSession(IRemoteRenderingService service)
    {
        await service.ClearAll();
    }

    private static async void ConnectToInspector(IRemoteRenderingService service)
    {
        if (service.PrimaryMachine != null)
        {
            await service.PrimaryMachine.Session.OpenWebInspector();
        }
    }

    private static async void Connect(IRemoteRenderingService service)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        await ApplySettings(service);

        IRemoteRenderingMachine machine = null;
        try
        {
            machine = await GetOrCreateMachine(service);
        }
        catch (Exception ex)
        {
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  $"Failed to obtain machine for connection. Reason: {ex.Message}");
        }

        if (machine != null)
        {
            try
            {
                await machine.Session.Connection.Connect();
            }
            catch (Exception ex)
            {
                Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  $"Failed to connect to machine. Reason: {ex.Message}");
            }
        }
    }

    private static async void Disconnect(IRemoteRenderingService service)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        IRemoteRenderingMachine machine = service.PrimaryMachine;

        if (machine != null)
        {
            try
            {
                await machine.Session.Connection.Disconnect();
            }
            catch (Exception ex)
            {
                Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  $"Failed to disconnect from machine. Reason: {ex.Message}");

            }
        }
    }

    private static async Task<IRemoteRenderingMachine> GetOrCreateMachine(IRemoteRenderingService service)
    {
        IRemoteRenderingMachine machine = service.PrimaryMachine;
        BaseRemoteRenderingServiceProfile loadedProfile = service.LoadedProfile;

        if (loadedProfile != null &&
            !string.IsNullOrEmpty(loadedProfile.SessionOverride))
        {
            if (machine == null || machine.Session.Id != loadedProfile.SessionOverride)
            {
                machine = await service.Open(loadedProfile.SessionOverride);
            }
        }
        else if (machine == null)
        {
            machine = await service.Create();
        }
        else if (loadedProfile.PreferredDomain != machine.Session.Domain)
        {
            machine = await service.Create();
        }

        return machine;

    }
}
