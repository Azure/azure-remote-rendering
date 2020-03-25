using Microsoft.Azure.RemoteRendering;
using Microsoft.Azure.RemoteRendering.Unity;
using UnityEngine;

public class CutPlane : MonoBehaviour
{
    private ARRCutPlaneComponent localCutPlaneComponent = null;
    private RemoteEntitySyncObject remoteEntitySync = null;

    void Awake()
    {
        RemoteManagerUnity.OnSessionUpdate += OnSessionUpdate;
    }

    void OnDestroy()
    {
        RemoteManagerUnity.OnSessionUpdate -= OnSessionUpdate;    
    }

    void OnEnable()
    {
        if (RemoteManagerUnity.IsConnected)
        {
            CreateRemoteComponent();
        }

        if (localCutPlaneComponent && localCutPlaneComponent.IsComponentValid)
        {
            localCutPlaneComponent.RemoteComponent.Enabled = true;
        }

        if (remoteEntitySync && remoteEntitySync.IsEntityValid)
        {
            remoteEntitySync.SyncEveryFrame = true;
        }
    }

    void OnDisable()
    {
        if (localCutPlaneComponent && localCutPlaneComponent.IsComponentValid)
        {
            localCutPlaneComponent.RemoteComponent.Enabled = false;
        }

        if (remoteEntitySync && remoteEntitySync.IsEntityValid)
        {
            remoteEntitySync.SyncEveryFrame = false;
        }
    }

    void OnSessionUpdate(RemoteManagerUnity.SessionUpdate update)
    {
        if (update == RemoteManagerUnity.SessionUpdate.SessionConnected)
        {
            CreateRemoteComponent();
        }
        else if (update == RemoteManagerUnity.SessionUpdate.SessionDisconnected)
        {
            DestroyRemoteComponent();
        }
    }

    private void CreateRemoteComponent()
    {
        if (localCutPlaneComponent == null)
        {
            localCutPlaneComponent = gameObject.CreateArrComponent<ARRCutPlaneComponent>(RemoteManagerUnity.CurrentSession);
        }

        if (remoteEntitySync == null)
        {
            remoteEntitySync = gameObject.GetComponent<RemoteEntitySyncObject>();
            remoteEntitySync.SyncEveryFrame = true;
        }

        localCutPlaneComponent.RemoteComponent.Normal = Axis.X;
        localCutPlaneComponent.RemoteComponent.FadeLength = 0.025f;
        localCutPlaneComponent.RemoteComponent.FadeColor = new Color4Ub(255, 128, 0, 255);
        localCutPlaneComponent.RemoteComponent.Enabled = true;
    }

    private void DestroyRemoteComponent()
    {
        Object.Destroy(localCutPlaneComponent);
        Object.Destroy(remoteEntitySync);

        localCutPlaneComponent = null;
        remoteEntitySync = null;
    }
}
