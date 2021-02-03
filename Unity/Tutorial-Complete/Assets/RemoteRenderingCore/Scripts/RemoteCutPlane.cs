// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.Azure.RemoteRendering.Unity;
using System;
using UnityEngine;

public class RemoteCutPlane : BaseRemoteCutPlane
{
    public Color SliceColor = new Color(0.5f, 0f, 0f, .5f);
    public float FadeLength = 0.01f;
    public Axis SliceNormal = Axis.NegativeY;

    public bool AutomaticallyCreate = true;

    private CutPlaneComponent remoteCutPlaneComponent;
    private bool cutPlaneReady = false;

    public override bool CutPlaneReady
    {
        get => cutPlaneReady;
        set
        {
            cutPlaneReady = value;
            CutPlaneReadyChanged?.Invoke(cutPlaneReady);
        }
    }

    public override event Action<bool> CutPlaneReadyChanged;

    public UnityBoolEvent OnCutPlaneReadyChanged = new UnityBoolEvent();

    public void Start()
    {
        // Hook up the event to the Unity event
        CutPlaneReadyChanged += (ready) => OnCutPlaneReadyChanged?.Invoke(ready);

        RemoteRenderingCoordinator.CoordinatorStateChange += RemoteRenderingCoordinator_CoordinatorStateChange;
        RemoteRenderingCoordinator_CoordinatorStateChange(RemoteRenderingCoordinator.instance.CurrentCoordinatorState);
    }

    private void RemoteRenderingCoordinator_CoordinatorStateChange(RemoteRenderingCoordinator.RemoteRenderingState state)
    {
        switch (state)
        {
            case RemoteRenderingCoordinator.RemoteRenderingState.RuntimeConnected:
                if (AutomaticallyCreate)
                    CreateCutPlane();
                break;
            default:
                DestroyCutPlane();
                break;
        }
    }

    public override void CreateCutPlane()
    {
        if (remoteCutPlaneComponent != null)
            return; //Nothing to do!

        //Create a root object for the cut plane
        var cutEntity = RemoteRenderingCoordinator.CurrentSession.Connection.CreateEntity();

        //Bind the remote entity to this game object
        cutEntity.BindToUnityGameObject(this.gameObject);

        //Sync the transform of this object so we can move the cut plane
        var syncComponent = this.gameObject.GetComponent<RemoteEntitySyncObject>();
        syncComponent.SyncEveryFrame = true;

        //Add a cut plane to the entity
        remoteCutPlaneComponent = RemoteRenderingCoordinator.CurrentSession.Connection.CreateComponent(ObjectType.CutPlaneComponent, cutEntity) as CutPlaneComponent;

        //Configure the cut plane
        remoteCutPlaneComponent.Normal = SliceNormal;
        remoteCutPlaneComponent.FadeColor = SliceColor.toRemote();
        remoteCutPlaneComponent.FadeLength = FadeLength;
        CutPlaneReady = true;
    }

    public override void DestroyCutPlane()
    {
        if (remoteCutPlaneComponent == null)
            return; //Nothing to do!

        remoteCutPlaneComponent.Owner.Destroy();
        remoteCutPlaneComponent = null;
        CutPlaneReady = false;
    }
}
