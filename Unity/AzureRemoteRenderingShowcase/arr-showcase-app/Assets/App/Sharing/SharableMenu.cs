// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using System;
using UnityEngine;

/// <summary>
/// A class for sharing the state of the application's menu 
/// </summary>
public class SharableMenu : MonoBehaviour
{
    private PointerModeSynchronizationType _pointerModeSynchronization = PointerModeSynchronizationType.None;
    private int _playersClipping;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The sharing target used to send properties updates too. If null at Start(), the nearest parent target will be used.")]
    private SharingTarget target;

    /// <summary>
    /// The sharing target used to send properties updates too. If null at Start(), the nearest parent target will be used.
    /// </summary>
    public SharingTarget Target
    {
        get => target;
        set => target = value;
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
    private SharableObjectTransform clippingBar = null;

    /// <summary>
    /// The target that manages the clipping bar position.
    /// </summary>
    public SharableObjectTransform ClippingBar
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

            return PresenterId == AppServices.SharingService.LocalPlayer.PlayerId;
        }
    }

    /// <summary>
    /// Get the rooms current presenter
    /// </summary>
    public int PresenterId
    {
        get
        {
            if (target == null)
            {
                return -1;
            }

            int id;
            if (!target.TryGetProperty(SharableStrings.PresenterId, out id))
            {
                id = -1;
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
            if (target == null)
            {
                return false;
            }

            bool menu;
            return target.TryGetProperty(SharableStrings.MenuIsVisible, out menu) && menu;
        }
    }

    /// <summary>
    /// Get if the room's tools a being shared
    /// </summary>
    public bool IsRoomSharedTools
    {
        get
        {
            if (target == null)
            {
                return false;
            }

            bool sharedTools;
            if (!target.TryGetProperty(SharableStrings.MenuIsSharingTools, out sharedTools))
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
            if (target == null)
            {
                return PointerMode.None;
            }

            PointerMode toolMode;
            if (!target.TryGetProperty(SharableStrings.MenuToolMode, out toolMode))
            {
                toolMode = PointerMode.None;
            }
            return toolMode;
        }
    }
    #endregion Public Properties

    #region MonoBehaviour Functions
    private void Start()
    {
        if (target == null)
        {
            target = GetComponent<SharingTarget>();
        }

        if (changeSkyReflection == null)
        {
            changeSkyReflection = GetComponent<ChangeSkyReflection>();
        }

        if (target != null)
        {
            target.PropertyChanged += TargetPropertyChanged;
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
        if (target != null)
        {
            target.PropertyChanged -= TargetPropertyChanged;
            target = null;
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
    public void StartPresenting()
    {
        // Can only start presenting if menu is visible locally
        if (target != null && GetMenuVisibility() && AppServices.SharingService.LocalPlayer != null)
        {
            target.SetProperties(
                SharableStrings.MenuIsVisible, false,
                SharableStrings.MenuIsSharingTools, false,
                SharableStrings.PresenterId, AppServices.SharingService.LocalPlayer.PlayerId);
        }
    }

    /// <summary>
    /// Calling this will put the menu into presenting mode with collaborating. This means all other players won't see the menu, but the pointer mode is shared across all players.
    /// This allows all players to interact with the model.
    /// </summary>
    public void StartCollaborating()
    {
        // Can only start presenting if menu is visible locally
        if (target != null && GetMenuVisibility() && AppServices.SharingService.LocalPlayer != null)
        {
            target.SetProperties(
                SharableStrings.MenuIsVisible, false,
                SharableStrings.MenuIsSharingTools, true,
                SharableStrings.PresenterId, AppServices.SharingService.LocalPlayer.PlayerId);
        }
    }

    public void StopPresenting()
    {
        if (target != null && IsPresenter)
        {
            ForceStopPresenting();
        }
    }
    #endregion Public Functions

    #region Private Functions
    private void TargetPropertyChanged(string property, object input)
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

            case int value when property == SharableStrings.PresenterId:
                ReceivePresenterId(value);
                break;
        }
    }

    private void SendPointerMode(object sender, IPointerModeChangedEventData args)
    {
        // can only change shared state if the menu is visible
        if (target != null && GetMenuVisibility())
        {
            target.SetProperty(SharableStrings.MenuToolMode, (int)args.NewValue);

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

    private void SendSkyReflectionCubeMap(string url)
    {
        target?.SetProperty(SharableStrings.SkyCubeMap, string.IsNullOrEmpty(url) ? null : url);
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
    private void ReceivePresenterId(int playerId)
    {
        if (target == null)
        {
            return;
        }

        if (playerId == -1)
        {
            if (IsRoomMenuVisible == false || IsRoomSharedTools == true)
            {
                target.SetProperties(
                    SharableStrings.MenuIsVisible, true,
                    SharableStrings.MenuIsSharingTools, false,
                    SharableStrings.PresenterId, -1);
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
        target.SetProperties(SharableStrings.PresenterId, -1);
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
            if (!visible)
            {
                AppServices.PointerStateService.Mode = PointerMode.None;
            }

            if (menuContainer != null)
            {
                menuContainer.gameObject.SetActive(visible);
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
        if (!IsPresenter && mode != AppServices.PointerStateService.Mode && _pointerModeSynchronization == PointerModeSynchronizationType.All)
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
        // Showing the clip will change its position. We want the original transform to be used for the clip bar, so save
        // the original transform before showing
        SharingServiceTransform? originalTransform = null;
        if (visible)
        {
            originalTransform = clippingBar?.GetTransform();
        }

        if (viaModeChange && visible)
        {
            // Setting this mode will show the clip bar, and also change the user's current pointer tool.
            AppServices.PointerStateService.Mode = PointerMode.ClipBar;
        }
        else if (clippingBarContainer != null)
        {
            clippingBarContainer.SetActive(visible);
        }

        // Re-apply the original transform, since showing the clip bar moves the clip bar to the menu's position.
        if (originalTransform != null)
        {
            clippingBar?.SetTransform(originalTransform.Value);
        }
    }

    /// <summary>
    /// Handle players being removed from the server. If the presenter left, force the presentation to end. If player 
    /// was clipping, force the old player to stop clipping.
    /// </summary>
    private void PlayerRemoved(ISharingService service, ISharingServicePlayer player)
    {
        if (PresenterId == player.PlayerId)
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

        if (AppServices.PointerStateService.Mode != PointerMode.ClipBar)
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

