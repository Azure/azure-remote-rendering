// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// A class for sharing the state of the application's menu 
/// </summary>
public class SharableStateMenu : MonoBehaviour
{
    private PointerModeSynchronizationType _pointerModeSynchronization = PointerModeSynchronizationType.None;
    private int _playersClipping;
    private AppSettingsHideOperation _hideInterface = null;

    #region Serialized Fields
    [SerializeField]
    [FormerlySerializedAs("target")]
    [Tooltip("The sharing object used to send properties updates too. If null at Start(), the nearest parent target will be used.")]
    private SharingObjectBase sharingObject;

    /// <summary>
    /// The sharing object used to send properties updates too. If null at Start(), the nearest parent target will be used.
    /// </summary>
    public SharingObjectBase SharingObject
    {
        get => sharingObject;
        set => sharingObject = value;
    }

    [SerializeField]
    [Tooltip("The behavior that will do the work to change the scene's sky reflection cube map.")]
    private ChangeSkyReflection changeSkyReflection = null;

    /// <summary>
    /// The behavior that will do the work to change the scene's sky reflection cube map.
    /// </summary>
    public ChangeSkyReflection ChangeSkyReflection
    {
        get => changeSkyReflection;
        set => changeSkyReflection = value;
    }

    [SerializeField]
    [Tooltip("The menu that will be hidden during presentations.")]
    private Transform menuContainer = null;

    /// <summary>
    /// The menu that will be hidden during presentations
    /// </summary>
    public Transform MenuContainer
    {
        get => menuContainer;
        set => menuContainer = value;
    }

    [SerializeField]
    [Tooltip("The target that manages the clipping bar position.")]
    private SharableStateTransform clippingBar = null;

    /// <summary>
    /// The target that manages the clipping bar position.
    /// </summary>
    public SharableStateTransform ClippingBar
    {
        get => clippingBar;
        set => clippingBar = value;
    }

    [SerializeField]
    [Tooltip("The visual that will be shown when clipping.")]
    private GameObject clippingBarContainer = null;

    /// <summary>
    /// The visual that will be shown when clipping.
    /// </summary>
    public GameObject ClippingBarContainer
    {
        get => clippingBarContainer;
        set => clippingBarContainer = value;
    }
    #endregion Serialized Fields

    #region Public Properties
    /// <summary>
    /// Return if this client is the presenter
    /// </summary>
    public bool IsPresenter
    {
        get
        {
            if (AppServices.SharingService.LocalPlayer == null)
            {
                return false;
            }

            return PresenterId == AppServices.SharingService.LocalPlayer.Data.PlayerId;
        }
    }

    /// <summary>
    /// Get the rooms current presenter
    /// </summary>
    public string PresenterId
    {
        get
        {
            if (sharingObject == null)
            {
                return AppServices.SharingService.InvalidPlayerId;
            }

            string id;
            if (!sharingObject.TryGetProperty(SharableStrings.PresenterId, out id))
            {
                id = AppServices.SharingService.InvalidPlayerId;
            }

            return id;
        }
    }

    /// <summary>
    /// Get if current player is marked has clipping
    /// </summary>
    public bool IsLocalPlayerClipping
    {
        get
        {
            if (AppServices.SharingService.LocalPlayer == null)
            {
                return false;
            }

            bool clipping;
            return AppServices.SharingService.LocalPlayer.TryGetProperty(SharableStrings.PlayerIsClipping, out clipping) && clipping;
        }
    }


    /// <summary>
    /// Get if the room's menu is visible.
    /// </summary>
    public bool IsRoomMenuVisible
    {
        get
        {
            if (sharingObject == null)
            {
                return false;
            }

            bool menu;
            return sharingObject.TryGetProperty(SharableStrings.MenuIsVisible, out menu) && menu;
        }
    }

    /// <summary>
    /// Get if the room's tools a being shared
    /// </summary>
    public bool IsRoomSharedTools
    {
        get
        {
            if (sharingObject == null)
            {
                return false;
            }

            bool sharedTools;
            if (!sharingObject.TryGetProperty(SharableStrings.MenuIsSharingTools, out sharedTools))
            {
                sharedTools = true;
            }
            return sharedTools;
        }
    }

    /// <summary>
    /// Get the sharing room's current pointer mode.
    /// </summary>
    public PointerMode CurrentPointerMode
    {
        get
        {
            if (sharingObject == null)
            {
                return PointerMode.None;
            }

            PointerMode toolMode;
            if (!sharingObject.TryGetProperty(SharableStrings.MenuToolMode, out toolMode))
            {
                toolMode = PointerMode.None;
            }
            return toolMode;
        }
    }

    /// <summary>
    /// Can the signed in user start presenting.
    /// </summary>
    public bool CanPresent => sharingObject != null && GetMenuVisibility() && AppServices.SharingService.LocalPlayer != null;

    /// <summary>
    /// Can the signed in user start collaborating.
    /// </summary>
    public bool CanCollaborate => sharingObject != null && GetMenuVisibility() && AppServices.SharingService.LocalPlayer != null;

    /// <summary>
    /// Can the signed in user stop presenting or collaborating.
    /// </summary>
    public bool CanStopPresenting => sharingObject != null && IsPresenter;
    #endregion Public Properties

    #region MonoBehaviour Functions
    private void Start()
    {
        if (sharingObject == null)
        {
            sharingObject = GetComponent<SharingObjectBase>();
        }

        if (changeSkyReflection == null)
        {
            changeSkyReflection = GetComponent<ChangeSkyReflection>();
        }

        if (sharingObject != null)
        {
            sharingObject.PropertyChanged += TargetPropertyChanged;
        }


        if (changeSkyReflection != null)
        {
            changeSkyReflection.SkyReflectionApplying.AddListener(SendSkyReflectionCubeMap);
        }

        AppServices.SharingService.PlayerRemoved += PlayerRemoved;
        AppServices.SharingService.PlayerPropertyChanged += PlayerPropertyChanged;
        AppServices.SharingService.LocalPlayerChanged += LocalPlayerChanged;
        AppServices.PointerStateService.ModeChanged += SendPointerMode;
    }

    private void OnDestroy()
    {
        if (sharingObject != null)
        {
            sharingObject.PropertyChanged -= TargetPropertyChanged;
            sharingObject = null;
        }

        if (changeSkyReflection != null)
        {
            changeSkyReflection.SkyReflectionApplying.RemoveListener(SendSkyReflectionCubeMap);
            changeSkyReflection = null;
        }

        AppServices.PointerStateService.ModeChanged -= SendPointerMode;
    }
    #endregion MonoBehaviour Functions

    #region Public Functions
    /// <summary>
    /// Calling this will put the menu into presenting mode. This means all other players won't see the menu, and can't interact with the model.
    /// </summary>
    public async void StartPresenting()
    {
        // Can only start presenting if menu is visible locally
        if (CanPresent)
        {
            var dialogResult = await AppServices.AppNotificationService.ShowDialog(new DialogOptions()
            {
                Title = "Start Presenting?",
                Message = "Would you like to start presenting?\n\nPresenting will hide the menu for all other users within your sharing session. Also, other users won't be able interact with the models.",
                Buttons = AppDialog.AppDialogButtons.Ok | AppDialog.AppDialogButtons.No,
                OKLabel = "Yes",
                NoLabel = "No"
            });

            if (dialogResult == AppDialog.AppDialogResult.Ok)
            {
                if (CanPresent)
                {
                    sharingObject.SetProperties(
                        SharableStrings.MenuIsVisible, false,
                        SharableStrings.MenuIsSharingTools, false,
                        SharableStrings.PresenterId, AppServices.SharingService.LocalPlayer.Data.PlayerId);
                }
                else
                {
                    AppServices.AppNotificationService.RaiseNotification("Unable to start presenting.", AppNotificationType.Warning);
                }
            }
        }
    }

    /// <summary>
    /// Calling this will put the menu into presenting mode with collaborating. This means all other players won't see the menu, but the pointer mode is shared across all players.
    /// This allows all players to interact with the model.
    /// </summary>
    public async void StartCollaborating()
    {
        // Can only start presenting if menu is visible locally
        if (CanCollaborate)
        {
            var dialogResult = await AppServices.AppNotificationService.ShowDialog(new DialogOptions()
            {
                Title = "Start Collaborating?",
                Message = "Would you like to start collaborating?\n\nCollaborating will hide the menu for all other users within your sharing session. However, other users can still interact with the models, using your selected tool.",
                Buttons = AppDialog.AppDialogButtons.Ok | AppDialog.AppDialogButtons.No,
                OKLabel = "Yes",
                NoLabel = "No"
            });

            if (dialogResult == AppDialog.AppDialogResult.Ok)
            {
                if (CanCollaborate)
                {
                    sharingObject.SetProperties(
                        SharableStrings.MenuIsVisible, false,
                        SharableStrings.MenuIsSharingTools, true,
                        SharableStrings.PresenterId, AppServices.SharingService.LocalPlayer.Data.PlayerId);
                }
                else
                {
                    AppServices.AppNotificationService.RaiseNotification("Unable to start collaborating.", AppNotificationType.Warning);
                }
            }
        }
    }

    /// <summary>
    /// Stop presenting or collaborating.
    /// </summary>
    public async void StopPresenting()
    {
        if (CanStopPresenting)
        {
            var dialogResult = await AppServices.AppNotificationService.ShowDialog(new DialogOptions()
            {
                Title = "Stop Presenting?",
                Message = "Would you like to stop presenting or collaborating?\n\nStopping will show the menu for all other users within your sharing session.",
                Buttons = AppDialog.AppDialogButtons.Ok | AppDialog.AppDialogButtons.No,
                OKLabel = "Yes",
                NoLabel = "No"
            });

            if (dialogResult == AppDialog.AppDialogResult.Ok)
            {
                if (CanStopPresenting)
                {
                    ForceStopPresenting();
                    AppServices.AppNotificationService.RaiseNotification("Stopped presenting or collaborating.", AppNotificationType.Info);
                }
                else
                {
                    AppServices.AppNotificationService.RaiseNotification("Unable to stop presenting or collaborating.", AppNotificationType.Warning);
                }
            }
        }
    }
    #endregion Public Functions

    #region Private Functions
    private void TargetPropertyChanged(ISharingServiceObject sender, string property, object input)
    {
        switch (input)
        {
            case int value when property == SharableStrings.MenuToolMode:
                SetPointerMode((PointerMode)value);
                break;

            case bool value when property == SharableStrings.MenuIsVisible:
                SetMenuVisibility(visible: value);
                break;

            case bool value when property == SharableStrings.MenuIsSharingTools:
                SetToolsShared(shared: value);
                break;

            case string value when property == SharableStrings.SkyCubeMap:
                ReceiveSkyReflectionCubeMap(value);
                break;

            case string value when property == SharableStrings.PresenterId:
                ReceivePresenterId(value);
                break;
        }
    }

    private void SendPointerMode(object sender, IPointerModeChangedEventData args)
    {
        // can only change shared state if the menu is visible
        if (sharingObject != null && GetMenuVisibility())
        {
            sharingObject.SetProperty(SharableStrings.MenuToolMode, (int)args.NewValue);

            if (args.NewValue == PointerMode.ClipBar)
            {
                StartedClipping();
            }
            else if (args.OldValue == PointerMode.ClipBar)
            {
                StoppedClipping();
            }
        }
    }

    private void SendSkyReflectionCubeMap(string newUrl)
    {
        if (sharingObject == null)
        {
            return;
        }

        // convert empty string to null
        newUrl = string.IsNullOrEmpty(newUrl) ? null : newUrl;

        // get old url, and only send property updates if there's an actual change
        string oldUrl;
        sharingObject.TryGetProperty(SharableStrings.SkyCubeMap, out oldUrl);

        if (newUrl != oldUrl)
        {
            sharingObject.SetProperty(SharableStrings.SkyCubeMap, newUrl);
        }
    }

    /// <summary>
    /// Handle receiving server changes for the sky reflection map.
    /// </summary>
    /// <param name="url"></param>
    private void ReceiveSkyReflectionCubeMap(string url)
    {
        if (changeSkyReflection != null)
        {
            changeSkyReflection.CubeMapUrl = url;
        }
    }

    /// <summary>
    /// Handle receiving server changes for which player is presenting
    /// </summary>
    private void ReceivePresenterId(string playerId)
    {
        if (sharingObject == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(playerId))
        {
            if (IsRoomMenuVisible == false || IsRoomSharedTools == true)
            {
                sharingObject.SetProperties(
                    SharableStrings.MenuIsVisible, true,
                    SharableStrings.MenuIsSharingTools, false,
                    SharableStrings.PresenterId, AppServices.SharingService.InvalidPlayerId);
            }
        }
        else
        {
            EnsureRenderingSessionForPresentation();
        }
    }

    /// <summary>
    /// Ensure there's a remote rendering session for the presentation.
    /// </summary>
    private async void EnsureRenderingSessionForPresentation()
    {
        try
        {
            await AppServices.RemoteRendering.AutoConnect();
        }
        catch (Exception ex)
        {
            var msg = $"Failed to create a remote rendering session. Reason: {ex.Message}";
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, msg);
            AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
        }
    }

    /// <summary>
    /// Force presenting to stop
    /// </summary>
    private void ForceStopPresenting()
    {
        sharingObject.SetProperty(SharableStrings.PresenterId, AppServices.SharingService.InvalidPlayerId);
    }

    /// <summary>
    /// Notify the other player that we started clipping
    /// </summary>
    private void StartedClipping()
    {
        if (AppServices.SharingService.LocalPlayer != null && !IsLocalPlayerClipping)
        {
            AppServices.SharingService.LocalPlayer.SetProperty(SharableStrings.PlayerIsClipping, true);
        }
    }

    /// <summary>
    /// Notify the other player that we stopped clipping
    /// </summary>
    private void StoppedClipping()
    {
        if (AppServices.SharingService.LocalPlayer != null && IsLocalPlayerClipping)
        {
            AppServices.SharingService.LocalPlayer.SetProperty(SharableStrings.PlayerIsClipping, false);
        }
    }

    /// <summary>
    /// Get the menu visibility
    /// </summary>
    private bool GetMenuVisibility()
    {
        return menuContainer != null && menuContainer.gameObject.activeInHierarchy;
    }

    /// <summary>
    /// If not the presenter, change the menu's visibility
    /// </summary>
    private void SetMenuVisibility(bool visible)
    {
        if (!IsPresenter)
        {
            if (!visible && AppServices.PointerStateService != null)
            {
                AppServices.PointerStateService.Mode = PointerMode.None;
            }

            if (visible)
            {
                _hideInterface?.Cancel();
                _hideInterface = null;
            }
            else if (_hideInterface == null)
            {
                _hideInterface = AppServices.AppSettingsService.HideInterface();
            }
        }
    }

    /// <summary>
    /// If not the presenter, change whether tools are shared
    /// </summary>
    private void SetToolsShared(bool shared)
    {
        if (!IsPresenter && menuContainer != null)
        {
            _pointerModeSynchronization = shared ? PointerModeSynchronizationType.All : PointerModeSynchronizationType.None;
            SetPointerMode(CurrentPointerMode);
        }
    }

    /// <summary>
    /// If not the present, change the pointer mode
    /// </summary>
    private void SetPointerMode(PointerMode mode)
    {
        // avoid setting multiple times, since resetting the mode can cause things like the clipping plane to reset.
        // also only consume clipping modes.
        if (!IsPresenter && 
            AppServices.PointerStateService != null &&
            mode != AppServices.PointerStateService.Mode &&
            _pointerModeSynchronization == PointerModeSynchronizationType.All)
        {
            if (mode == PointerMode.ClipBar)
            {
                SetClipBarVisibility(visible: true, viaModeChange: true);
            }
            else
            {
                AppServices.PointerStateService.Mode = mode;
            }
        }
    }

    /// <summary>
    /// Set the clip bar's visibility. The clip bar can be shown by either changing the global pointer mode, or directly
    /// setting the game object's active state. If changing the global pointer mode, the user's active tool will be 
    /// changed, which may be unexpected by the user. 
    /// </summary>
    private void SetClipBarVisibility(bool visible, bool viaModeChange)
    {        

        if (viaModeChange && visible && AppServices.PointerStateService != null)
        {
            // Setting this mode will show the clip bar, and also change the user's current pointer tool.
            AppServices.PointerStateService.Mode = PointerMode.ClipBar;
        }
        else if (clippingBarContainer != null)
        {
            clippingBarContainer.SetActive(visible);
        }
    }

    /// <summary>
    /// Handle players being removed from the server. If the presenter left, force the presentation to end. If player 
    /// was clipping, force the old player to stop clipping.
    /// </summary>
    private void PlayerRemoved(ISharingService service, ISharingServicePlayer player)
    {
        if (PresenterId == player.Data.PlayerId)
        {
            ForceStopPresenting();
        }

        bool wasClipping;
        if (player.TryGetProperty(SharableStrings.PlayerIsClipping, out wasClipping) && wasClipping)
        {
            PlayerPropertyChanged(player, SharableStrings.PlayerIsClipping, false);
        }
    }

    /// <summary>
    /// Inform other players if the local user is actively clipping
    /// </summary>
    private void LocalPlayerChanged(ISharingService service, ISharingServicePlayer player)
    {
        _playersClipping = 0;
        SetMenuVisibility(visible: true);
        SetToolsShared(shared: false);

        if (AppServices.PointerStateService != null &&
            AppServices.PointerStateService.Mode != PointerMode.ClipBar)
        {
            // make sure the clipper is no longer visible
            SetClipBarVisibility(visible: false, viaModeChange: false);
        }
    }    

    /// <summary>
    /// Handling receiving player property changes from the server.
    /// </summary>
    private void PlayerPropertyChanged(ISharingServicePlayer player, string property, object value)
    {    
        if (property == SharableStrings.PlayerIsClipping && value is bool)
        {
            _playersClipping = Math.Max(0, _playersClipping + ((bool)value ? 1 : -1));
            SetClipBarVisibility(visible: _playersClipping > 0, viaModeChange: false);
        }
    }
    #endregion Private Functions

    #region Public Region
    /// <summary>
    /// How should pointer modes be synchronized across players.
    /// </summary>
    [Serializable]
    private enum PointerModeSynchronizationType
    {
        [Tooltip("Don't synchronize anything.")]
        None,

        [Tooltip("Synchronize all pointer mode changes.")]
        All
    }
    #endregion Public Region
}

