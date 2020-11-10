// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine.Events;
using System;

[Serializable]
public class RemoteObjectDeletedEvent : UnityEvent<RemoteObjectDeletedEventData>
{
}

public class RemoteObjectDeletedEventData
{
    public RemoteObjectDeletedEventData(RemoteObject sender)
    {
        Sender = sender;
    }

    #region Public Properties
    public RemoteObject Sender { get; }
    #endregion Public Properties
}
