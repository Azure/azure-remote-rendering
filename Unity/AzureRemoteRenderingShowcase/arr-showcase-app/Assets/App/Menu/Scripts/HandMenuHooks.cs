// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication;
using Microsoft.MixedReality.Toolkit.UI;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Hooks up HandMenu UI to make the appropriate application calls.
/// </summary>
public class HandMenuHooks : MonoBehaviour
{
    private IRemoteRenderingService _remoteRenderingService;
    private ISharingService _sharingService;
    private IAnchoringService _anchoringService;
    private Coroutine _updatePlayerCount;
    private bool _sessionFirstConnect;

    #region Serialized Fields
    [Header("Primary Buttons")]

    [SerializeField]
    [Tooltip("The button to be used to show the tools menu.")]
    private Interactable toolsButtonLogic = null;

    /// <summary>
    /// The button to be used to show the tools menu.
    /// </summary>
    public Interactable ToolsButtonLogic
    {
        get => toolsButtonLogic;
        set => toolsButtonLogic = value;
    }

    [SerializeField]
    [Tooltip("The button to be used to show the models menu.")]
    private Interactable modelsButtonLogic = null;

    /// <summary>
    /// The button to be used to show the models menu.
    /// </summary>
    public Interactable ModelsButtonLogic
    {
        get => modelsButtonLogic;
        set => modelsButtonLogic = value;
    }

    [SerializeField]
    [Tooltip("The button to be used to show the session menu.")]
    private Interactable sessionButtonLogic = null;

    /// <summary>
    /// The button to be used to show the session menu.
    /// </summary>
    public Interactable SessionButtonLogic
    {
        get => sessionButtonLogic;
        set => sessionButtonLogic = value;
    }

    [SerializeField]
    [Tooltip("The button to be used to show the status panel.")]
    private Interactable statsButtonLogic = null;

    /// <summary>
    /// The button to be used to show the status panel.
    /// </summary>
    public Interactable StatsButtonLogic
    {
        get => statsButtonLogic;
        set => statsButtonLogic = value;
    }

    [Header("Tool Buttons")]

    [SerializeField]
    [Tooltip("The button to be used to change the pointer into a 'move' tool.  This will move, scale, or rotate the entire model.")]
    private Interactable moveButtonLogic = null;

    /// <summary>
    /// The button to be used to change the pointer into a 'move' tool. This will move, scale, or rotate the entire model.
    /// </summary>
    public Interactable MoveButtonLogic
    {
        get => moveButtonLogic;
        set => moveButtonLogic = value;
    }

    [SerializeField]
    [Tooltip("The button to be used to change the pointer into a 'move piece' tool. This will move, scale, or rotate pieces of a model.")]
    private Interactable movePieceButtonLogic = null;

    /// <summary>
    /// The button to be used to change the pointer into a 'move piece' tool.  This will move, scale, or rotate pieces of a model.
    /// </summary>
    public Interactable MovePieceButtonLogic
    {
        get => movePieceButtonLogic;
        set => movePieceButtonLogic = value;
    }

    [SerializeField]
    [Tooltip("The button to be used to turn on the slice (or clipping) plane.")]
    private Interactable sliceButtonLogic = null;

    /// <summary>
    /// The button to be used to turn on the slice (or clipping) plane.
    /// </summary>
    public Interactable SliceButtonLogic
    {
        get => sliceButtonLogic;
        set => sliceButtonLogic = value;
    }

    [SerializeField]
    [Tooltip("The button to be used to change the pointer into a 'erase' tool. This will erase an entire model.")]
    private Interactable eraseButtonLogic = null;

    /// <summary>
    /// The button to be used to change the pointer into a 'erase' tool. This will erase an entire model.
    /// </summary>
    public Interactable EraseButtonLogic
    {
        get => eraseButtonLogic;
        set => eraseButtonLogic = value;
    }

    [SerializeField]
    [Tooltip("The button to be used to change the pointer into a 'explode' tool. This will explode pieces of the model outwards.")]
    private Interactable explodeButtonLogic = null;

    /// <summary>
    /// The button to be used to change the pointer into a 'explode' tool. This will explode pieces of the model outwards.
    /// </summary>
    public Interactable ExplodeButtonLogic
    {
        get => explodeButtonLogic;
        set => explodeButtonLogic = value;
    }

    [SerializeField]
    [Tooltip("The button to be used to change the pointer into a 'revert' tool. This will revert a model back to its original state.")]
    private Interactable revertButtonLogic = null;

    /// <summary>
    /// The button to be used to change the pointer into a 'revert' tool. This will revert a model back to its original state.
    /// </summary>
    public Interactable RevertButtonLogic
    {
        get => revertButtonLogic;
        set => revertButtonLogic = value;
    }

    [Header("Session Buttons & Parts")]

    [SerializeField]
    [Tooltip("The button to be used to start a new Azure Remote Rendering session, and connect to that session.")]
    private Interactable startSessionButtonLogic = null;

    /// <summary>
    /// The button to be used to start a new Azure Remote Rendering session, and connect to that session.
    /// </summary>
    public Interactable StartSessionButtonLogic
    {
        get => startSessionButtonLogic;
        set => startSessionButtonLogic = value;
    }

    [SerializeField]
    [Tooltip("The button to be used to stop the current Azure Remote Rendering session.")]
    private Interactable stopSessionButtonLogic = null;

    /// <summary>
    /// The button to be used to stop the current Azure Remote Rendering session.
    /// </summary>
    public Interactable StopSessionButtonLogic
    {
        get => stopSessionButtonLogic;
        set => stopSessionButtonLogic = value;
    }

    [SerializeField]
    [Tooltip("The button to be used to open a new web browser to the current Azure Remote Rendering session's 'Inspector' page.")]
    private Interactable webInspectorButtonLogic = null;
    private InteractableEnabledHelper webInspectorButtonHelper = null;

    /// <summary>
    /// The button to be used to open a new web browser to the current Azure Remote Rendering session's 'Inspector' page.
    /// </summary>
    public Interactable WebInspectorButtonLogic
    {
        get => webInspectorButtonLogic;
        set => webInspectorButtonLogic = value;
    }

    [SerializeField]
    [Tooltip("The text mesh displaying the current session status (Starting, Ready, Stopped, Expired, Failure, etc.).")]
    private TextMeshPro sessionStatusText = null;

    [SerializeField]
    [Tooltip("The alternate text mesh displaying the current session status (Starting, Ready, Stopped, Expired, Failure, etc.).")]
    private TextMeshPro sessionStatusTextAlt = null;
    
    /// <summary>
    /// The text mesh displaying the current session's status (Starting, Ready, Stopped, Expired, Failure, etc.).
    /// </summary>
    public TextMeshPro SessionStatusText
    {
        get => sessionStatusText;
        set => sessionStatusText = value;
    }

    [SerializeField]
    [Tooltip("The text mesh displaying the current session's region (West US, West Europe, etc.).")]
    private TextMeshPro sessionRegionText = null;

    /// <summary>
    /// The text mesh displaying the current session's region (West US, West Europe, etc.).
    /// </summary>
    public TextMeshPro SessionRegionText
    {
        get => sessionRegionText;
        set => sessionRegionText = value;
    }

    [SerializeField]
    [Tooltip("The text mesh displaying the current session's size (small, big, ect.).")]
    private TextMeshPro sessionSizeText = null;

    /// <summary>
    /// The text mesh displaying the current session's size (small, big, ect.).
    /// </summary>
    public TextMeshPro SessionSizeText
    {
        get => sessionSizeText;
        set => sessionSizeText = value;
    }

    [SerializeField]
    [Tooltip("The text mesh displaying how long a session has been active.")]
    private TextMeshPro sessionDurationText = null;

    /// <summary>
    /// The text mesh displaying how long a session has been active.
    /// </summary>
    public TextMeshPro SessionDurationText
    {
        get => sessionDurationText;
        set => sessionDurationText = value;
    }

    [SerializeField]
    [Tooltip("The text mesh pro displaying the overall status of the current rendering session. This contains performance statistics, such as frame rate and latency.")]
    private TextMeshPro debugMenuText = null;

    /// <summary>
    /// The text mesh pro displaying the overall status of the current rendering session. This contains performance statistics, such as frame rate and latency.
    /// </summary>
    public TextMeshPro DebugMenuText
    {
        get => debugMenuText;
        set => debugMenuText = value;
    }

    [Header("Session Configuration Buttons & Parts")]

    [SerializeField]
    [Tooltip("The button to forget the saved session.")]
    private Interactable forgetSessionButtonLogic = null;

    /// <summary>
    /// The button to forget the saved session.
    /// </summary>
    public Interactable ForgetSessionButtonLogic
    {
        get => forgetSessionButtonLogic;
        set => forgetSessionButtonLogic = value;
    }

    [Header("Sharing Buttons & Parts")]

    [SerializeField]
    [Tooltip("The button to be used to create a new sharing room and join it.")]
    private Interactable createAndJoinRoomButtonLogic = null;

    /// <summary>
    /// The button to be used to create a new sharing room and join it.
    /// </summary>
    public Interactable CreateAndJoinRoomButtonLogic
    {
        get => createAndJoinRoomButtonLogic;
        set => createAndJoinRoomButtonLogic = value;
    }
    
    [SerializeField]
    [Tooltip("The button to be used to leave a sharing room.")]
    private Interactable leaveRoomButtonLogic = null;

    /// <summary>
    /// The button to be used to leave a sharing room.
    /// </summary>
    public Interactable LeaveRoomButtonLogic
    {
        get => LeaveRoomButtonLogic;
        set => LeaveRoomButtonLogic = value;
    }

    [SerializeField]
    [Tooltip("The button to be used to update the list of available rooms.")]
    private Interactable updateRoomsButtonLogic = null;

    /// <summary>
    /// The button to be used to update the list of available rooms.
    /// </summary>
    public Interactable UpdateRoomsButtonLogic
    {
        get => updateRoomsButtonLogic;
        set => updateRoomsButtonLogic = value;
    }

    [SerializeField]
    [Tooltip("The text mesh displaying the current sharing status (Disconnected or Connected, ect.).")]
    private TextMeshPro sharingStatusText = null;

    /// <summary>
    /// The text mesh displaying the current sharing status (Disconnected or Connected, ect.).
    /// </summary>
    public TextMeshPro SharingStatusText
    {
        get => sharingStatusText;
        set => sharingStatusText = value;
    }

    [SerializeField]
    [Tooltip("The text mesh displaying the current sharing room.")]
    private TextMeshPro sharingRoomText = null;

    /// <summary>
    /// The text mesh displaying the current sharing room.
    /// </summary>
    public TextMeshPro SharingRoomText
    {
        get => sharingRoomText;
        set => sharingRoomText = value;
    }

    [SerializeField]
    [Tooltip("The text mesh displaying the number of players in the curent room.")]
    private TextMeshPro sharingPlayerCountText = null;

    /// <summary>
    /// The text mesh displaying the number of players in the curent room.
    /// </summary>
    public TextMeshPro SharingPlayerCountText
    {
        get => sharingPlayerCountText;
        set => sharingPlayerCountText = value;
    }

    [Header("Status Parts")]

    [SerializeField]
    [Tooltip("The text mesh displaying various application notifications.")]
    private NotificationBarController notificationBar = null;

    /// <summary>
    /// The text mesh displaying various application notifications.
    /// </summary>
    public NotificationBarController NotificationBar
    {
        get => notificationBar;
        set => notificationBar = value;
    }
    
    [SerializeField]
    [Tooltip("The icon object displaying which tool is currently selected.")]
    private PointerToolIcon pointerToolIcon = null;

    /// <summary>
    /// The icon object displaying which tool is currently selected.
    /// </summary>
    public PointerToolIcon PointerToolIcon
    {
        get => pointerToolIcon;
        set => pointerToolIcon = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Functions

    private void Awake()
    {
        webInspectorButtonHelper = webInspectorButtonLogic.GetComponent<InteractableEnabledHelper>();
    }

    /// <summary>
    /// Initialize all the buttons click listners and various other menu states.
    /// </summary>
    private void Start()
    {
        moveButtonLogic.OnClick.AddListener(delegate { SetPointerMode(PointerMode.Manipulate); });
        movePieceButtonLogic.OnClick.AddListener(delegate { SetPointerMode(PointerMode.ManipulatePiece); });
        sliceButtonLogic.OnClick.AddListener(delegate { SetPointerMode(PointerMode.ClipBar); });
        eraseButtonLogic.OnClick.AddListener(delegate { SetPointerMode(PointerMode.Delete); });
        explodeButtonLogic.OnClick.AddListener(delegate { SetPointerMode(PointerMode.Explode); });
        revertButtonLogic.OnClick.AddListener(delegate { SetPointerMode(PointerMode.Reset); });
        startSessionButtonLogic.OnClick.AddListener(delegate { StartAndConnectSession(); });
        stopSessionButtonLogic.OnClick.AddListener(delegate { StopSession(); });
        forgetSessionButtonLogic.OnClick.AddListener(delegate { ForgetSession(); });
        toolsButtonLogic.OnClick.AddListener(delegate { SetMenuState(MenuState.Tools); });
        modelsButtonLogic.OnClick.AddListener(delegate { SetMenuState(MenuState.Models); });
        sessionButtonLogic.OnClick.AddListener(delegate { SetMenuState(MenuState.Session); });
        statsButtonLogic.OnClick.AddListener(delegate { SetMenuState(MenuState.Stats); });
        webInspectorButtonLogic.OnClick.AddListener(delegate { _remoteRenderingService?.PrimaryMachine?.Session.OpenWebInspector(); });
        createAndJoinRoomButtonLogic.OnClick.AddListener(delegate
        {
            ClearMenu();
            _sharingService?.CreateAndJoinRoom();
        });
        leaveRoomButtonLogic.OnClick.AddListener(delegate { _sharingService?.LeaveRoom(); });

        _remoteRenderingService = AppServices.RemoteRendering;
        if (_remoteRenderingService != null)
        {
            _remoteRenderingService.StatusChanged += RemoteRendering_StatusChanged;
            UpdateNotifcationsAndSessionButtons();
            // Hide web inspector on non debug profile builds
            if(_remoteRenderingService.LoadedProfile.AuthType == AuthenticationType.AccessToken)
                webInspectorButtonLogic.gameObject.SetActive(false);
        }

        _sharingService = AppServices.SharingService;
        if (_sharingService != null)
        {
            _sharingService.CurrentRoomChanged += SharingService_CurrentRoomChanged;
            _sharingService.Connected += SharingService_ConnectionChanged;
            _sharingService.Disconnected += SharingService_ConnectionChanged;
            _sharingService.PlayerAdded += SharingService_PlayerAdded;
            _sharingService.PlayerRemoved += SharingService_PlayerRemoved;
            UpdateSharingConnectionStatus();
        }

        _anchoringService = AppServices.AnchoringService;
        if (_anchoringService != null)
        {
            _anchoringService.ActiveSearchesCountChanged += AnchoringService_ActiveSearchesCountChanged;
            _anchoringService.ActiveCreationsCountChanged += AnchoringService_ActiveCreationsCountChanged;
        }

        if (notificationBar != null)
        {
            notificationBar.NotificationBarHidden.AddListener(WhenNotificationBarHiddenShowAnchorInformation);
        }

        // Set default state to manipulation
        SetPointerMode(PointerMode.Manipulate);
    }

    /// <summary>
    /// Update the debug menu text as well as the session duration text.
    /// </summary>
    private void Update()
    {
        if (_remoteRenderingService != null)
        {
            if (debugMenuText.gameObject.activeInHierarchy)
            {
                debugMenuText.text = _remoteRenderingService.DebugStatus;
            }

            if (sessionDurationText.gameObject.activeInHierarchy)
            {
                if (_remoteRenderingService.PrimaryMachine != null)
                {
                    sessionDurationText.text = _remoteRenderingService.PrimaryMachine.Session.ElapsedTime.ToString(@"hh\:mm\:ss");
                }
                else
                {
                    sessionDurationText.text = string.Empty;
                }
            }
        }
    }

    /// <summary>
    /// Release resources.
    /// </summary>
    private void OnDestroy()
    {
        if (_remoteRenderingService != null)
        {
            _remoteRenderingService.StatusChanged -= RemoteRendering_StatusChanged;
        }

        if (_sharingService != null)
        {
            _sharingService.CurrentRoomChanged -= SharingService_CurrentRoomChanged;
            _sharingService.Connected -= SharingService_ConnectionChanged;
            _sharingService.Disconnected -= SharingService_ConnectionChanged;
            _sharingService.PlayerAdded -= SharingService_PlayerAdded;
            _sharingService.PlayerRemoved -= SharingService_PlayerRemoved;
        }

        if (_anchoringService != null)
        {
            _anchoringService.ActiveSearchesCountChanged -= AnchoringService_ActiveSearchesCountChanged;
            _anchoringService.ActiveCreationsCountChanged -= AnchoringService_ActiveCreationsCountChanged;
        }

        if (notificationBar != null)
        {
            notificationBar.NotificationBarHidden.RemoveListener(WhenNotificationBarHiddenShowAnchorInformation);
        }
    }
    #endregion MonoBehavior Functions

    #region Public Functions
    /// <summary>
    /// Close all menus.
    /// </summary>
    public void ClearMenu()
    {
        SetMenuState(MenuState.None);
    }

    /// <summary>
    /// Turn off all pointer tools.
    /// </summary>
    public void ClearPointerMode()
    {
        AppServices.PointerStateService.Mode = PointerMode.None;
    }

    /// <summary>
    /// Set the pointer to a particular tool. If a tool is already selected, set the selection to manipulate.
    /// </summary>
    /// <param name="pointerMode"></param>
    public void SetPointerMode(PointerMode pointerMode)
    {
        if(AppServices.PointerStateService.Mode == pointerMode)
        {
            pointerMode = PointerMode.Manipulate;
        }
        AppServices.PointerStateService.Mode = pointerMode;
    }
    #endregion Public Functions

    #region Private Functions
    private async void StartAndConnectSession()
    {
        if (_remoteRenderingService != null)
        {
            await _remoteRenderingService.StopAll();
            var machine = await _remoteRenderingService.Create();
            machine?.Session.Connection.Connect();
        }
    }

    private async void StopSession()
    {
        await _remoteRenderingService?.StopAll();
    }

    private async void ForgetSession()
    {
        await _remoteRenderingService?.ClearAll();
    }

    private void RemoteRendering_StatusChanged(object sender, IRemoteRenderingStatusChangedArgs args)
    {
        UpdateNotifcationsAndSessionButtons();
    }

    private void UpdateNotifcationsAndSessionButtons()
    {
        if (_remoteRenderingService == null)
        {
            return;
        }

        bool showStartSessionButton = true;
        bool showWebInspectorButton = false;
        bool shouldHideNotification = false;
        switch (_remoteRenderingService.Status)
        {
            case RemoteRenderingServiceStatus.SessionReadyAndDisconnected:
                sessionStatusText.text = "Disconnected";
                notificationBar.SetScrollableNotification(3f, "Session disconnected.");
                break;
            case RemoteRenderingServiceStatus.SessionReadyAndConnectionError:
                sessionStatusText.text = "Disconnected";
                notificationBar.SetNotification(-1f, 0.3f, new string[]{"Connection failed. Retrying to connect.", "Connection failed. Retrying to connect..", "Connection failed. Retrying to connect..."});
                break;
            case RemoteRenderingServiceStatus.SessionReadyAndConnected:
                sessionStatusText.text = "Connected";
                notificationBar.SetScrollableNotification(3f, "Session connected.");
                showStartSessionButton = false;
                showWebInspectorButton = _remoteRenderingService.LoadedProfile.AuthType == AuthenticationType.AccountKey;
                // Switch to session menu if not already there, and prevent calling this twice
                if(!_sessionFirstConnect && !sessionButtonLogic.GetComponent<ToggleObject>().TargetObject.activeInHierarchy)
                {
                    SetMenuState(MenuState.Session);
                    _sessionFirstConnect = true;
                }
                break;
            case RemoteRenderingServiceStatus.SessionReadyAndConnecting:
                sessionStatusText.text = "Connecting";
                notificationBar.SetNotification(-1f, 0.3f, new string[] { "Connecting to session.", "Connecting to session..", "Connecting to session..." });
                showStartSessionButton = false;
                break;
            case RemoteRenderingServiceStatus.SessionConstruction:
                sessionStatusText.text = "Initializing";
                showStartSessionButton = false;
                break;
            case RemoteRenderingServiceStatus.SessionStarting:
                sessionStatusText.text = "Starting";
                notificationBar.SetNotification(-1f, 0.3f, new string[] { "Starting the session. This may take a few minutes.", "Starting the session. This may take a few minutes..", "Starting the session. This may take a few minutes..." });
                showStartSessionButton = false;
                break;
            case RemoteRenderingServiceStatus.SessionError:
                sessionStatusText.text = "Failure";
                notificationBar.SetScrollableNotification(3f, "Session failed to start.");
                break;
            case RemoteRenderingServiceStatus.SessionExpired:
                sessionStatusText.text = "Expired";
                notificationBar.SetScrollableNotification(3f, "Session expired.");
                break;
            case RemoteRenderingServiceStatus.SessionStopped:
                sessionStatusText.text = "Stopped";
                notificationBar.SetScrollableNotification(3f, "Session stopped.");
                break;
            case RemoteRenderingServiceStatus.NoSession:
            case RemoteRenderingServiceStatus.Unknown:
                sessionStatusText.text = "No Session";
                shouldHideNotification = true;
                break;
            default:
                Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  $"Unknown remoting status '{_remoteRenderingService.Status}'");
                sessionStatusText.text = string.Empty;
                break;
        }

        // Update alternate session status text
        sessionStatusTextAlt.text = sessionStatusText.text;

        // force anchor states to override the last message
        shouldHideNotification &= !TrySendingNotificationAreaWithAnchorInformation();

        // hide notification is there's no session and no anchoring messagess
        if (shouldHideNotification)
        {
            notificationBar.HideNotification();
        }

        if (sessionRegionText != null)
        {
            sessionRegionText.text = _remoteRenderingService.PrimaryMachine == null ? 
                "None" : _remoteRenderingService.PrimaryMachine.Session.Location.ToString();
        }

        if (sessionSizeText != null)
        {
            sessionSizeText.text = _remoteRenderingService.PrimaryMachine == null ?
                "None" : _remoteRenderingService.PrimaryMachine.Session.Size.ToString();
        }

        // Clear session first connect state
        if(_remoteRenderingService.Status != RemoteRenderingServiceStatus.SessionReadyAndConnected)
        {
            _sessionFirstConnect = false;
        }
        
        startSessionButtonLogic.gameObject.SetActive(showStartSessionButton);
        stopSessionButtonLogic.gameObject.SetActive(!showStartSessionButton);
        
        webInspectorButtonHelper.IsEnabled = showWebInspectorButton;
    }

    /// <summary>
    /// If the notification bar was hidden for some reason, show any available anchoring information.
    /// </summary>
    private void WhenNotificationBarHiddenShowAnchorInformation()
    {
        TrySendingNotificationAreaWithAnchorInformation();
    }

    /// <summary>
    /// Try sending a notification with anchor information. If a notification was sent, return true.
    /// </summary>
    private bool TrySendingNotificationAreaWithAnchorInformation()
    {
        bool sentNotification = false;
        // Force the anchor searches and creations to show in notification area.
        if (_anchoringService != null)
        {
            if (_anchoringService.ActiveCreationsCount > 0)
            {
                string savingAnchors = $"Saving {_anchoringService.ActiveCreationsCount} cloud anchor(s).";
                notificationBar.SetNotification(-1f, 0.3f, new string[] { savingAnchors, savingAnchors + ".", savingAnchors + ".." });
                sentNotification = true;
            }
            else if (_anchoringService.ActiveSearchesCount > 0)
            {
                string searchingForAnchors = $"Searching for {_anchoringService.ActiveSearchesCount} cloud anchor(s).";
                notificationBar.SetNotification(-1f, 0.3f, new string[] { searchingForAnchors, searchingForAnchors + ".", searchingForAnchors + ".." });
                sentNotification = true;
            }
        }
        return sentNotification;
    }

    private void SharingService_CurrentRoomChanged(ISharingService sender, ISharingServiceRoom room)
    {
        UpdateSharingConnectionStatus();
    }

    private void SharingService_ConnectionChanged(ISharingService sender)
    {
        UpdateSharingConnectionStatus();
    }
    private void SharingService_PlayerAdded(ISharingService sender, ISharingServicePlayer player)
    {
        InvalidateSharingPlayerCount();
    }

    private void SharingService_PlayerRemoved(ISharingService sender, ISharingServicePlayer player)
    {
        InvalidateSharingPlayerCount();
    }

    private void UpdateSharingConnectionStatus()
    {
        bool joinedRoom = _sharingService?.CurrentRoom != null;
        if (sharingRoomText != null)
        {
            sharingRoomText.text = joinedRoom ? _sharingService.CurrentRoom.Name : "None";
            if(joinedRoom)
            {
                int count = _sharingService.Players?.Count ?? 0;
                notificationBar.SetScrollableNotification(3f, $"Joined Shared {_sharingService.CurrentRoom.Name} with {count} users.");
            }
        }

        if (sharingStatusText != null && _sharingService != null)
        {
            sharingStatusText.text = _sharingService.IsConnected ? "Connected" : "Disconnected";
        }
        
        createAndJoinRoomButtonLogic.gameObject.SetActive(!joinedRoom);
        updateRoomsButtonLogic.gameObject.SetActive(!joinedRoom);
        leaveRoomButtonLogic.gameObject.SetActive(joinedRoom);
    }

    private void InvalidateSharingPlayerCount()
    {
        if (_updatePlayerCount == null)
        {
            _updatePlayerCount = StartCoroutine(UpdateSharingPlayerCount());
        }
    }

    private IEnumerator UpdateSharingPlayerCount()
    {
        yield return null;

        if (sharingPlayerCountText != null && _sharingService != null)
        {
            int count = _sharingService.Players?.Count ?? 0;
            sharingPlayerCountText.text = count <= 0 ? "None" : count.ToString();
        }

        _updatePlayerCount = null;
    }

    private void SetMenuState(MenuState state)
    {
        // sets toggle state of menu objects.
        // if menu is currently open, hitting button again will turn it off.
        ToggleObject toggleLogic = toolsButtonLogic.GetComponent<ToggleObject>();
        if(state == MenuState.Tools && toggleLogic.TargetObject.activeInHierarchy) state = MenuState.None;
        toggleLogic.SetObjectActive(state == MenuState.Tools);

        toggleLogic = modelsButtonLogic.GetComponent<ToggleObject>();
        if(state == MenuState.Models && toggleLogic.TargetObject.activeInHierarchy) state = MenuState.None;
        toggleLogic.SetObjectActive(state == MenuState.Models);

        toggleLogic = sessionButtonLogic.GetComponent<ToggleObject>();
        if(state == MenuState.Session && toggleLogic.TargetObject.activeInHierarchy) state = MenuState.None;
        toggleLogic.SetObjectActive(state == MenuState.Session);

        toggleLogic = statsButtonLogic.GetComponent<ToggleObject>();
        if(state == MenuState.Stats && toggleLogic.TargetObject.activeInHierarchy) state = MenuState.None;
        toggleLogic.SetObjectActive(state == MenuState.Stats);

        pointerToolIcon.HandMenuStateShow = state == MenuState.Tools || state == MenuState.None;
        
        switch (state)
        {
            case MenuState.Tools:
                break;
            case MenuState.Models:
                RemoteObjectListLoader.RefreshLists();
                break;
            case MenuState.Session:
                break;
            case MenuState.Stats:
                break;
            case MenuState.None:
                break;
        }
    }

    private void AnchoringService_ActiveSearchesCountChanged(IAnchoringService sender, AnchoringServiceSearchingArgs searchingArgs)
    {
        UpdateNotifcationsAndSessionButtons();
    }

    private void AnchoringService_ActiveCreationsCountChanged(IAnchoringService sender, AnchoringServiceCreatingArgs creatingArgs)
    {
        UpdateNotifcationsAndSessionButtons();
    }
    #endregion Private Functions

    #region Private Enums
    private enum MenuState
    {
        Tools,
        Models,
        Session,
        Stats,
        None
    }
    #endregion Private Enums
}
