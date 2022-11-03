// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication;
using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A behavior to watch the room connection status
/// </summary>
public class ShareRoomWatcher : MonoBehaviour
{
    private bool _isConnectedKnown = false;

    #region Serialized Fields
    [Header("Events")]

    [SerializeField]
    [Tooltip("Event raised when client is connected to sharing room/session.")]
    private UnityEvent connected = new UnityEvent();

    /// <summary>
    /// Event raised when client is connected to sharing room/session.
    /// </summary>
    public UnityEvent Connected => connected;

    [SerializeField]
    [Tooltip("Event raised when client is disconnected to sharing room/session.")]
    private UnityEvent disconnected = new UnityEvent();

    /// <summary>
    /// Event raised when client is disconnected to sharing room/session.
    /// </summary>
    public UnityEvent Disconnected => disconnected;

    [SerializeField]
    [Tooltip("Event raised when a private room is joined.")]
    private UnityEvent privateRoomJoined = new UnityEvent();

    /// <summary>
    /// Event raised when a private room is joined.
    /// </summary>
    public UnityEvent PrivateRoomJoined => privateRoomJoined;

    [SerializeField]
    [Tooltip("Event raised when a public room is joined.")]
    private UnityEvent publicRoomJoined = new UnityEvent();

    /// <summary>
    /// Event raised when a public room is joined.
    /// </summary>
    public UnityEvent PublicRoomJoined => publicRoomJoined;

    [SerializeField]
    [Tooltip("Event raised when the IsConnected value has changed.")]
    private ShareRoomWatcherIsConnectedRoomEvent isConnectedChanged = new ShareRoomWatcherIsConnectedRoomEvent();

    /// <summary>
    /// Event raised when the IsConnected value has changed.
    /// </summary>
    public ShareRoomWatcherIsConnectedRoomEvent IsConnectedChanged => isConnectedChanged;

    [SerializeField]
    [Tooltip("Event raised when the IsPrivateRoom value has changed.")]
    private ShareRoomWatcherIsPrivateRoomEvent isPrivateRoomChanged = new ShareRoomWatcherIsPrivateRoomEvent();

    /// <summary>
    /// Event raised when the IsPrivateRoom value has changed.
    /// </summary>
    public ShareRoomWatcherIsPrivateRoomEvent IsPrivateRoomChanged => isPrivateRoomChanged;
    #endregion Serialized Fields

    #region Public Properties
    /// <summary>
    /// Get if the client is connected to a room
    /// </summary>
    public bool IsConnected { get; private set; }

    /// <summary>
    /// Get if the client is connect to a private room
    /// </summary>
    public bool IsPrivateRoom { get; private set; }
    #endregion Public Properties

    #region MonoBehaviour Functions
    private void Awake()
    {
        isPrivateRoomChanged?.Invoke(IsPrivateRoom);
    }

    private void OnEnable()
    {
        AppServices.SharingService.CurrentRoomChanged += OnCurrentRoomChanged;
        OnCurrentRoomChanged(null, AppServices.SharingService.CurrentRoom);
    }

    private void OnDisable()
    {
        AppServices.SharingService.CurrentRoomChanged -= OnCurrentRoomChanged;
    }
    #endregion MonoBehavior Functions

    #region Private Functions
    private void OnCurrentRoomChanged(ISharingService sender, ISharingServiceRoom room)
    {
        bool isConnected = room != null;
        bool isPrivateRoom = isConnected && room.IsPrivate;

        if (!_isConnectedKnown ||
            isConnected != IsConnected ||
            isPrivateRoom != IsPrivateRoom)
        {
            _isConnectedKnown = true;
            IsConnected = isConnected;
            IsPrivateRoom = isConnected && room.IsPrivate;

            if (IsConnected)
            {
                connected?.Invoke();
                if (IsPrivateRoom)
                {
                    privateRoomJoined?.Invoke();
                }
                else
                {
                    publicRoomJoined?.Invoke();
                }
            }
            else
            {
                disconnected?.Invoke();
            }

            isConnectedChanged?.Invoke(IsConnected);
            isPrivateRoomChanged?.Invoke(IsPrivateRoom);
        }
    }
    #endregion Private Functions
}

[Serializable]
public class ShareRoomWatcherIsPrivateRoomEvent : UnityEvent<bool>
{
}

[Serializable]
public class ShareRoomWatcherIsConnectedRoomEvent : UnityEvent<bool>
{
}
