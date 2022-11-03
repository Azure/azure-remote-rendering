// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A menu for showing player specific actions and information
/// </summary>
public class ShareUserSubMenu : SubMenu
{
    private const float TimeoutInSeconds = 10.0f;
    private string _targetPlayerId = null;
    private List<TimeSpan> _totalPingsReceived = new List<TimeSpan>();
    private Coroutine _timeoutRoutine = null;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The sub-menu controller that controls which sub-menu is visible. This is used by the back button to go back to the main menu.")]
    private SubMenuController menuController;

    /// <summary>
    /// The sub-menu controller that controls which sub-menu is visible.
    /// This is used by the back button to go back to the main menu.
    /// </summary>
    public SubMenuController MenuController
    {
        get => menuController;
        set => menuController = value;
    }

    [SerializeField]
    [Tooltip("The index of the menu to go to when the back button is clicked.")]
    private int backDestinationIndex = 0;

    /// <summary>
    /// The index of the menu to go to when the back button is clicked.
    /// </summary>
    public int BackDestinationIndex
    {
        get => backDestinationIndex;
        set => backDestinationIndex = value;
    }

    [SerializeField]
    [Tooltip("The text mesh display the panel title.")]
    private TextMesh titleText = null;

    /// <summary>
    /// The text mesh displaying the number of players in the current room.
    /// </summary>
    public TextMesh TitleText
    {
        get => titleText;
        set => titleText = value;
    }

    [SerializeField]
    [Tooltip("close button to control closing.")]
    private Interactable closeButtonLogic = null;

    /// <summary>
    /// the close button
    /// </summary>
    public Interactable CloseButtonLogic
    {
        get => closeButtonLogic;
        set => closeButtonLogic = value;
    }

    [SerializeField]
    [Tooltip("Broadcast button to control closing.")]
    private Interactable broadcastButtonLogic = null;

    /// <summary>
    /// broadcast ping button
    /// </summary>
    public Interactable BroadcastButtonLogic
    {
        get => broadcastButtonLogic;
        set => broadcastButtonLogic = value;
    }

    [SerializeField]
    [Tooltip("Directed button to control closing.")]
    private Interactable directedButtonLogic = null;

    /// <summary>
    /// selected ping button
    /// </summary>
    public Interactable DirectedButtonLogic
    {
        get => directedButtonLogic;
        set => directedButtonLogic = value;
    }

    [SerializeField]
    [Tooltip("The text for broadcast ping value.")]
    private TMPro.TextMeshPro broadcastPingValue = null;

    /// <summary>
    /// Text for broadcast ping value
    /// </summary>
    public TMPro.TextMeshPro BroadcastPingValue
    {
        get => broadcastPingValue;
        set => broadcastPingValue = value;
    }

    [SerializeField]
    [Tooltip("The text mesh display the panel title.")]
    private TMPro.TextMeshPro directedPingValue = null;

    /// <summary>
    /// Text for directed ping value
    /// </summary>
    public TMPro.TextMeshPro DirectedPingValue
    {
        get => directedPingValue;
        set => directedPingValue = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Functions
    private void Awake()
    {
        closeButtonLogic.OnClick.AddListener(delegate { CloseSubMenu(); });
        broadcastButtonLogic.OnClick.AddListener(delegate { BroadcasePing(); });
        directedButtonLogic.OnClick.AddListener(delegate { DirectedPing(); });
    }

    private void OnEnable()
    {
        AppServices.SharingService.PingReturned += OnPingReturned;
        AppServices.SharingService.PlayerRemoved += OnPlayerRemoved;
        ResetDisplay();
    }

    private void OnDisable()
    {
        _targetPlayerId = null;
        ResetDisplay();
        _totalPingsReceived.Clear();

        AppServices.SharingService.PlayerRemoved -= OnPlayerRemoved;
        AppServices.SharingService.PingReturned -= OnPingReturned;
    }
    #endregion MonoBehavior Functions

    #region Public Functions
    public void CloseSubMenu()
    {
        menuController.GoToMenu(backDestinationIndex);
    }

    public void BroadcasePing()
    {
        ResetDisplay();
        _timeoutRoutine = StartCoroutine(WaitForPingTimeout(false));
        AppServices.SharingService.SendPing();
    }

    public void DirectedPing()
    {
        if (string.IsNullOrEmpty(_targetPlayerId))
        {
            return;
        }

        ResetDisplay();
        _timeoutRoutine = StartCoroutine(WaitForPingTimeout(true));
        AppServices.SharingService.SendPing(_targetPlayerId);
    }

    public void SetPlayer(string playerId)
    {
        _targetPlayerId = playerId;

        object displayName = null;
        if (AppServices.SharingService.TryGetPlayerProperty(playerId, SharableStrings.PlayerName, out displayName))
        {
            titleText.text = displayName.ToString();
        }
    }
    #endregion Public Functions

    #region Private Functions
    private void OnPingReturned(ISharingService sharingService, string playerId, TimeSpan delta)
    {
        if (_timeoutRoutine == null)
        {
            // timeout probably happened, just return
            return;
        }

        // since this is not on the ui thread, just add the deltas to the collection
        _totalPingsReceived.Add(delta);
    }

    private void OnPlayerRemoved(ISharingService sender, ISharingServicePlayer args)
    {
        if (args.Data.PlayerId == _targetPlayerId)
        {
            CloseSubMenu();
        }
    }

    private void ResetDisplay()
    {
        if (_timeoutRoutine != null)
        {
            StopCoroutine(_timeoutRoutine);
            _timeoutRoutine = null;
        }

        _totalPingsReceived.Clear();
        broadcastPingValue.text = string.Empty;
        directedPingValue.text = string.Empty;
    }

    private IEnumerator WaitForPingTimeout(bool waitForSinglePing)
    {
        var pingResults = 1;
        if (!waitForSinglePing)
        {
            pingResults = AppServices.SharingService.Players.Count - 1;
        }

        float deltaTime = 0;

        while (_totalPingsReceived.Count != pingResults)
        {
            deltaTime += Time.deltaTime;
            if (deltaTime > TimeoutInSeconds)
            {
                break;
            }

            yield return null;
        }

        if (_timeoutRoutine != null)
        {
            if (_totalPingsReceived.Count != pingResults)
            {
                broadcastPingValue.text = "timed out";
                directedPingValue.text = "timed out";
            }
            else if (waitForSinglePing)
            {
                directedPingValue.text = $"{_totalPingsReceived[0].TotalMilliseconds} ms.";
            }
            else
            {
                TimeSpan deltas = TimeSpan.Zero;
                foreach (var time in _totalPingsReceived)
                {
                    deltas += time;
                }

                var averageMs = deltas.TotalMilliseconds / _totalPingsReceived.Count;

                BroadcastPingValue.text = $"{averageMs} ms.";
            }
        }

        _timeoutRoutine = null;
    }
    #endregion Private Functions
}
