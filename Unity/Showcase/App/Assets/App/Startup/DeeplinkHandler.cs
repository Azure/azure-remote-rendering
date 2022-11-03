// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeeplinkHandler : MonoBehaviour
{
    LogHelper<DeeplinkHandler> _log = new LogHelper<DeeplinkHandler>();

    /// <summary>
    /// Get the get deeplink handler
    /// </summary>
    public static DeeplinkHandler Instance { get; private set; }

    /// <summary>
    /// Get the launch deeplink
    /// </summary>
    public string Deeplink { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            _log.LogVerbose($"Awake() (url: {Application.absoluteURL})");

            Instance = this;
            Application.deepLinkActivated += OnDeepLinkActivated;
            if (!string.IsNullOrEmpty(Application.absoluteURL))
            {
                // Cold start and Application.absoluteURL not null so process Deep Link.
                OnDeepLinkActivated(Application.absoluteURL);
            }
            else
            {
                Deeplink = string.Empty;
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDeepLinkActivated(string url)
    {
        _log.LogVerbose($"OnDeepLinkActivated() (url: {url})"); 

        // Update DeepLink Manager global variable, so URL can be accessed from anywhere.
        Deeplink = url;
        if (string.IsNullOrEmpty(url))
        {
            _log.LogError($"OnDeepLinkActivated() Empty url (url: {url})");
            return;
        }

        var pathAndParameters = url.Split('?');
        if (pathAndParameters == null || 
            pathAndParameters.Length < 1 || 
            pathAndParameters.Length > 2)
        {
            _log.LogError($"OnDeepLinkActivated() Invalid url (url: {url})");
        }

        string paramterString = string.Empty;
        if (pathAndParameters.Length > 1)
        {
            paramterString = pathAndParameters[1];
        }

        var pathParts = pathAndParameters[0].Split('/');
        if (pathParts == null || pathParts.Length < 2)
        {
            _log.LogError($"OnDeepLinkActivated() Invalid url path (url: {url})");
            return;
        }

        string operationString = string.Empty;
        for (int i = pathParts.Length - 1; i >= 0; i--)
        {
            if (!string.IsNullOrEmpty(pathParts[i]))
            {
                operationString = pathParts[i];
                break;
            }
        }

        DeeplinkOperation operation;
        if (!TryParseOperation(operationString, out operation))
        {
            _log.LogError($"OnDeepLinkActivated() Invalid operation (url: {url}) (op: {operationString}");
            return;
        }

        Dictionary<string, string> parameterValues = new Dictionary<string, string>();
        var keyValues = paramterString.Split('&');
        if (keyValues != null)
        {
            foreach (var keyValue in keyValues)
            {
                var split = keyValue.Split('=');
                if (split == null || split.Length != 2)
                {
                    _log.LogError($"OnDeepLinkActivated() Invalid key value pair (url: {url}) (key value = {keyValue})");
                    continue;
                }

                parameterValues.Add(Uri.UnescapeDataString(split[0]), Uri.UnescapeDataString(split[1]));
            }
        }

        ApplyDeeplinkParameters(operation, parameterValues);
    }

    private bool TryParseOperation(string value, out DeeplinkOperation operation)
    {
        operation = DeeplinkOperation.none;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        value.Replace("/", string.Empty);
        return Enum.TryParse(value, out operation);
    }

    private void ApplyDeeplinkParameters(DeeplinkOperation operation, Dictionary<string, string> parameterValues)
    {
        switch (operation)
        {
            case DeeplinkOperation.join:
                StartCoroutine(HandleJoinRoomOperation(parameterValues));
                break;

            default:
                _log.LogError($"Unhandled launch operation. (op: {operation})");
                break;
        }
    }

    private IEnumerator HandleJoinRoomOperation(Dictionary<string, string> parameterValues)
    {
        // delay a couple seconds to give app time to start up
        yield return new WaitForSeconds(2);

        string roomName;
        if (parameterValues.TryGetValue("room", out roomName) &&
            !string.IsNullOrEmpty(roomName))
        {
            _log.LogVerbose($"HandleJoinRoomOperation() Joining room ({roomName})");
            AppServices.SharingService.JoinRoom(roomName);
        }
        else
        {
            _log.LogError($"HandleJoinRoomOperation() Invalid join room operation");
        }
    }

    private enum DeeplinkOperation
    { 
        none,
        join
    }
}
