// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A helper to raising events when there is no network connection
/// </summary>
public class NetworkWatcher : MonoBehaviour
{
    private bool _isConnected = false;
    private Coroutine _connectionTest = null;

    #region Serialized Fields
    [Header("Settings")]

    [SerializeField]
    [Tooltip("When app was connected to the internet, this is the rate (updates / second) at which the internet connection is checked. Defaults to one update every 30 seconds.")]
    private float connectedUpdateRate = 1.0f / 30.0f;

    /// <summary>
    /// When app was connected to the internet, this is the rate (updates / second) at which the internet connection is checked.
    /// Defaults to one update every 30 seconds.
    /// </summary>
    public float ConnectedUpdateRate
    {
        get => connectedUpdateRate;
        set => connectedUpdateRate = value;
    }

    [SerializeField]
    [Tooltip("When app was disconnected to the internet, this is the rate (updates / second) at which the internet connection is checked. Defaults to one update every second.")]
    private float disconnectedUpdateRate = 1.0f;

    /// <summary>
    /// When app was sicconnected to the internet, this is the rate (updates / second) at which the internet connection is checked.
    /// Defaults to one update every second.
    /// </summary>
    public float DisconnectedUpdateRate
    {
        get => disconnectedUpdateRate;
        set => disconnectedUpdateRate = value;
    }

    [Header("Events")]

    [SerializeField]
    [Tooltip("Event raised when 'isConnected' changes.")]
    private NetworkWatcherConnectionChangedEvent isConnectedChanged = new NetworkWatcherConnectionChangedEvent();

    /// <summary>
    /// Event raised when 'isConnected' changes.
    /// </summary>
    public NetworkWatcherConnectionChangedEvent IsConnectedChanged => isConnectedChanged;
    #endregion Serialized Fields

    #region Public Properties
    public bool IsConnected
    {
        get => _isConnected;

        set
        {
            if (_isConnected != value)
            {
                _isConnected = value;
                isConnectedChanged?.Invoke(value);
            }
        }
    }
    #endregion Public Properties

    #region MonoBehavior Functions
    private void Start()
    {
        isConnectedChanged?.Invoke(_isConnected);
    }

    private void OnEnable()
    {
        TestConnection();
        _connectionTest = StartCoroutine(TestConnectionCoroutine());
    }

    private void OnDisable()
    {
        if (_connectionTest != null)
        {
            StopCoroutine(_connectionTest);
            _connectionTest = null;
        }
    }
    #endregion MonoBehavior Functions

    #region Private Functions
    private void TestConnection()
    {
        IsConnected = Application.internetReachability != NetworkReachability.NotReachable;
    }

    private IEnumerator TestConnectionCoroutine()
    {
        float connectedDelay = 1.0f / connectedUpdateRate;
        float disconnectedDelay = 1.0f / disconnectedUpdateRate;
        while (true)
        {
            yield return new WaitForSecondsRealtime(_isConnected ? connectedDelay : disconnectedDelay);
            TestConnection();
        }
    }
    #endregion Private Functions
}

[Serializable]
public class NetworkWatcherConnectionChangedEvent : UnityEvent<bool>
{ }

