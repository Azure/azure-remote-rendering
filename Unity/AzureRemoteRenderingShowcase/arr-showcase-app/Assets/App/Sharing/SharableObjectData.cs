// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication;
using System;
using UnityEngine;

/// <summary>
/// A class for sharing the state of Remote Objects with other clients.
/// </summary>
public class SharableObjectData : MonoBehaviour
{
    private string _serializedData = null;

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
    #endregion Serialized Fields

    #region MonoBehaviour Functions
    private void Start()
    {
        if (target == null)
        {
            target = GetComponentInChildren<SharingTarget>();
        }

        if (remoteObject == null)
        {
            remoteObject = GetComponentInChildren<RemoteObject>();
        }

        if (remoteObject != null)
        {
            remoteObject.Deleted.AddListener(SendDelete);
            remoteObject.DataChanged.AddListener(SendModelData);
            remoteObject.IsEnabledChanged.AddListener(SendEnabled);
            SendModelData(remoteObject.Data);
        }

        if (target != null)
        {
            target.PropertyChanged += HandlePropertyChanged;
            target.MessageReceived += HandleMessageReceived;
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

        if (target != null)
        {
            target.PropertyChanged -= HandlePropertyChanged;
            target = null;
        }
    }

    private void OnApplicationQuit()
    {
        // prevent delete messages on shut down
        target = null;
    }
    #endregion MonoBehaviour Functions

    #region Private Functions
    /// <summary>
    /// Handle messages received from the server.
    /// </summary>
    private void HandleMessageReceived(ISharingServiceMessage message)
    {
        if (message.Command == SharableStrings.CommandObjectDelete)
        {
            ReceiveDelete();
        }
    }

    /// <summary>
    /// Handle property received from the server.
    /// </summary>
    private void HandlePropertyChanged(string property, object input)
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
        target?.SetProperty(SharableStrings.ObjectIsEnabled, isEnabled);
    }
    
    /// <summary>
    /// Notify other clients for a RemoteObject being deleted.
    /// </summary>
    private void SendDelete(RemoteObjectDeletedEventData args)
    {
        if (target != null)
        {
            // mark as being deleting, this flag is check by other client before they create local versions of objects.
            target.SetProperty(SharableStrings.ObjectIsDeleting, true);
            target.SendCommandMessage(SharableStrings.CommandObjectDelete);
            target.ClearProperties();
        }
    }

    private void ReceiveDelete()
    {
        if (remoteObject != null)
        {
            remoteObject.Destroy();
        }
    }

    private async void SendModelData(RemoteItemBase modelData)
    {
        RemoteContainer container = modelData as RemoteContainer;
        if (container != null && target != null)
        {
            string serializedData = null;
            try
            {
                serializedData = await XmlHelper.Serialize<RemoteContainer>(container);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to serialize model data.\r\nException: {ex.ToString()}");
            }

            // Don't allow sending data if we already received or sent it. This is to avoid resending received data.
            if (serializedData != null &&
                serializedData != _serializedData)
            {
                target.SetProperty(SharableStrings.ObjectData, serializedData);
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
