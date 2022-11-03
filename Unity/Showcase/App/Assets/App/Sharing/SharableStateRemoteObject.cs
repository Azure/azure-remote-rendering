// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication;
using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// A class for sharing the state of Remote Objects with other clients.
/// </summary>
public class SharableStateRemoteObject : MonoBehaviour, ISharingServiceObjectInitialized
{
    private string _serializedData = null;

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
    [Tooltip("The remote object to share.")]
    private RemoteObject remoteObject;

    /// <summary>
    /// The remote object to share.
    /// </summary>
    public RemoteObject RemoteObject
    {
        get => remoteObject;
        set => remoteObject = value;
    }

    [SerializeField]
    [Tooltip("The remote object mover.")]
    private RemoteObjectReparent remoteObjectMover;

    /// <summary>
    /// The remote object mover.
    /// </summary>
    public RemoteObjectReparent RemoteObjectMover
    {
        get => remoteObjectMover;
        set => remoteObjectMover = value;
    }
    #endregion Serialized Fields

    #region MonoBehaviour Functions
    private void Awake()
    {
        if (sharingObject == null)
        {
            sharingObject = GetComponentInChildren<SharingObjectBase>();
        }

        if (remoteObject == null)
        {
            remoteObject = GetComponentInChildren<RemoteObject>();
        }

        if (remoteObjectMover == null)
        {
            remoteObjectMover = GetComponentInChildren<RemoteObjectReparent>();
        }
    }

    private void Start()
    {
        if (remoteObject != null)
        {
            remoteObject.Deleted.AddListener(SendDelete);
            remoteObject.DataChanged.AddListener(SendModelData);
            remoteObject.IsEnabledChanged.AddListener(SendEnabled);
            SendModelData(remoteObject.Data);
        }

        if (sharingObject != null)
        {
            sharingObject.PropertyChanged += HandlePropertyChanged;
        }
    }

    private void OnDestroy()
    {
        if (remoteObject != null)
        {
            remoteObject.Deleted.RemoveListener(SendDelete);
            remoteObject.DataChanged.RemoveListener(SendModelData);
            remoteObject.IsEnabledChanged.RemoveListener(SendEnabled);
        }

        if (sharingObject != null)
        {
            sharingObject.PropertyChanged -= HandlePropertyChanged;
            sharingObject = null;
        }
    }

    private void OnApplicationQuit()
    {
        // prevent delete messages on shut down
        sharingObject = null;
    }
    #endregion MonoBehaviour Functions

    #region ISharingServiceTargetInitialized Functions
    /// <summary>
    /// Invoked when the sharing object has been initialized
    /// </summary>
    public void Initialized(ISharingServiceObject target, object[] data)
    {
        if (remoteObjectMover != null)
        {
            RemoteObjectReparent.OperationType type = RemoteObjectReparent.OperationType.Unstaged;
            if (data != null)
            {
                foreach (var item in data)
                {
                    if (item is RemoteObjectSpawnData)
                    {
                        if (((RemoteObjectSpawnData)item).Staged)
                        {
                            type = RemoteObjectReparent.OperationType.Staged;
                        }
                        break;
                    }
                }
            }

            bool reposition = type == RemoteObjectReparent.OperationType.Staged;
            remoteObjectMover.Reparent(reposition, type);
        }
    }
    #endregion ISharingServiceTargetInitialized Functions

    #region Private Functions
    /// <summary>
    /// Handle property received from the server.
    /// </summary>
    private void HandlePropertyChanged(ISharingServiceObject sender, string property, object input)
    {
        switch (input)
        {
            case string value when property == SharableStrings.ObjectData:
                ReceiveModelData(value);
                break;

            case bool value when property == SharableStrings.ObjectIsEnabled:
                remoteObject.IsEnabled = value;
                break;
        }
    }

    /// <summary>
    /// Handle property received from the server.
    /// </summary>
    private void SendEnabled(bool isEnabled)
    {
        sharingObject?.SetProperty(SharableStrings.ObjectIsEnabled, isEnabled);
    }
    
    /// <summary>
    /// Notify other clients for a RemoteObject being deleted.
    /// </summary>
    private void SendDelete(RemoteObjectDeletedEventData args)
    {
        if (sharingObject != null)
        {
            // mark as being deleting, this flag is check by other client before they create local versions of objects.
            sharingObject.SetProperty(SharableStrings.ObjectIsDeleting, true);
            sharingObject.SendCommandMessage(SharableStrings.CommandObjectDelete);
            sharingObject.ClearProperties();
        }
    }

    private void Despawn()
    {
        if (sharingObject != null)
        {
            sharingObject.Despawn(); 
        }
    }

    private void SendModelData(RemoteObjectDataChangedEventData args)
    {
        SendModelData(args.Data);
    }

    private async void SendModelData(RemoteItemBase data)
    { 
        RemoteContainer container = data as RemoteContainer;
        if (container != null && sharingObject != null)
        {
            string serializedData = null;
            try
            {
                serializedData = await XmlHelper.Serialize<RemoteContainer>(container);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to serialize model data.\r\nException: {ex}");
            }

            // Don't allow sending data if we already received or sent it. This is to avoid resending received data.
            if (serializedData != null &&
                serializedData != _serializedData)
            {
                sharingObject.SetProperty(SharableStrings.ObjectData, serializedData);
                _serializedData = serializedData;
            }
        }
    }

    private async void ReceiveModelData(string serializedData)
    {
        // Don't allow use of data if we already received or sent it. This is to avoid handling data this client sends.
        if (serializedData == _serializedData)
        {
            return;
        }

        // If there is no data, then delete this object.
        // An empty string can occur when a model has been deleted on the sharing (collaboration) service.
        if (string.IsNullOrEmpty(serializedData))
        {
            Despawn();
        }

        if (remoteObject != null)
        {
            try
            {
                var data = await XmlHelper.Deserialize<RemoteContainer>(serializedData);
                if (remoteObject.Data == null)
                {
                    remoteObject.Data = data;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to deserialize model data.\r\nException: {ex.ToString()}");
            }
        }

        _serializedData = serializedData;
    }
    #endregion Private Functions
}
