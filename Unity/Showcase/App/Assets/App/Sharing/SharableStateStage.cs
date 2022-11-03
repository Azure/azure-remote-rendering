// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication;
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// A class for sharing the state of main application stage.
/// </summary>
public class SharableStateStage : MonoBehaviour
{
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
    [Tooltip("The stage that loads and manages new models.")]
    private RemoteObjectStage stage;

    /// <summary>
    /// The stage that loads and manages new models.
    /// </summary>
    public RemoteObjectStage Stage
    {
        get => stage;
        set => stage = value;
    }
    #endregion Serialized Fields

    #region Public Properties
    #endregion Private Properties

    #region MonoBehaviour Functions
    private void Start()
    {
        if (sharingObject == null)
        {
            sharingObject = GetComponent<SharingObjectBase>();
        }

        if (stage == null)
        {
            stage = GetComponent<RemoteObjectStage>();
        }

        if (stage != null)
        {
            stage.StageVisualVisibilityChanged.AddListener(SendStageVisible);
        }

        if (sharingObject != null)
        {
            sharingObject.PropertyChanged += TargetPropertyChanged;
            sharingObject.MessageReceived += TargetCommandMessageReceived;
            sharingObject.ConnectionChanged += TargetConnectionChanged;
        }
    }

    private void OnDestroy()
    {
        if (stage != null)
        {
            stage.StageVisualVisibilityChanged.RemoveListener(SendStageVisible);
            stage = null;
        }

        if (sharingObject != null)
        {
            sharingObject.PropertyChanged -= TargetPropertyChanged;
            sharingObject.MessageReceived -= TargetCommandMessageReceived;
            sharingObject.ConnectionChanged -= TargetConnectionChanged;
            sharingObject = null;
        }
    }
    #endregion MonoBehaviour Functions

    #region Public Functions
    /// <summary>
    /// Notify all other players that they should move and place their stages.
    /// </summary>
    public void AllPlayersMoveStage()
    {
        sharingObject?.SendCommandMessage(SharableStrings.CommandPlayersMoveStage);
    }
    #endregion Public Functions

    #region Private Functions
    /// <summary>
    /// Handle property changes received from the server.
    /// </summary>
    private void TargetPropertyChanged(ISharingServiceObject sender, string property, object input)
    {
        switch (input)
        {

            case bool value when property == SharableStrings.StageIsVisible && stage != null:
                stage.IsStageVisible = value;
                break;
        }
    }

    /// <summary>
    /// Handle receiving a new command message from the sharing target.
    /// </summary>
    private void TargetCommandMessageReceived(ISharingServiceObject sender, ISharingServiceMessage message)
    {
        if (message.Command == SharableStrings.CommandPlayersMoveStage && stage != null)
        {
            stage.MoveStage();
        }
    }

    /// <summary>
    /// Notify other users that the stage visibility has changed.
    /// </summary>
    private void SendStageVisible(bool visible)
    {
        if (sharingObject == null)
        {
            return;
        }

        bool oldVisible;
        if (!sharingObject.TryGetProperty(SharableStrings.StageIsVisible, out oldVisible) || oldVisible != visible)
        {
            sharingObject.SetProperty(SharableStrings.StageIsVisible, visible);
        }
    }

    /// <summary>
    /// Force the stage to show if this is the first user connected
    /// </summary>
    private void TargetConnectionChanged(bool connected)
    {
        if (connected)
        {
            ShowStageIfFirstUser();
        }
        else
        {
            ClearStageContent();
        }
    }

    /// <summary>
    /// Show the stage if this is the first signed in user.
    /// </summary>
    private void ShowStageIfFirstUser()
    {
        if (AppServices.SharingService.Players?.Count <= 1 && Stage != null)
        {
            Stage.IsStageVisible = true;
            TryPlacingStageIfNotPlaced();
        }
    }

    /// <summary>
    /// Ask the user to place the stage, if not placed.
    /// </summary>
    private async void TryPlacingStageIfNotPlaced()
    {
        if (Stage == null || !AnchorSupport.IsNativeEnabled)
        {
            return;
        }

        AppDialog.AppDialogResult dialogPlaceStage = AppDialog.AppDialogResult.No;

        // Wait to let anchor id load
        await Task.Delay(TimeSpan.FromSeconds(value: 1));

        // If the sharing service has an anchor address, then assume it's using a known stage location.
        if (AppServices.SharingService.PrimaryAddress == null ||
            AppServices.SharingService.PrimaryAddress.Type != SharingServiceAddressType.Anchor)
        {
            dialogPlaceStage = await AppServices.AppNotificationService.ShowDialog(new DialogOptions()
            {
                Title = "Place Stage?",
                Message = "Users will only see your avatar correctly if you place the virtual stage on the floor.\n\nWould you like to place your stage now?",
                OKLabel = "Yes",
                NoLabel = "No",
                Buttons = AppDialog.AppDialogButtons.Ok | AppDialog.AppDialogButtons.No
            });
        }

        if (dialogPlaceStage == AppDialog.AppDialogResult.Ok)
        {
            Stage.MoveStage();
        }
    }


    /// <summary>
    /// Clear all the content under the stage
    /// </summary>
    private void ClearStageContent()
    {
        if (Stage != null)
        {
            Stage.ClearContainer(force: true);
        }
    }
    #endregion Private Functions
}
