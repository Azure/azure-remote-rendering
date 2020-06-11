// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// A class for sharing the state of main application stage.
/// </summary>
public class SharableStage : MonoBehaviour
{
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
    /// <summary>
    /// Get the rooms current staged object "sharing id".
    /// </summary>
    public string StagedObjectSharingId
    {
        get
        {
            if (target == null)
            {
                return null;
            }

            string id;
            target.TryGetProperty(SharableStrings.StageObjectId, out id);
            return id;
        }
    }
    #endregion Private Properties

    #region MonoBehaviour Functions
    private void Start()
    {
        if (target == null)
        {
            target = GetComponent<SharingTarget>();
        }

        if (stage == null)
        {
            stage = GetComponent<RemoteObjectStage>();
        }

        if (stage != null)
        {
            stage.StagedObjectChanged.AddListener(SendStagedObject);
            stage.StageVisualVisibilityChanged.AddListener(SendStageVisible);
        }

        if (target != null)
        {
            target.PropertyChanged += TargetPropertyChanged;
            target.MessageReceived += TargetCommandMessageReceived;
        }

        AppServices.SharingService.TargetAdded += SharingTargetAdded;
    }

    private void OnDestroy()
    {
        if (stage != null)
        {
            stage.StagedObjectChanged.RemoveListener(SendStagedObject);
            stage.StageVisualVisibilityChanged.RemoveListener(SendStageVisible);
            stage = null;
        }

        if (target != null)
        {
            target.PropertyChanged -= TargetPropertyChanged;
            target = null;
        }

        AppServices.SharingService.TargetAdded -= SharingTargetAdded;
    }
    #endregion MonoBehaviour Functions

    #region Public Functions
    /// <summary>
    /// Notify all other players that they should move and place their stages.
    /// </summary>
    public void AllPlayersMoveStage()
    {
        target?.SendCommandMessage(SharableStrings.CommandPlayersMoveStage);
    }
    #endregion Public Functions

    #region Private Functions
    /// <summary>
    /// Invoked when a new sharing target has been added to the server. If a target is a remote "object", the stage 
    /// will be notified and it'll start loading a new model.
    /// </summary>
    private void SharingTargetAdded(ISharingService sender, ISharingServiceTarget target)
    {
        if (target.Type == SharingServiceTargetType.Dynamic)
        {
            ReceiveLoadedObject(target);
        }
    }

    /// <summary>
    /// Handle property changes received from the server.
    /// </summary>
    private void TargetPropertyChanged(string property, object input)
    {
        switch (input)
        {
            case string value when property == SharableStrings.StageObjectId:
                ReceiveStagedObject(value);
                break;

            case bool value when property == SharableStrings.StageIsVisible && stage != null:
                stage.IsStageVisible = value;
                break;
        }
    }

    /// <summary>
    /// Handle receiving a new command message from the sharing target.
    /// </summary>
    private void TargetCommandMessageReceived(ISharingServiceMessage message)
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
        target?.SetProperty(SharableStrings.StageIsVisible, visible);
    }

    /// <summary>
    /// Notify other users that a new model is being placed on the center stage.
    /// </summary>
    private void SendStagedObject(RemoteObject remoteObject)
    {
        SharingTarget sharingTarget = remoteObject?.GetComponent<SharingTarget>();
        if (sharingTarget != null)
        {
            SendStagedObject(sharingTarget.SharingId);
        }
    }

    /// <summary>
    /// Notify other users that a new model is being placed on the center stage.
    /// </summary>
    private void SendStagedObject(string sharingId)
    {
        if (!string.IsNullOrEmpty(sharingId))
        {
            target?.SetProperty(SharableStrings.StageObjectId, sharingId);
        }
    }

    /// <summary>
    /// Handle receiving an object id from the server. This is the object id of the model being placed on the center stage.
    /// </summary>
    private void ReceiveStagedObject(string sharingId)
    {
        if (StagedObjectSharingId != sharingId)
        {
            RemoteObject remoteObject = FindLoadedObject(sharingId);
            if (remoteObject != null)
            {
                stage?.StageObject(remoteObject, false);
            }
            else
            {
                stage?.ClearStage();
            }
        }
    }

    /// <summary>
    /// Handle receiving a new object id from the server. This object will be placed in the local scene if it doesn't already exist.
    /// </summary>
    private void ReceiveLoadedObject(ISharingServiceTarget target)
    {
        if (target != null && target.IsRoot && !target.HasProperty(SharableStrings.ObjectIsDeleting))
        {
            ReceiveLoadedObject(target.SharingId);
        }
    }

    /// <summary>
    /// Handle receiving a new object id from the server. This object will be placed in the local scene if it doesn't already exist.
    /// </summary>
    private void ReceiveLoadedObject(string sharingId)
    {
        if (stage != null && FindLoadedObject(sharingId) == null)
        {
            SharingTarget sharingTarget = null;
            RemoteObject remoteObject = stage.Load(null, false, false, (RemoteObject initializeThis) =>
            {
                sharingTarget = initializeThis.GetComponent<SharingTarget>();
                ISharingServiceTarget innerTarget = null;

                if (sharingTarget != null)
                {
                    innerTarget = AppServices.SharingService.CreateTargetFromSharingId(sharingId);
                }

                if (innerTarget != null)
                {
                    sharingTarget.InnerTarget = innerTarget;
                }
            });

            if (sharingTarget != null)
            {
                ReceiveLoadedObject(remoteObject, StagedObjectSharingId == sharingTarget.SharingId);
            }
            else
            {
                Destroy(remoteObject);
            }
        }
    }

    /// <summary>
    /// Handle receiving a new object id from the server. This object will be placed in the local scene and placed either in the staged or unstaged container.
    /// </summary>
    private void ReceiveLoadedObject(RemoteObject remoteObject, bool staged)
    {
        if (stage != null)
        {
            if (staged)
            {
                stage.StageObject(remoteObject, false);
            }
            else
            {
                stage.UnstageObject(remoteObject, false);
            }
        }
    }

    /// <summary>
    /// Search for a game object with the given "sharing id". If not found, null is returned.
    /// </summary>
    private RemoteObject FindLoadedObject(string sharingId)
    {
        RemoteObject result = null;
        SharingTarget[] sharingTargets = stage?.GetComponentsInChildren<SharingTarget>();
        int count = sharingTargets?.Length ?? 0;

        for (int i = 0; i < count; i++)
        {
            var target = sharingTargets[i];
            if (target.SharingId == sharingId)
            {
                result = target.GetComponentInChildren<RemoteObject>();
                break;
            }
        }

        return result;
    }
    #endregion Private Functions
}
