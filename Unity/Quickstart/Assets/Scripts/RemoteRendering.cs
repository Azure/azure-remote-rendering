using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Microsoft.Azure.RemoteRendering;
using Microsoft.Azure.RemoteRendering.Unity;
using Quaternion = UnityEngine.Quaternion;
using System.Globalization;

#if UNITY_WSA
using UnityEngine.XR.WSA;
#endif

// ask Unity to automatically append an ARRServiceUnity component when a RemoteRendering script is attached
[RequireComponent(typeof(ARRServiceUnity))]
public class RemoteRendering : MonoBehaviour
{
    // Fill out the variables with your account details. Note that these need to be set on the RemoteRendering object in the Unity scene.
    // Modifying these values in code has no effect.

    // AccountDomain must be '<region>.mixedreality.azure.com' - if no '<region>' is specified, connections will fail
    // For the best suitable region near you, please refer to the "Reference > Regions" chapter in the documentation
    public string AccountDomain = "westus2.mixedreality.azure.com";
    public string AccountId = "<enter your account id here>";
    public string AccountKey = "<enter your account key here>";

    public uint MaxLeaseTimeHours = 0;
    public uint MaxLeaseTimeMinutes = 10;
    public RenderingSessionVmSize VMSize = RenderingSessionVmSize.Standard;

    private readonly string LastSessionIdKey = "Microsoft.Azure.RemoteRendering.Quickstart.LastSessionId";

    private string _sessionId = null;

    // Load or store the session id from the editor or player settings, so we can try to re-use an existing session
    [SerializeField]
    public string SessionId
    {
        get
        {
#if UNITY_EDITOR
            _sessionId = UnityEditor.EditorPrefs.GetString(LastSessionIdKey);
#else
            _sessionId = PlayerPrefs.GetString(LastSessionIdKey);
#endif
            return _sessionId;
        }

        set
        {
#if UNITY_EDITOR
            UnityEditor.EditorPrefs.SetString(LastSessionIdKey, value);
#else
            PlayerPrefs.SetString(LastSessionIdKey, value);
#endif
            _sessionId = value;
        }
    }

    public string ModelName = "builtin://Engine";

    public RemoteFrameStats Stats;

    private ARRServiceUnity arrService = null;
    private Entity modelEntity = null;
    private GameObject modelEntityGO = null;
    private bool isConnected = false;

#if UNITY_WSA
    private WorldAnchor modelWorldAnchor = null;
#endif

    private void Awake()
    {
        // initialize Azure Remote Rendering for use in Unity:
        // it needs to know which camera is used for rendering the scene
        RemoteUnityClientInit clientInit = new RemoteUnityClientInit(Camera.main);
        RemoteManagerUnity.InitializeManager(clientInit);

        // lookup the ARRServiceUnity component and subscribe to session events
        arrService = GetComponent<ARRServiceUnity>();
        arrService.OnSessionStarted += ARRService_OnSessionStarted;
        arrService.OnSessionStatusChanged += ARRService_OnSessionStatusChanged;
        arrService.OnSessionEnded += ARRService_OnSessionEnded;

        if (Stats != null)
        {
            Stats.Initialize(arrService);
        }
    }

    private void OnEnable()
    {
        AutoStartSession();
    }

    private void OnDisable()
    {
        DisconnectSession();
    }

    private void OnDestroy()
    {
        arrService.OnSessionStarted -= ARRService_OnSessionStarted;
        arrService.OnSessionStatusChanged -= ARRService_OnSessionStatusChanged;
        arrService.OnSessionEnded -= ARRService_OnSessionEnded;

        RemoteManagerStatic.ShutdownRemoteRendering();
    }


    private void CreateFrontend()
    {
        if (arrService.Frontend != null)
        {
            // early out if the front-end has been created before
            return;
        }

        // initialize the ARR service with our account details
        AzureFrontendAccountInfo accountInfo = new AzureFrontendAccountInfo();
        accountInfo.AccountKey = AccountKey;
        accountInfo.AccountId = AccountId;
        accountInfo.AccountDomain = AccountDomain;

        arrService.Initialize(accountInfo);
    }

    public void CreateSession()
    {
        CreateFrontend();

        // StartSession will call ARRService_OnSessionStarted once the session becomes available
        arrService.StartSession(new RenderingSessionCreationParams(VMSize, MaxLeaseTimeHours, MaxLeaseTimeMinutes));
    }

    public void StopSession()
    {
        arrService.StopSession();
    }

    private async void ARRService_OnSessionStarted(AzureSession session)
    {
        LogSessionStatus(session);

        session.ConnectionStatusChanged += AzureSession_OnConnectionStatusChanged;

        if (arrService.CurrentActiveSession != null)
        {
            var sessionProperties = await arrService.CurrentActiveSession.GetPropertiesAsync().AsTask();

            if (sessionProperties.Status != RenderingSessionStatus.Ready &&
                sessionProperties.Status != RenderingSessionStatus.Starting)
            {
                LogMessage($"Existing session has status '{sessionProperties.Status}'", true);
                StopSession();
            }
            else
            {
                SessionId = session.SessionUUID;
            }
        }
    }

    private void ARRService_OnSessionEnded(AzureSession session)
    {
        LogSessionStatus(session);

        if (session != null)
        {
            session.ConnectionStatusChanged -= AzureSession_OnConnectionStatusChanged;
        }
    }

    private void ARRService_OnSessionStatusChanged(AzureSession session)
    {
        LogSessionStatus(session);
    }

    private async void LogSessionStatus(AzureSession session)
    {
        if (session != null)
        {
            var sessionProperties = await session.GetPropertiesAsync().AsTask();
            LogSessionStatus(sessionProperties);
        }
        else
        {
            var sessionProperties = arrService.LastProperties;
            LogMessage($"Session ended: Id={sessionProperties.Id}");
        }
    }

    private void LogSessionStatus(RenderingSessionProperties sessionProperties)
    {
        Debug.Log($"Session '{sessionProperties.Id}' is {sessionProperties.Status}. Size={sessionProperties.Size}" +
            (!string.IsNullOrEmpty(sessionProperties.Hostname) ? $", Hostname='{sessionProperties.Hostname}'" : "") +
            (!string.IsNullOrEmpty(sessionProperties.Message) ? $", Message='{sessionProperties.Message}'" : ""));
    }

    private void LogMessage(string message, bool error = false)
    {
        if (error)
        {
            Debug.LogError(message);
        }
        else
        {
            Debug.Log(message);
        }

        if (Stats != null)
        {
            Stats.SetLogMessage(message, error);
        }
    }

    public void UseExistingSession()
    {
        CreateFrontend();

        // OpenSession will call ARRService_OnSessionStarted once the session becomes available
        arrService.OpenSession(SessionId);
    }

    public void ConnectSession()
    {
        arrService.CurrentActiveSession?.ConnectToRuntime(new ConnectToRuntimeParams());
    }

    public void DisconnectSession()
    {
        if (isConnected)
        {
            arrService.CurrentActiveSession?.DisconnectFromRuntime();
        }

        DestroyModel();
    }

    private void AzureSession_OnConnectionStatusChanged(ConnectionStatus status, Result result)
    {
        LogMessage($"Connection status: '{status}', result: '{result}'", result != Result.Success);
        isConnected = (status == ConnectionStatus.Connected);
    }

    private void LateUpdate()
    {
        // The session must have its runtime pump updated.
        // The update will push messages to the server, receive messages, and update the frame-buffer with the remotely rendered content.
        arrService.CurrentActiveSession?.Actions.Update();
    }

    public async void LoadModel()
    {
        // create a root object to parent a loaded model to
        modelEntity = arrService.CurrentActiveSession.Actions.CreateEntity();

        // get the game object representation of this entity
        modelEntityGO = modelEntity.GetOrCreateGameObject(UnityCreationMode.DoNotCreateUnityComponents);

        // ensure the entity will sync translations with the server
        var sync = modelEntityGO.GetComponent<RemoteEntitySyncObject>();
        sync.SyncEveryFrame = true;

        // set position to an arbitrary distance from the parent
        PlaceModel();
        modelEntityGO.transform.localScale = Vector3.one;

        // load a model that will be parented to the entity
        var loadModelParams = new LoadModelFromSASParams(ModelName, modelEntity);
        var async = arrService.CurrentActiveSession.Actions.LoadModelFromSASAsync(loadModelParams);
        async.ProgressUpdated += (float progress) =>
        {
            LogMessage($"Loading Model: {progress.ToString("P2", CultureInfo.InvariantCulture)}");
        };

        await async.AsTask();
    }

    public void PlaceModel()
    {
#if UNITY_WSA
        if (modelWorldAnchor != null)
        {
            DestroyImmediate(modelWorldAnchor);

            modelWorldAnchor = null;
        }
#endif

        if (modelEntityGO != null)
        {
            modelEntityGO.transform.position = Camera.main.transform.position + Camera.main.transform.forward * 2;
            modelEntityGO.transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward);
#if UNITY_WSA
            // anchor the model in the world
            modelWorldAnchor = modelEntityGO.AddComponent<WorldAnchor>();
#endif    
        }
    }

    public void DestroyModel()
    {
        if (modelEntity == null)
        {
            return;
        }

#if UNITY_WSA
        if (modelWorldAnchor != null)
        {
            DestroyImmediate(modelWorldAnchor);

            modelWorldAnchor = null;
        }
#endif

        modelEntity.Destroy();
        modelEntity = null;

        DestroyImmediate(modelEntityGO);
    }

    // start a new session or use an existing one, connect to it and then load the model
    public void AutoStartSession()
    {
        if (!string.IsNullOrEmpty(SessionId))
        {
            UseExistingSession();
            SessionId = null;
        }
        else
        {
            CreateSession();
        }

        StartCoroutine(WaitForSessionReady(() =>
        {
            // once the session is ready, connect to it
            ConnectSession();

            // wait for the Connection to Connect then load the model
            StartCoroutine(WaitForConnectionChange(() =>
            {
                LoadModel();
            }));
        }));
    }

    // wait for the session's ready status and then trigger the action callback
    IEnumerator WaitForSessionReady(Action action)
    {
        // wait for Ready status
        while (true)
        {
            if (arrService.CurrentActiveSession != null
                &&
                arrService.LastProperties.Status == RenderingSessionStatus.Ready)
            {
                break;
            }

            yield return null;
        }

        // trigger callback
        action();
    }

    // wait for the connected status and then trigger the action callback
    IEnumerator WaitForConnectionChange(Action action)
    {
        // trigger callback once the connection status is Connected
        while (arrService.CurrentActiveSession.ConnectionStatus != ConnectionStatus.Connected)
        {
            yield return null;
        }

        yield return new WaitForSeconds(1.0f);

        // trigger callback
        action();
    }
}
