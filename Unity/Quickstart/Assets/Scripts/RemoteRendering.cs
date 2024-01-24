// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.Threading.Tasks;

using UnityEngine;

using Microsoft.Azure.RemoteRendering;
using Microsoft.Azure.RemoteRendering.Unity;

#if UNITY_WSA && AR_FOUNDATION_AVAILABLE
using UnityEngine.XR.ARFoundation;
#endif

// Ask Unity to automatically append an ARRServiceUnity component when a RemoteRendering script is attached.
[RequireComponent(typeof(ARRServiceUnity))]
public class RemoteRendering : MonoBehaviour
{
    private static readonly string LastSessionIdKey = "Microsoft.Azure.RemoteRendering.Quickstart.LastSessionId";

    private string sessionId = null;
    private ARRServiceUnity arrService = null;
    private GameObject modelEntityGO = null;

#if UNITY_WSA && AR_FOUNDATION_AVAILABLE
    private ARAnchor modelArAnchor = null;
#endif

    // Fill out the variables with your account details. Note that these need to be set on the RemoteRendering object in the Unity scene.
    // Modifying these values in code has no effect.
    /// <summary>
    /// RemoteRenderingDomain must be '<region>.mixedreality.azure.com' - if no '<region>' is specified, connections will fail
    /// For the best suitable region near you, please refer to the "Reference > Regions" chapter in the documentation
    /// </summary>
    public string RemoteRenderingDomain = "westus2.mixedreality.azure.com";
    /// <summary>
    /// AccountDomain must be '<account_region>.mixedreality.azure.com' where '<account_region>' is the your Remote Rendering
    /// account location.
    /// </summary>
    public string AccountDomain = "<enter your account domain here>";
    public string AccountId = "<enter your account id here>";
    public string AccountKey = "<enter your account key here>";

    public uint MaxLeaseTimeHours = 0;
    public uint MaxLeaseTimeMinutes = 10;
    public RenderingSessionVmSize VMSize = RenderingSessionVmSize.Standard;

    public string ModelName = "builtin://Engine";
    public RemoteFrameStats Stats;

    /// <summary>
    /// Load or store the session id from the persistent preference store, so we can try to re-use an existing session.
    /// </summary>
    public string SessionId
    {
        get
        {
            if (sessionId == null)
            {
                sessionId = PlayerPrefs.GetString(LastSessionIdKey, string.Empty);
            }
            return sessionId;
        }

        set
        {
            if (sessionId != value)
            {
                sessionId = value;
                PlayerPrefs.SetString(LastSessionIdKey, value);
            }
        }
    }


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
        if (arrService.Client != null)
        {
            // early out if the front-end has been created before
            return;
        }

        // initialize the ARR service with our account details.
        // Trim the strings in case they have been pasted into the inspector with trailing whitespace
        SessionConfiguration sessionConfiguration = new SessionConfiguration();
        sessionConfiguration.AccountKey = AccountKey.Trim();
        sessionConfiguration.AccountId = AccountId.Trim();
        sessionConfiguration.RemoteRenderingDomain = RemoteRenderingDomain.Trim();
        sessionConfiguration.AccountDomain = AccountDomain.Trim();

        arrService.Initialize(sessionConfiguration);
    }

    private void ARRService_OnSessionStatusChanged(ARRServiceUnity service, RenderingSession session)
    {
        LogSessionStatus(session);
    }

    private async void LogSessionStatus(RenderingSession session)
    {
        if (session != null)
        {
            var sessionProperties = await session.GetPropertiesAsync();
            LogSessionStatus(sessionProperties.SessionProperties);
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
        if (arrService.CurrentActiveSession?.ConnectionStatus == ConnectionStatus.Connected)
        {
            DestroyModel();
            arrService.CurrentActiveSession.Disconnect();
        }
    }

    private void LateUpdate()
    {
        // The session must have its runtime pump updated.
        // The update will push messages to the server, receive messages, and update the frame-buffer with the remotely rendered content.
        arrService.CurrentActiveSession?.Connection.Update();
    }

    private async Task LoadModel()
    {
        // Create a root object to parent a loaded model to.
        Entity modelEntity = arrService.CurrentActiveSession.Connection.CreateEntity();
        modelEntity.Name = "Model";

        // Get the game object representation of this entity.
        modelEntityGO = modelEntity.GetOrCreateGameObject(UnityCreationMode.DoNotCreateUnityComponents);

        // Ensure the entity will sync translations with the server.
        var sync = modelEntityGO.GetComponent<RemoteEntitySyncObject>();
        sync.SyncEveryFrame = true;

        // Hide the scene tree until the model is loaded and we had time to get the AABB and recenter the model.
        var stateOverride = modelEntityGO.CreateArrComponent<ARRHierarchicalStateOverrideComponent>(arrService.CurrentActiveSession);
        stateOverride.RemoteComponent.HiddenState = HierarchicalEnableState.ForceOn;

        // Set position to an arbitrary distance from the parent.
        PlaceModel();
        modelEntityGO.transform.localScale = Vector3.one;

        // Load a model that will be parented to the entity.
        var loadModelParams = new LoadModelFromSasOptions(ModelName, modelEntity);
        var loadModelResult = await arrService.CurrentActiveSession.Connection.LoadModelFromSasAsync(loadModelParams, (float progress) =>
        {
            LogMessage($"Loading Model: {progress.ToString("P2", CultureInfo.InvariantCulture)}");
        });

        // Recenter / scale model.
        var rootGO = loadModelResult.Root.GetOrCreateGameObject(UnityCreationMode.DoNotCreateUnityComponents);
        rootGO.GetComponent<RemoteEntitySyncObject>().SyncEveryFrame = true;

        var aabb = (await loadModelResult.Root.QueryLocalBoundsAsync()).toUnity();
        bool tooBig = aabb.extents.magnitude > Camera.main.farClipPlane;
        bool tooFar = aabb.center.magnitude > Camera.main.farClipPlane;
        float scaleFactor = 1.0f;
        String modelMessage = "Model loaded";
        if (tooBig)
        {
            scaleFactor = (2.0f / aabb.extents.magnitude);
            rootGO.transform.localScale = (rootGO.transform.localScale * scaleFactor);
            modelMessage += $", too big (scaled to {(scaleFactor).ToString("P2", CultureInfo.InvariantCulture)})";
        }
        rootGO.transform.localPosition = (rootGO.transform.localPosition - aabb.center * scaleFactor);
        if (tooFar)
        {
            modelMessage += $", center too far (moved by {-aabb.center})";
        }
        LogMessage(modelMessage);

        // Model is loaded and recentered. We can show the model now.
        stateOverride.RemoteComponent.HiddenState = HierarchicalEnableState.InheritFromParent;
    }

    private void PlaceModel()
    {
#if UNITY_WSA && AR_FOUNDATION_AVAILABLE
        if (modelArAnchor != null)
        {
            DestroyImmediate(modelArAnchor);
            modelArAnchor = null;
        }
#endif

        if (modelEntityGO != null)
        {
            modelEntityGO.transform.position = Camera.main.transform.position + Camera.main.transform.forward * 2;
#if UNITY_WSA && AR_FOUNDATION_AVAILABLE
            // anchor the model in the world
            modelArAnchor = modelEntityGO.AddComponent<ARAnchor>();
#endif    
        }
    }

    public void DestroyModel()
    {
        if (modelEntityGO == null)
        {
            return;
        }

#if UNITY_WSA && AR_FOUNDATION_AVAILABLE
        if (modelArAnchor != null)
        {
            DestroyImmediate(modelArAnchor);
            modelArAnchor = null;
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
                    SessionId = string.Empty;
                }
            }

            if (props.Status != RenderingSessionStatus.Ready)
            {
                if (hasSessionId)
                {
                    LogMessage($"Session ID: {sessionId} is in state: {props.Status}. Starting a new session.");
                }

                props = await arrService.StartSession(new RenderingSessionCreationOptions(VMSize, (int)MaxLeaseTimeHours, (int)MaxLeaseTimeMinutes));

                if (props.Status != RenderingSessionStatus.Ready)
                {
                    LogMessage($"Session creation failed. Session status: {props.Status}.", true);
                    return;
                }
            }

            SessionId = arrService.CurrentActiveSession.SessionUuid;

            if (!enabled)
            {
                return;
            }
        }
        catch (RRSessionException sessionException)
        {
            LogMessage($"Error creating session: {sessionException.Context.ErrorMessage}", true);
            return;
        }
        catch (RRException generalException)
        {
            LogMessage($"General error creating session: {generalException.ErrorCode}", true);
            return;
        }
        catch (ArgumentException argumentException)
        {
            var msg = argumentException.Message + "\nPlease check your Remote Rendering account configuration.";
            LogMessage(msg, true);
            return;
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

            ConnectionStatus res = await arrService.CurrentActiveSession.ConnectAsync(RendererInitOptions.PlatformDefaults);
            if (!arrService.CurrentActiveSession.IsConnected)
            {
                LogMessage($"Failed to connect to runtime: {res}.", true);
                return;
            }

            try
            {
                await LoadModel();
            }
            catch (RRException generalException)
            {
                LogMessage($"Failed to load model: {generalException.ErrorCode}", true);
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
