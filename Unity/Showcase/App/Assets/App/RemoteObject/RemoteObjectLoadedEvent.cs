// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering.Unity;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

[Serializable]
public class RemoteObjectLoadedEvent : UnityEvent<RemoteObjectLoadedEventData>
{
}

public class RemoteObjectLoadedEventData
{
    public RemoteObjectLoadedEventData(RemoteEntitySyncObject syncObject, List<EntitySnapshot> snapshot)
    {
        SyncObject = syncObject;
        Snapshot = snapshot;
    }

    #region Public Properties
    public RemoteEntitySyncObject SyncObject { get; }
    public List<EntitySnapshot> Snapshot { get; }
    #endregion Public Properties
}

public class RemoteObjectLoadingProgressEventData
{
    public RemoteObjectLoadingProgressEventData(float oldProgress, float newProgress)
    {
        OldProgress = oldProgress;
        NewProgress = newProgress;
    }

    #region Public Properties
    public float OldProgress { get; }

    public float NewProgress { get; }
    #endregion Public Properties
}
