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
    private GameObject modelEntityGO = null;

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
        arrService.OnSessionStatusChanged += ARRService_OnSessionStatusChanged;

        if (Stats != null)
        {
            Stats.Initialize(arrService);
        }
    }

    private void Start()
    {
        AutoStartSessionAsync();
    }

    private void OnDestroy()
    {
        DisconnectSession();

        arrService.OnSessionStatusChanged -= ARRService_OnSessionStatusChanged;

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

    private void ARRService_OnSessionStatusChanged(ARRServiceUnity service, AzureSession session)
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

    public void DisconnectSession()
    {
        if( arrService.CurrentActiveSession?.ConnectionStatus == ConnectionStatus.Connected )
        {
            DestroyModel();
            arrService.CurrentActiveSession.DisconnectFromRuntime();
        }
    }

    private void LateUpdate()
    {
        // The session must have its runtime pump updated.
        // The update will push messages to the server, receive messages, and update the frame-buffer with the remotely rendered content.
        arrService.CurrentActiveSession?.Actions.Update();
    }

    private async void LoadModel()
    {
        // create a root object to parent a loaded model to
        Entity modelEntity = arrService.CurrentActiveSession.Actions.CreateEntity();

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

    private void PlaceModel()
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
        if (modelEntityGO == null)
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

        DestroyImmediate(modelEntityGO);
    }

    // start a new session or use an existing one, connect to it and then load the model
    public async void AutoStartSessionAsync()
    {
        try
        {
            CreateFrontend();

            RenderingSessionProperties props = default(RenderingSessionProperties);
            bool hasSessionId = !string.IsNullOrEmpty(SessionId);
            string sessionId = SessionId;

            if (hasSessionId)
            {
                try
                {
                    props = await arrService.OpenSession(SessionId);
                }
                catch (RRSessionException sessionException)
                {
                    LogMessage($"Error opening session: {sessionException.Context.ErrorMessage}", true);
                }
                catch (RRException generalException)
                {
                    LogMessage($"General error opening session: {generalException.ErrorCode}", true);
                }
                finally
                {
                    SessionId = null;
                }
            }

            if (props.Status != RenderingSessionStatus.Ready)
            {
                if(hasSessionId)
                {
                    LogMessage($"Session ID: {sessionId} is in state: {props.Status}. Starting a new session.");
                }

                props = await arrService.StartSession(new RenderingSessionCreationParams(VMSize, MaxLeaseTimeHours, MaxLeaseTimeMinutes));

                if (props.Status != RenderingSessionStatus.Ready)
                {
                    LogMessage($"Session creation failed. Session status: {props.Status}.", true);
                    return;
                }
            }

            SessionId = arrService.CurrentActiveSession.SessionUUID;

            if (!enabled)
            {
                return;
            }
        }
        catch (RRSessionException sessionException)
        {
            LogMessage($"Error creating session: {sessionException.Context.ErrorMessage}", true);
        }
        catch (RRException generalException)
        {
            LogMessage($"General error creating session: {generalException.ErrorCode}", true);
        }

        ConnectAndLoadModel();
    }

    public async void ConnectAndLoadModel()
    {
        try
        {
            if (arrService.CurrentActiveSession?.ConnectionStatus != ConnectionStatus.Disconnected)
            {
                return;
            }

            Result res = await arrService.CurrentActiveSession.ConnectToRuntime(new ConnectToRuntimeParams()).AsTask();

            if (arrService.CurrentActiveSession.IsConnected)
            {
                LoadModel();
            }
            else
            {
                LogMessage($"Failed to connect to runtime: {res}.", true);
            }
        }
        catch (RRSessionException sessionException)
        {
            LogMessage($"Error connecting to runtime: {sessionException.Context.ErrorMessage}", true);
        }
        catch (RRException generalException)
        {
            LogMessage($"General error connecting to runtime: {generalException.ErrorCode}", true);
        }
    }
}
