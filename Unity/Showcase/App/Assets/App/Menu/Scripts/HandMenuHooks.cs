// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication;
using Microsoft.MixedReality.Toolkit.UI;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Hooks up HandMenu UI to make the appropriate application calls.
/// </summary>
public class HandMenuHooks : MonoBehaviour, IMainMenu
{
    private IRemoteRenderingService _remoteRenderingService;
    private ISharingService _sharingService;
    private IAnchoringService _anchoringService;
    private Coroutine _updatePlayerCount;
    private bool _sessionFirstConnect;
    private MenuState? _lockedState;

    // IMainMenu
    public string Name => gameObject.name;

    #region Serialized Fields
    [Header("Components")]

    [SerializeField]
    [Tooltip("The app's network watching, watching for network connections.")]
    private NetworkWatcher networkWatcher;

    /// <summary>
    /// The app's network watching, watching for network connections.
    /// </summary>
    public NetworkWatcher NetworkWatcher
    {
        get => networkWatcher;
        set => networkWatcher = value;
    }

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
    private Interactable shareButtonLogic = null;

    /// <summary>
    /// The button to be used to show the status panel.
    /// </summary>
    public Interactable StatsButtonLogic
    {
        get => shareButtonLogic;
        set => shareButtonLogic = value;
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
        get => leaveRoomButtonLogic;
        set => leaveRoomButtonLogic = value;
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
    [Tooltip("The text mesh displaying the number of players in the current room.")]
    private TextMeshPro sharingPlayerCountText = null;

    /// <summary>
    /// The text mesh displaying the number of players in the current room.
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

    [Header("Dialog Parts")]

    [SerializeField]
    [Tooltip("The component that will host dialogs inside the app's menu.")]
    private AppDialog menuDialog = null;

    /// <summary>
    /// The component that will host dialogs inside the app's menu.
    /// </summary>
    public AppDialog MenuDialog
    {
        get => menuDialog;
        set => menuDialog = value;
    }

    [Header("Events")]

    [SerializeField]
    [Tooltip("Event raised when the menu state has changed.")]
    private UnityEvent stateChanged = new UnityEvent();

    /// <summary>
    /// Event raised when the menu state has changed.
    /// </summary>
    public UnityEvent StateChanged => stateChanged;
    #endregion Serialized Fields

    #region Public Properties
    /// <summary>
    /// Get the current menu state.
    /// </summary>
    public MenuState State { get; private set; }
    #endregion Public Properties

    #region MonoBehavior Functions
    /// <summary>
    /// Initialize all the buttons click listeners and various other menu states.
    /// </summary>
    private void Start()
    {
        AppServices.AppSettingsService.RegisterMainMenu(this);

        SetMenuState(MenuState.None);

        startSessionButtonLogic.OnClick.AddListener(delegate { StartAndConnectSession(); });
        stopSessionButtonLogic.OnClick.AddListener(delegate { StopSession(); });
        forgetSessionButtonLogic.OnClick.AddListener(delegate { ForgetSession(); });
        toolsButtonLogic.OnClick.AddListener(delegate { SetMenuState(MenuState.Tools); });
        modelsButtonLogic.OnClick.AddListener(delegate { SetMenuState(MenuState.Models); });
        sessionButtonLogic.OnClick.AddListener(delegate { SetMenuState(MenuState.Session); });
        shareButtonLogic.OnClick.AddListener(delegate { SetMenuState(MenuState.Share); });
        webInspectorButtonLogic.OnClick.AddListener(delegate { _remoteRenderingService?.PrimaryMachine?.Session.OpenWebInspector(); });
        createAndJoinRoomButtonLogic.OnClick.AddListener(delegate { CreateSharedRoom(); });
        leaveRoomButtonLogic.OnClick.AddListener(delegate { _sharingService?.LeaveRoom(); });
        menuDialog.OnOpened.AddListener(delegate { ClearAndLockState(); });
        menuDialog.OnClose.AddListener(delegate { UnlockState(); });
        networkWatcher.IsConnectedChanged.AddListener(delegate { UpdateNotificationsAndSessionButtons(); });

        _remoteRenderingService = AppServices.RemoteRendering;
        if (_remoteRenderingService != null)
        {
            _remoteRenderingService.StatusChanged += RemoteRendering_StatusChanged;
            UpdateNotificationsAndSessionButtons();
        }


        _sharingService = AppServices.SharingService;
        if (_sharingService != null)
        {
            _sharingService.CurrentRoomChanged += SharingService_CurrentRoomChanged;
            _sharingService.Connected += SharingService_ConnectionChanged;
            _sharingService.Connecting += SharingService_ConnectionChanged;
            _sharingService.Disconnected += SharingService_ConnectionChanged;
            _sharingService.StatusMessageChanged += SharingService_StatusMessageChanged;
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

        // Disable tools by default
        AppServices.PointerStateService.Mode = PointerMode.None;
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
            _sharingService.Connecting -= SharingService_ConnectionChanged;
            _sharingService.Disconnected -= SharingService_ConnectionChanged;
            _sharingService.StatusMessageChanged -= SharingService_StatusMessageChanged;
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
    /// Open a player panel for the given player id.
    /// </summary>
    public void OpenPlayerPanel(string playerId = null)
    {
        if (State != MenuState.Share)
        {
            SetMenuState(MenuState.Share);
        }

        var shareObject = shareButtonLogic.GetComponent<ToggleObject>().TargetObject;
        var subMenuController = shareObject.GetComponent<SubMenuController>();
        subMenuController.GoToMenu<ShareUserSubMenu>()?.SetPlayer(playerId);
    }

    /// <summary>
    /// Open an invite user panel
    /// </summary>
    public void OpenInviteUserPanel()
    {
        if (State != MenuState.Share)
        {
            SetMenuState(MenuState.Share);
        }

        var shareObject = shareButtonLogic.GetComponent<ToggleObject>().TargetObject;
        var subMenuController = shareObject.GetComponent<SubMenuController>();
        subMenuController.GoToMenu<ShareInviteUserSubMenu>();
    }

    /// <summary>
    /// Open the sharing room create panel
    /// </summary>
    public void OpenCreateRoomPanel()
    {
        CreateSharedRoom();
    }

    /// <summary>
    /// Open the sharing join room panel
    /// </summary>
    public void OpenJoinRoomPanel()
    {
        if (State != MenuState.Share)
        {
            SetMenuState(MenuState.Share);
        }

        var shareObject = shareButtonLogic.GetComponent<ToggleObject>().TargetObject;
        var subMenuController = shareObject.GetComponent<SubMenuController>();
        subMenuController.GoToMenu<ShareRoomListSubMenu>();
    }
    #endregion Public Functions

    #region Private Functions
    private void StartAndConnectSession()
    {
        RemoteRenderingStartHelper.StartWithPrompt(AppDialog.AppDialogLocation.Menu);
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
        UpdateNotificationsAndSessionButtons();
    }

    private void UpdateNotificationsAndSessionButtons()
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
                var connectError = _remoteRenderingService?.PrimaryMachine?.Session?.Connection?.ConnectionError;
                if (!connectError.HasValue)
                {
                    connectError = Microsoft.Azure.RemoteRendering.Result.Fail;
                }
                sessionStatusText.text = "Disconnected";
                notificationBar.SetNotification(-1f, 0.3f, new string[]{$"Connect error: {connectError}. Retrying.", $"Connect error: {connectError}. Retrying..", $"Connect error: {connectError}. Retrying..." }, AppNotificationType.Error);
                break;
            case RemoteRenderingServiceStatus.SessionReadyAndConnected:
                sessionStatusText.text = "Connected";
                notificationBar.SetScrollableNotification(3f, "Session connected.");
                showStartSessionButton = false;
                showWebInspectorButton = _remoteRenderingService.LoadedProfile.AuthType == AuthenticationType.AccountKey;
                if(!_sessionFirstConnect && !sessionButtonLogic.GetComponent<ToggleObject>().TargetObject.activeInHierarchy)
                {
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
        shouldHideNotification &= !TrySendingPresistentNotification();

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


        // Hide web inspector on non debug profile builds
        if (!showWebInspectorButton || _remoteRenderingService.LoadedProfile.AuthType == AuthenticationType.AccessToken)
        {
            webInspectorButtonLogic.gameObject.SetActive(false);
        }
        else
        {
            webInspectorButtonLogic.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// If the notification bar was hidden for some reason, show any available anchoring information.
    /// </summary>
    private void WhenNotificationBarHiddenShowAnchorInformation()
    {
        TrySendingPresistentNotification();
    }

    /// <summary>
    /// Try senting one of the presistent notification messages.
    /// </summary>
    /// <returns></returns>
    private bool TrySendingPresistentNotification()
    { 
        return TrySendingNotificationWithSharingServiceInformation() ||
            TrySendingNotificationWithAddressAndAnchorInformation() ||
            TrySendingNotificationAboutDisconnectedFromNetwork();
    }

    /// <summary>
    /// Try sending a notification with sharing service information.
    /// </summary>
    /// <returns></returns>
    private bool TrySendingNotificationWithSharingServiceInformation()
    {
        bool sentNotification = false;

        if (!string.IsNullOrEmpty(_sharingService?.StatusMessage))
        {
            string message = $"{_sharingService.StatusMessage}.";
            notificationBar.SetNotification(-1f, 0.3f, new string[] { message, message + ".", message + ".." });
            sentNotification = true;
        }

        return sentNotification;
    }

    /// <summary>
    /// Try sending a notification with anchor information. If a notification was sent, return true.
    /// </summary>
    private bool TrySendingNotificationWithAddressAndAnchorInformation()
    {
        bool sentNotification = false;

        // If still haven't sent a notification, force the anchor searches and creations to show in notification area.
        if (_anchoringService != null && !sentNotification)
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

    private bool TrySendingNotificationAboutDisconnectedFromNetwork()
    {
        bool sentNotification = false;

        if (networkWatcher != null && !networkWatcher.IsConnected)
        {
            notificationBar.SetScrollableNotification(duration: -1f, "Disconnected from the Internet.");
            sentNotification = true;
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

    private void SharingService_StatusMessageChanged(ISharingService arg1, string arg2)
    {
        UpdateNotificationsAndSessionButtons();
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
            if (joinedRoom)
            {
                notificationBar.SetScrollableNotification(3f, $"Joined {_sharingService.CurrentRoom.Name}.");
            }
        }

        if (sharingStatusText != null && _sharingService != null)
        {
            sharingStatusText.text = 
                _sharingService.IsConnected ? "Connected" : 
                _sharingService.IsConnecting ? "Connecting" :
                "Disconnected";
        }        
    }

    private void InvalidateSharingPlayerCount()
    {
        if (_updatePlayerCount == null  && isActiveAndEnabled)
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
        // If there is a value here. The menu state is locked
        if (_lockedState.HasValue)
        {
            return;
        }

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

        toggleLogic = shareButtonLogic.GetComponent<ToggleObject>();
        if(state == MenuState.Share && toggleLogic.TargetObject.activeInHierarchy) state = MenuState.None;
        toggleLogic.SetObjectActive(state == MenuState.Share);

        pointerToolIcon.HandMenuStateShow = state == MenuState.Tools || state == MenuState.None;
        
        switch (state)
        {
            case MenuState.Tools:
                break;
            case MenuState.Models:
                break;
            case MenuState.Session:
                break;
            case MenuState.Share:
                AppServices.SharingService.Login();
                break;
            case MenuState.None:
                break;
            case MenuState.Locked:
                break;
        }

        if (State != state)
        {
            State = state;
            stateChanged?.Invoke();
        }
    }

    private async void CreateSharedRoom()
    {
        if (_sharingService == null)
        {
            return;
        }

        AppDialog.AppDialogResult result = AppDialog.AppDialogResult.Ok;

        if (_sharingService.HasPrivateRooms)
        {
            result = await AppServices.AppNotificationService.ShowDialog(new DialogOptions()
            {
                Title = "Public or Private Room",
                Message = "Do you want to create a public or private room?\n\nIf you create a private room, you must invite users after the room is created.",
                Location = AppDialog.AppDialogLocation.Menu,
                Buttons = AppDialog.AppDialogButtons.All,
                OKLabel = "Public",
                NoLabel = "Private"
            });
        }

        if (result == AppDialog.AppDialogResult.Ok)
        {
            await _sharingService.CreateAndJoinRoom();
        }
        else if (result == AppDialog.AppDialogResult.No)
        {
            // passing in a non-null invite list makes the room private
            await _sharingService.CreateAndJoinRoom(new SharingServicePlayerData[0]);
        }
    }

    private void AnchoringService_ActiveSearchesCountChanged(IAnchoringService sender, AnchoringServiceSearchingArgs searchingArgs)
    {
        UpdateNotificationsAndSessionButtons();
    }

    private void AnchoringService_ActiveCreationsCountChanged(IAnchoringService sender, AnchoringServiceCreatingArgs creatingArgs)
    {
        UpdateNotificationsAndSessionButtons();
    }

    /// <summary>
    /// Save, then clear the current menu state.
    /// </summary>
    private void ClearAndLockState()
    {
        var previousState = State;
        SetMenuState(MenuState.Locked);
        _lockedState = previousState;
    }

    /// <summary>
    /// Restore the last saved state
    /// </summary>
    private void UnlockState()
    {
        if (_lockedState.HasValue)
        {
            var previousState = _lockedState.Value;
            _lockedState = null;
            SetMenuState(previousState);
        }
    }
    #endregion Private Functions

    #region Public Enums
    public enum MenuState
    {
        None = 0,
        Tools,
        Models,
        Session,
        Share,
        Locked,
    }
    #endregion Public Enums
}
