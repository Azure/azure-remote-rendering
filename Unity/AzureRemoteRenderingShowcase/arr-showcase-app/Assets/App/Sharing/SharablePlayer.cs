// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Extensions;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// A class for receiving the state of non-local players.
/// </summary>
public class SharablePlayer : MonoBehaviour
{
    private ISharingServicePlayer _player;
    private event Action<string, object> _propertyChanged;

    #region Public Properties
    /// <summary>
    /// The child game object containing the visual representation of the player.
    /// </summary>
    public GameObject PlayerVisual
    {
        get => playerVisual;
        set => playerVisual = value;
    }

    [SerializeField]
    [Tooltip("The child game object containing the visual representation of the player.")]
    private GameObject playerVisual = null;
    
    /// <summary>
    /// The label which will be used to display the player's name.
    /// </summary>
    public TextMeshPro PlayerNameLabel
    {
        get => playerNameLabel;
        set => playerNameLabel = value;
    }

    [SerializeField]
    [Tooltip("The label which will be used to display the player's name.")]
    private TextMeshPro playerNameLabel = null;
    
    /// <summary>
    /// Get or set the player objects that controls the position and rotation of this object.
    /// </summary>
    public ISharingServicePlayer Player
    {
        get => _player;

        set
        {
            if (_player != value)
            {
                UnregisterPlayerHandlers();
                _player = value;
                RegisterPlayerHandlers();
                ReplayPropertyChanges(_propertyChanged);
            }
        }
    }
    #endregion Public Properties

    #region Public Events
    /// <summary>
    /// Event fired when a property changes. Events will be replayed on added event handlers.
    /// </summary>
    public event Action<string, object> PropertyChanged
    {
        add
        {
            _propertyChanged += value;
            ReplayPropertyChanges(value);
        }

        remove
        {
            _propertyChanged -= value;
        }
    }
    #endregion

    #region MonoBehaviour Functions

    /// <summary>
    /// Hide the player visual representation until our first pose update is received.
    /// </summary>
    private void Start()
    {
        playerVisual.SetActive(false);
    }

    /// <summary>
    /// Update the player's game object position and rotation based on the last received server data.
    /// </summary>
    private void Update()
    {
        if (Player != null)
        {
            if (Player.Pose.position.IsValidVector())
            {
                playerVisual.SetActive(true);
                transform.localPosition = Player.Pose.position;
            }

            if (Player.Pose.rotation.IsValidRotation())
            {
                transform.localRotation = Player.Pose.rotation;
            }
        }
    }

    private void OnDestroy()
    {
        UnregisterPlayerHandlers();
    }
    #endregion MonoBehaviour Functions

    #region Private Functions
    /// <summary>
    /// Start handling events raised by the current Player object.
    /// </summary>
    private void RegisterPlayerHandlers()
    {
        if (_player == null)
        {
            return;
        }

        _player.PropertyChanged += HandlePlayerPropertyChanged;
    }

    /// <summary>
    /// Stop handling events raised by the current Player object.
    /// </summary>
    private void UnregisterPlayerHandlers()
    {
        if (_player == null)
        {
            return;
        }

        _player.PropertyChanged -= HandlePlayerPropertyChanged;
    }

    /// <summary>
    /// Handle player property changes received from the server.
    /// </summary>
    private void HandlePlayerPropertyChanged(ISharingServicePlayer sender, string property, object value)
    {
        _propertyChanged?.Invoke(property, value);
        // Update player name label
        if(playerNameLabel != null && property == SharableStrings.PlayerName)
        {
            playerNameLabel.text = (string)value;
        }
    }
    
    /// <summary>
    /// Replay all property change events on the given event handler.
    /// </summary>
    private void ReplayPropertyChanges(Action<string, object> propertyChangeHandler)
    {
        if (_player == null || _player.Properties == null || propertyChangeHandler == null)
        {
            return;
        }

        // The properties can change during handler callbacks, so copy the dictionary first.
        var toReplay = new List<(string, object)>(_player.Properties.Count);

        foreach (var property in _player.Properties)
        {
            if (property.Value != null)
            {
                toReplay.Add((property.Key, property.Value));
            }
        }

        foreach (var property in toReplay)
        {
            propertyChangeHandler(property.Item1, property.Item2);
        }
    }
    #endregion Private Functions
}

