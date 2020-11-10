// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine.Events;

[Serializable]
public class RemoteObjectResetCompletedEvent : UnityEvent<RemoteObjectResetCompletedEventData>
{
}

public class RemoteObjectResetCompletedEventData
{
    public RemoteObjectResetCompletedEventData(RemoteObjectReset sender)
    {
        Sender = sender;
    }

    #region Public Properties
    public RemoteObjectReset Sender { get; }
    #endregion Public Properties
}
