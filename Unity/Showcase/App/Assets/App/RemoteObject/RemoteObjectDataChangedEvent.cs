// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine.Events;
using System;

[Serializable]
public class RemoteObjectDataChangedEvent : UnityEvent<RemoteObjectDataChangedEventData>
{
}

[Serializable]
public class RemoteObjectDataChangedEventData
{
    public RemoteObjectDataChangedEventData(RemoteObject sender, RemoteItemBase data)
    {
        Sender = sender;
        Data = data;
    }

    #region Public Properties
    public RemoteObject Sender { get; }
    public RemoteItemBase Data { get; }
    #endregion Public Properties
}
