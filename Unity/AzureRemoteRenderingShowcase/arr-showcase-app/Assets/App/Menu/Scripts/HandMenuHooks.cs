// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.UI;
using TMPro;
using UnityEngine;

/// <summary>
/// Hooks up HandMenu UI to make the appropriate application calls.
/// </summary>
public class HandMenuHooks : MonoBehaviour
{
    private IRemoteRenderingService _remoteRenderingService;

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

    /// <summary>
    /// The button to be used to open a new web browser to the current Azure Remote Rendering session's 'Inspector' page.
    /// </summary>
    public Interactable WebInspectorButtonLogic
    {
        get => webInspectorButtonLogic;
        set => webInspectorButtonLogic = value;
    }

    [SerializeField]
    [Tooltip("The text mesh displaying the current session status (Starting, Ready, Stopped, Expired, Failure, ect.).")]
    private TextMeshPro sessionStatusText = null;

    /// <summary>
    /// The text mesh displaying the current session's status (Starting, Ready, Stopped, Expired, Failure, ect.).
    /// </summary>
    public TextMeshPro SessionStatusText
    {
        get => sessionStatusText;
        set => sessionStatusText = value;
    }

    [SerializeField]
    [Tooltip("The text mesh displaying the current session's region (West US, West Eurpore, ect.).")]
    private TextMeshPro sessionRegionText = null;

    /// <summary>
    /// The text mesh displaying the current session's region (West US, West Eurpore, ect.).
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

    [Header("Status Parts")]

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

    [SerializeField]
    [Tooltip("The text mesh displaying various application notifications.")]
    private NotificationBarController notificationBar = null;

    /// <summary>
    /// he text mesh displaying various application notifications.
    /// </summary>
    public NotificationBarController NotificationBar
    {
        get => notificationBar;
        set => notificationBar = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Functions
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
        toolsButtonLogic.OnClick.AddListener(delegate { SetMenuState(MenuState.Tools); });
        modelsButtonLogic.OnClick.AddListener(delegate { SetMenuState(MenuState.Models); });
        sessionButtonLogic.OnClick.AddListener(delegate { SetMenuState(MenuState.Session); });
        statsButtonLogic.OnClick.AddListener(delegate { SetMenuState(MenuState.Stats); });
        webInspectorButtonLogic?.OnClick.AddListener(delegate { _remoteRenderingService.PrimaryMachine?.Session.OpenWebInspector(); });

        _remoteRenderingService = AppServices.RemoteRendering;
        if (_remoteRenderingService != null)
        {
            _remoteRenderingService.StatusChanged += RemoteRendering_StatusChanged;
            UpdateNotifcationsAndSessionButtons();
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
    /// Set the pointer to a praticular tool.
    /// </summary>
    /// <param name="pointerMode"></param>
    public void SetPointerMode(PointerMode pointerMode)
    {
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
        switch (_remoteRenderingService.Status)
        {
            case RemoteRenderingServiceStatus.SessionReadyAndDisconnected:
                sessionStatusText.text = "Disconnected";
                notificationBar.SetNotification(3f, "Session disconnected.");
                break;
            case RemoteRenderingServiceStatus.SessionReadyAndConnectionError:
                sessionStatusText.text = "Disconnected";
                notificationBar.SetNotification(-1f, 0.3f, new string[]{"Connection failed. Retrying to connect.", "Connection failed. Retrying to connect..", "Connection failed. Retrying to connect..."});
                break;
            case RemoteRenderingServiceStatus.SessionReadyAndConnected:
                sessionStatusText.text = "Connected";
                notificationBar.SetNotification(3f, "Session connected.");
                showStartSessionButton = false;
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
                notificationBar.SetNotification(-1f, 0.3f, new string[] { "Starting the session.", "Starting the session..", "Starting the session..." });
                showStartSessionButton = false;
                break;
            case RemoteRenderingServiceStatus.SessionError:
                sessionStatusText.text = "Failure";
                notificationBar.SetNotification(3f, "Session failed to start.");
                break;
            case RemoteRenderingServiceStatus.SessionExpired:
                sessionStatusText.text = "Expired";
                notificationBar.SetNotification(3f, "Session expired.");
                break;
            case RemoteRenderingServiceStatus.SessionStopped:
                sessionStatusText.text = "Stopped";
                notificationBar.SetNotification(3f, "Session stopped.");
                break;
            case RemoteRenderingServiceStatus.NoSession:
            case RemoteRenderingServiceStatus.Unknown:
                sessionStatusText.text = "No Session";
                notificationBar.HideNotification();
                break;
            default:
                Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, $"Unknown remoting status '{_remoteRenderingService.Status}'");
                sessionStatusText.text = string.Empty;
                break;
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

        startSessionButtonLogic.gameObject.SetActive(showStartSessionButton);
        stopSessionButtonLogic.gameObject.SetActive(!showStartSessionButton);
    }

    private void SetMenuState(MenuState state)
    {
        // sets toggle state of menu objects.
        // if menu is currently open, hitting button again will turn it off.
        ToggleObject toggleLogic = toolsButtonLogic.GetComponent<ToggleObject>();
        toggleLogic.SetObjectActive((state == MenuState.Tools) && !toggleLogic.TargetObject.activeInHierarchy);

        toggleLogic = modelsButtonLogic.GetComponent<ToggleObject>();
        toggleLogic.SetObjectActive((state == MenuState.Models) && !toggleLogic.TargetObject.activeInHierarchy);

        toggleLogic = sessionButtonLogic.GetComponent<ToggleObject>();
        toggleLogic.SetObjectActive((state == MenuState.Session) && !toggleLogic.TargetObject.activeInHierarchy);

        toggleLogic = statsButtonLogic.GetComponent<ToggleObject>();
        toggleLogic.SetObjectActive((state == MenuState.Stats) && !toggleLogic.TargetObject.activeInHierarchy);
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
