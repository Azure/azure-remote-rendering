// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using App.Authentication;
using Microsoft.Azure.Storage;
using Microsoft.Azure.RemoteRendering;
using Microsoft.Azure.RemoteRendering.Unity;
using Microsoft.MixedReality.Toolkit.Utilities;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

#if !UNITY_EDITOR && WINDOWS_UWP
using Windows.System;
using Windows.Storage;
#endif

using Remote = Microsoft.Azure.RemoteRendering;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// Usage
    /// IRemoteRenderingService remoteRenderingService = MixedRealityToolkit.Instance.GetService<IRemoteRenderingService>();
    /// </summary>
    [MixedRealityExtensionService(SupportedPlatforms.WindowsStandalone | SupportedPlatforms.WindowsUniversal | SupportedPlatforms.WindowsEditor)]
    public class RemoteRenderingService : BaseExtensionService, IRemoteRenderingService, IMixedRealityExtensionService
    {
        private Task _initializationTask;
        private Task<IRemoteRenderingMachine> _autoConnectTask;
        private RemoteRenderingClient _arrClient = null;
        private RemoteRenderingMachine _primaryMachine = null;
        private RemoteRenderingStorage _storage = new RemoteRenderingStorage();
        private List<RemoteRenderingMachine> _machines = new List<RemoteRenderingMachine>();
        private bool _isDestroyed;
        private ConnectionStatus _connectionStatus = ConnectionStatus.Disconnected;
        private RenderingSessionStatus _sessionStatus = RenderingSessionStatus.Unknown;
        private RemoteRenderingServiceStatus _combinedStatus = RemoteRenderingServiceStatus.Unknown;
        private Timer _editorUpdateTimer;
        private LastSessionData _lastSession = LastSessionData.Empty;
        private LogHelper<RemoteRenderingService> _log = new LogHelper<RemoteRenderingService>();
        private const string _lastSessionKey = "Microsoft.MixedReality.Toolkit.Extensions.RemoteRenderingService.LastSession2";

        public RemoteRenderingService(string name, uint priority, BaseMixedRealityProfile profile) : base(name, priority, profile)
        {
            LoadedProfile = profile as BaseRemoteRenderingServiceProfile;
            if (LoadedProfile == null)
            {
                //Default to a new dev profile if no profile is available
                LoadedProfile = ScriptableObject.CreateInstance<RemoteRenderingServiceDevelopmentProfile>();
            }
        }

        #region IRemoteRenderingService Properties
        /// <summary>
        /// Get the current session
        /// </summary>
        public IRemoteRenderingMachine PrimaryMachine => _primaryMachine;

        /// <summary>
        /// Get the status of the rendering service.
        /// </summary>
        public RemoteRenderingServiceStatus Status
        {
            get => _combinedStatus;

            private set
            {
                if (_combinedStatus != value)
                {
                    var oldStatus = _combinedStatus;
                    _combinedStatus = value;
                    StatusChanged?.Invoke(this, new RemoteRenderingSessionStatusChangedArgs(oldStatus, value));
                }
            }
        }

        /// <summary>
        /// A string used for debugging
        /// </summary>
        public string DebugStatus
        {
            get
            {
                if (_primaryMachine == null)
                {
                    return "No primary session.";
                }
                else
                {
                    var sessionId = $"Session Id: {_primaryMachine.Session.Id}";
                    var sessionStatus = $"{_primaryMachine.Session.StatusMessage}{_primaryMachine.Session.Connection.ConnectionStatus}";
                    if (_primaryMachine.Session.Connection.ConnectionStatus != ConnectionStatus.Connected)
                    {
                        return $"{sessionId}\r\n{sessionStatus}";
                    }
                    else
                    {
                        var sessionStatistics = _primaryMachine.ServiceStats?.GetStatsString();
                        return $"{sessionStatistics}\r\n{sessionId}\r\n{sessionStatus}";
                    }
                }
            }
        }

        /// <summary>
        /// Get all the known machines
        /// </summary>
        public IReadOnlyCollection<IRemoteRenderingMachine> Machines => _machines.AsReadOnly();

        /// <summary>
        /// Get the storage interface for obtaining the configured account's remote models.
        /// </summary>
        public IRemoteRenderingStorage Storage => _storage;

        /// <summary>
        /// Get the loaded profile. This is the profile object that also includes overrides from the various override files, as well as the default values.
        /// </summary>
        public BaseRemoteRenderingServiceProfile LoadedProfile { get; private set; }
        #endregion IRemoteRenderingService Properties

        #region IRemoteRenderingService Events
        /// <summary>
        /// Event raised when current machine changes.
        /// </summary>
        public event EventHandler<IRemoteRenderingMachine> PrimaryMachineChanged;

        /// <summary>
        /// Event raised when the status changes.
        /// </summary>
        public event EventHandler<IRemoteRenderingStatusChangedArgs> StatusChanged;
        #endregion IRemoteRenderingService Events

        #region Private Properties

        /// <summary>
        /// Get or set the session status. 
        /// </summary>
        /// <remarks>
        /// Do not make this public, public consumers should use the combines status, or query the primary machine.
        /// </remarks>
        private RenderingSessionStatus SessionStatus
        {
            get => _sessionStatus;

            set
            {
                if (_sessionStatus != value)
                {
                    _sessionStatus = value;
                    UpdateCombinedStatus();
                }
            }
        }

        /// <summary>
        /// Get or set the connection status. 
        /// </summary>
        /// <remarks>
        /// Do not make this public, public consumers should use the combines status, or query the primary machine.
        /// </remarks>
        private ConnectionStatus ConnectionStatus
        {
            get => _connectionStatus;

            set
            {
                if (_connectionStatus != value)
                {
                    _connectionStatus = value;
                    UpdateCombinedStatus();
                }
            }
        }

        /// <summary>
        /// Get the caches current session data
        /// </summary>
        private LastSessionData LastSession
        {
            get
            {
                if (_lastSession != LastSessionData.Empty)
                {
                    return _lastSession;
                }

                string lastSessionString = null;
#if UNITY_EDITOR
                lastSessionString = UnityEditor.EditorPrefs.GetString(_lastSessionKey);
#else
                lastSessionString = PlayerPrefs.GetString(_lastSessionKey);
#endif

                _lastSession = LastSessionData.FromJson(lastSessionString);
                return _lastSession;
            }

            set
            {
                if (_lastSession != value)
                {
#if UNITY_EDITOR
                    UnityEditor.EditorPrefs.SetString(_lastSessionKey, value.ToJson());
#else
                    PlayerPrefs.SetString(_lastSessionKey, value.ToJson());
                    PlayerPrefs.Save();
#endif
                    _lastSession = value;
                }
            }
        }
        #endregion Private Properties

        #region IRemoteRenderingService Methods

        /// <summary>
        /// Reload the settings profile.
        /// </summary>
        public async Task ReloadProfile()
        {
            LoadedProfile = await RemoteRenderingServiceProfileLoader.Load(ConfigurationProfile as BaseRemoteRenderingServiceProfile);
        }

        /// <summary>
        /// Shut down all known machines, and forget about them
        /// </summary>
        public async Task StopAll()
        {
            List<RemoteRenderingMachine> oldMachines = null;

            //
            // First disconnect from current primary machine
            //

            LastSession = LastSessionData.Empty;
            await ClearPrimaryMachine();

            //
            // Next stop all known machines
            //

            lock (_machines)
            {
                oldMachines = new List<RemoteRenderingMachine>(_machines.Count);
                foreach (var machine in _machines)
                {
                    if (machine != null && !machine.IsDisposed)
                    {
                        machine.Session.Stop();
                        oldMachines.Add(machine);
                    }
                }
            }

            //
            // Finally clear machine cache
            //

            lock (_machines)
            {
                foreach (var oldMachine in oldMachines)
                {
                    oldMachine.Dispose();
                    _machines.Remove(oldMachine);
                }
            }
        }

        /// <summary>
        /// Forget the current machine. This will set 'CurrentMachine' to null without shutting it down.
        /// </summary>
        public async Task ClearAll()
        {
            LastSession = LastSessionData.Empty;
            await ClearMachines();
        }

        /// <summary>
        /// Start a new Azure Remote Rendering session. Once created connect must be called on the session.
        /// </summary>
        public async Task<IRemoteRenderingMachine> Create()
        {
            RemoteRenderingClient client = null;
            try
            {
                client = await GetClient();
            }
            catch (DllNotFoundException e)
            {
                var msg = $"Dll not found: {e.Message}";
                AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
                _log.LogError(msg);
            }
            catch (Exception ex)
            {
                var msg = $"Error creating remote rendering client: {ex.Message}";
                AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
                _log.LogError(msg);
                return null;
            }
            Task<CreateRenderingSessionResult> sessionTask;

            string error = null;
            RemoteRenderingMachine result = null;

            try
            {
                if (!string.IsNullOrEmpty(LoadedProfile.UnsafeSizeOverride))
                {
                    var sessionParms = new RenderingSessionCreationOptionsUnsafe(
                        null,
                        LoadedProfile.UnsafeSizeOverride,
                        LoadedProfile.MaxLeaseTimespan.Hours,
                        LoadedProfile.MaxLeaseTimespan.Minutes);

                    sessionTask = client.CreateNewRenderingSessionUnsafeAsync(sessionParms);
                }
                else
                {
                    var sessionParms = new RenderingSessionCreationOptions(
                        null,
                        LoadedProfile.Size,
                        LoadedProfile.MaxLeaseTimespan.Hours,
                        LoadedProfile.MaxLeaseTimespan.Minutes);
                    sessionTask = client.CreateNewRenderingSessionAsync(sessionParms);
                }

                var resultSession = await sessionTask;
                if (resultSession.ErrorCode == Result.Success)
                {
                    result = AddMachine(resultSession.Session);
                }
                else
                {
                    error = $"Failed to create session. Reason: {resultSession.Context.Result.ToString()} - {resultSession.Context.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                error = $"Failed to create session. Exception Reason: {ex?.Message}";
            }

            if (error != null && result == null)
            {
                AppServices.AppNotificationService.RaiseNotification(error, AppNotificationType.Error);
                _log.LogError(error);
            }

            return result;
        }

        /// <summary>
        /// Connect to a known session id. Once created connect must be called on the machine.
        /// </summary>
        public Task<IRemoteRenderingMachine> Open(string id)
        {
            return Open(id, null);
        }

        /// <summary>
        /// Connect to a known session id. Once created connect must be called on the machine.
        /// </summary>
        public async Task<IRemoteRenderingMachine> Open(string id, string domain)
        {
            var client = await GetClient(domain);
            Task<CreateRenderingSessionResult> sessionTask;
            try
            {
                sessionTask = client.OpenRenderingSessionAsync(id);
                var resultSession = await sessionTask;
                return AddMachine(resultSession.Session);
            }
            catch (Exception ex)
            {
                string msg = $"Failed to open session. Reason: {ex.Message}";
                AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
                _log.LogError(msg);
            }

            return null;
        }

        /// <summary>
        /// Connect to an existing or new session if not already connected.
        /// </summary>
        public async Task<IRemoteRenderingMachine> AutoConnect()
        {
            await _initializationTask;

            // If there is already an active auto connect task, return this
            if (_autoConnectTask != null)
            {
                return await _autoConnectTask;
            }

            // Save auto connect task to avoid simultaneous session creations.
            var autoConnectTaskSource = new TaskCompletionSource<IRemoteRenderingMachine>();
            _autoConnectTask = autoConnectTaskSource.Task;

            // Wait for the new session. Then clear the auto-connect task variable, to allow
            // new session creations later.
            IRemoteRenderingMachine result = null;
            try
            {
                result = await ConnectToBestMachine(allowCreation: true);
                autoConnectTaskSource.SetResult(result);
            }
            catch (Exception ex)
            {
                autoConnectTaskSource.SetException(ex);
                throw ex;
            }
            finally
            {
                _autoConnectTask = null;
            }

            return result;
        }

        #endregion IRemoteRenderingService Methods

        #region BaseExtensionService Methods
        public override async void Initialize()
        {
            base.Initialize();
            _isDestroyed = false;
            _initializationTask = InitializeAsync();

            try
            {
                await _initializationTask;
            }
            catch (Exception ex)
            {
                var msg = $"Failed to initialize ARR service. Reason: {ex.Message}";
                AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
                _log.LogError(msg);
            }
        }

        public override void Destroy()
        {
            base.Destroy();

            // Only destroy once
            if (_isDestroyed)
            {
                return;
            }
            _isDestroyed = true;

            // Block for now, however may want to make destroy async.
            ClearMachines().Wait();

            // Cleanup remote rendering clients
            DestroyClient();

            // Cleanup editor stuff
            DestroyEditor();
            if (_editorUpdateTimer != null)
            {
                _editorUpdateTimer.Dispose();
                _editorUpdateTimer = null;
            }
        }

        public override void Update()
        {
            base.Update();

            lock (_machines)
            {
                for (int i = _machines.Count - 1; i >= 0; i--)
                {
                    var machine = _machines[i];
                    if (machine.IsDisposed)
                    {
                        _machines.RemoveAt(i);
                    }

                    machine.Update();
                }
            }

            if (_primaryMachine == null)
            {
                SessionStatus = RenderingSessionStatus.Unknown;
                ConnectionStatus = ConnectionStatus.Disconnected;
            }
            else
            {
                ConnectionStatus = _primaryMachine.Session.Connection.ConnectionStatus;
                SessionStatus = _primaryMachine.Session.Status;
            }
        }
        #endregion BaseExtensionService Methods

        #region Private Methods
        private async Task<RemoteRenderingClient> GetClient(string domain = null)
        {
            if (string.IsNullOrEmpty(domain))
            {
                domain = LoadedProfile.PreferredDomain;
            }

            if (_arrClient == null || _arrClient.Configuration.RemoteRenderingDomain != domain)
            {
                DestroyClient();
                ValidateClientConfiguration();

                _arrClient = await LoadedProfile.GetClient(domain);
            }

            return _arrClient;
        }

        /// <summary>
        /// Validate that the remote rendering client has been configured correctly.
        /// </summary>
        private void ValidateClientConfiguration()
        {
            string error = null;

            LoadedProfile.ValidateProfile(out error);

            if (error != null)
            {
                throw new ApplicationException(error);
            }
        }

        private void DestroyClient()
        {
            if (_arrClient != null)
            {
                _arrClient.Dispose();
                _arrClient = null;
            }
        }

        private void MakePrimaryMachine(RemoteRenderingMachine machine)
        {
            if (_primaryMachine != machine)
            {
                _primaryMachine?.Session.Connection.Disconnect();
                _primaryMachine = machine;
                PrimaryMachineChanged?.Invoke(this, machine);
                UpdateCombinedStatus();
            }

            // Also re-save last session, as the properties may have changed
            // (e.g. size can change from none to big/small once session has finished loading)
            if (_primaryMachine != null)
            {
                LastSession = new LastSessionData()
                {
                    Id = _primaryMachine.Session.Id,
                    PreferredDomain = _primaryMachine.Session.Domain,
                    Size = _primaryMachine.Session.Size
                };
            }
        }

        private async Task ClearPrimaryMachine()
        {
            IRemoteRenderingMachine oldMachine = null;
            if (_primaryMachine != null)
            {
                oldMachine = _primaryMachine;
                _primaryMachine = null;
                PrimaryMachineChanged?.Invoke(this, null);
                UpdateCombinedStatus();
            }

            if (oldMachine != null)
            {
                await oldMachine.Session.Connection.Disconnect();
            }
        }

        private RemoteRenderingMachine AddMachine(RenderingSession arrSession)
        {
            if (arrSession == null)
            {
                return null;
            }

            _log.LogInformation("ARR Session: {0}", arrSession.SessionUuid);

            RemoteRenderingMachine machine = new RemoteRenderingMachine(arrSession, LoadedProfile)
            {
                PrimaryMachineAction = MakePrimaryMachine
            };

            lock (_machines)
            {
                _machines.Add(machine);
            }

            return machine;
        }

        private async Task ClearMachines()
        {
            await ClearPrimaryMachine();

            lock (_machines)
            {
                foreach (var machine in _machines)
                {
                    machine?.Dispose();
                }
                _machines.Clear();
            }
        }

        private void UpdateCombinedStatus()
        {
            _log.LogAssert(UnityEngine.WSA.Application.RunningOnAppThread(), "Not running on app thread.");

            if (_primaryMachine == null)
            {
                Status = RemoteRenderingServiceStatus.NoSession;
            }
            else if (_primaryMachine.Session.Status == RenderingSessionStatus.Error)
            {
                Status = RemoteRenderingServiceStatus.SessionError;
            }
            else if (_primaryMachine.Session.Status == RenderingSessionStatus.Expired)
            {
                Status = RemoteRenderingServiceStatus.SessionExpired;
            }
            else if (_primaryMachine.Session.Status == RenderingSessionStatus.Unknown)
            {
                Status = RemoteRenderingServiceStatus.SessionConstruction;
            }
            else if (_primaryMachine.Session.Status == RenderingSessionStatus.Starting)
            {
                Status = RemoteRenderingServiceStatus.SessionStarting;
            }
            else if (_primaryMachine.Session.Status == RenderingSessionStatus.Stopped)
            {
                Status = RemoteRenderingServiceStatus.SessionStopped;
            }
            else if (_primaryMachine.Session.Status == RenderingSessionStatus.Ready)
            {
                var connectionStatus = _primaryMachine.Session.Connection.ConnectionStatus;
                var connectionError = _primaryMachine.Session.Connection.ConnectionError;
                if (connectionStatus == ConnectionStatus.Connected)
                {
                    Status = RemoteRenderingServiceStatus.SessionReadyAndConnected;
                }
                else if (connectionStatus == ConnectionStatus.Connecting)
                {
                    Status = RemoteRenderingServiceStatus.SessionReadyAndConnecting;
                }
                else if (connectionStatus == ConnectionStatus.Disconnected)
                {
                    if (connectionError == Result.NoConnection || connectionError == Result.Success || connectionError == Result.DisconnectRequest)
                    {
                        Status = RemoteRenderingServiceStatus.SessionReadyAndDisconnected;
                    }
                    else
                    {
                        Status = RemoteRenderingServiceStatus.SessionReadyAndConnectionError;
                    }
                }
            }
        }

        private async Task InitializeAsync()
        {
            _isDestroyed = false;

            await ReloadProfile();

            if (Application.isPlaying)
            {
                RemoteManagerUnity.InitializeManager(new RemoteUnityClientInit(CameraCache.Main));
            }
            else
            {
                _editorUpdateTimer = new Timer(EditorUpdateTimer, null, TimeSpan.Zero, TimeSpan.FromSeconds(1.0 / 60.0));
            }

            InitializeStorage();
            InitializeEditor();
            InitializeLastSessionOverride();

            // Attempt to auto connect to last machine
            IRemoteRenderingMachine lastMachine = null;
            if (Application.isPlaying)
            {
                lastMachine = await ConnectToBestMachine(allowCreation: false);
            }

            // Only update status once we know for sure there is no "last machine"
            if (lastMachine == null)
            {
                UpdateCombinedStatus();
            }
        }

        private void InitializeStorage()
        {
            _storage.Initialize(LoadedProfile.StorageAccountData);
        }

        private void InitializeEditor()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;
            UnityEditor.EditorApplication.projectChanged += EditorApplication_projectChanged;
            UnityEditor.EditorApplication.quitting += EditorApplication_quitting;
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += EditorSceneManager_sceneOpened;
            UnityEditor.Compilation.CompilationPipeline.compilationStarted += CompilationPipeline_compilationStarted;
#endif
        }

        /// <summary>
        /// If a session override was supplied, override the LastSession struct with this data.
        /// </summary>
        private void InitializeLastSessionOverride()
        {
            if (string.IsNullOrEmpty(LoadedProfile.SessionOverride))
            {
                return;
            }

            LastSession = new LastSessionData
            {
                Id = LoadedProfile.SessionOverride,
                PreferredDomain = LoadedProfile.PreferredDomain,
                Size = LoadedProfile.Size
            };
        }

        /// <summary>
        /// Find the best machine session, and attempt to connect to it.
        /// </summary>
        /// <param name="allowCreation">
        /// If true, new remote rendering sessions may be created.
        /// </param>
        /// <remarks>
        /// This tries connecting to a list of machine. The connection attempts occur in the following order;
        /// once a connection is made, no further attempts are made.
        ///
        /// 1) The currently set "PrimaryMachine"
        /// 2) The last used machine from a previous app session (i.e. LastSession)
        /// 3) If allowCreation is true, create a new machine and connect to it.
        /// </summary>
        private async Task<IRemoteRenderingMachine> ConnectToBestMachine(bool allowCreation)
        {
            bool connected = false;
            IRemoteRenderingMachine lastMachine = null;

            // First attempt to connect to the primary machine
            if (PrimaryMachine != null && PrimaryMachine.Session.Status.IsValid())
            {
                lastMachine = PrimaryMachine;
            }

            if (lastMachine != null && lastMachine.Session.Connection.ConnectionStatus == ConnectionStatus.Disconnected)
            {
                try
                {
                    connected = await lastMachine.Session.Connection.Connect();
                }
                catch (Exception ex)
                {
                    var msg = $"Failed to connect to primary machine. Reason: {ex.Message}";
                    AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
                    _log.LogError(msg);
                }

                if (!connected)
                {
                    lastMachine = null;
                }
            }

            // Next, if no connection, connect to the last session id.
            if (lastMachine == null && LastSession.HasId())
            {
                try
                {
                    if (!string.IsNullOrEmpty(LastSession.Id))
                    {
                        lastMachine = await Open(LastSession.Id, LastSession.PreferredDomain);
                    }
                }
                catch (Exception ex)
                {
                    var msg = $"Failed to open last session ({LastSession.Id}). Reason: {ex.Message}.";
                    AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
                    _log.LogWarning(msg);
                }

                if (lastMachine != null)
                {
                    try
                    {
                        connected = await lastMachine.Session.Connection.Connect();
                    }
                    catch (Exception ex)
                    {
                        lastMachine = null;

                        var msg = $"Failed to connect to last session ({LastSession.Id}). Reason: {ex.Message}.";
                        AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
                        _log.LogWarning(msg);
                    }
                }

                if (!connected)
                {
                    lastMachine = null;
                }
            }

            // Finally, if still no connection, create a new session to connect to.
            if (lastMachine == null && allowCreation)
            {
                try
                {
                    lastMachine = await Create();
                }
                catch (Exception ex)
                {
                    var msg = $"Failed to create a new remote machine. Reason: {ex.Message}.";
                    AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
                    _log.LogWarning(msg);
                }

                if (lastMachine != null)
                {
                    try
                    {
                        connected = await lastMachine.Session.Connection.Connect();
                    }
                    catch (Exception ex)
                    {
                        lastMachine = null;

                        var msg = $"Failed to connect a new remote machine. Reason: {ex.Message}.";
                        AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
                        _log.LogWarning(msg);
                    }
                }

                if (!connected)
                {
                    lastMachine = null;
                }
            }

            return lastMachine;
        }

        private void DestroyEditor()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= EditorApplication_playModeStateChanged;
            UnityEditor.EditorApplication.quitting -= EditorApplication_quitting;
            UnityEditor.EditorApplication.projectChanged -= EditorApplication_projectChanged;
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened -= EditorSceneManager_sceneOpened;
            UnityEditor.Compilation.CompilationPipeline.compilationStarted -= CompilationPipeline_compilationStarted;
#endif
        }

        private static async Task WaitForValidStatus(RemoteRenderingMachine machine)
        {
            const int maxRetries = 10;
            int retry = 0;
            while (retry++ < maxRetries && machine != null && machine.Session.Status == RenderingSessionStatus.Unknown)
            {
                await machine.Session.UpdateProperties();
            }
        }

        private void EditorUpdateTimer(object state)
        {
            MainThreadInvoker.Invoke(Update, true);
        }

#if UNITY_EDITOR
        private void EditorApplication_quitting()
        {
            if (!Application.isPlaying)
            {
                Destroy();
            }
        }

        private void CompilationPipeline_compilationStarted(object obj)
        {
            if (!Application.isPlaying)
            {
                Destroy();
            }
        }

        private void EditorApplication_projectChanged()
        {
            if (!Application.isPlaying)
            {
                Destroy();
            }
        }

        private void EditorSceneManager_sceneOpened(UnityEngine.SceneManagement.Scene scene, UnityEditor.SceneManagement.OpenSceneMode mode)
        {
            if (!Application.isPlaying)
            {
                Destroy();
            }
        }

        private void EditorApplication_playModeStateChanged(UnityEditor.PlayModeStateChange obj)
        {
            if (obj == UnityEditor.PlayModeStateChange.ExitingEditMode ||
                obj == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                Destroy();
            }
        }
#endif
        #endregion Private Methods

        #region Private Classes
        /// <summary>
        /// The args used when notifying listeners of a status property change
        /// </summary>
        private class RemoteRenderingSessionStatusChangedArgs : IRemoteRenderingStatusChangedArgs
        {
            public RemoteRenderingSessionStatusChangedArgs(
                RemoteRenderingServiceStatus oldStatus,
                RemoteRenderingServiceStatus newStatus)
            {
                OldStatus = oldStatus;
                NewStatus = newStatus;
            }

            /// <summary>
            /// The old status.
            /// </summary>
            public RemoteRenderingServiceStatus OldStatus { get; }

            /// <summary>
            /// The previous status.
            /// </summary>
            public RemoteRenderingServiceStatus NewStatus { get; }
        }

        /// <summary>
        /// Represents Azure storage APIs for obtain remote models.
        /// </summary>
        private class RemoteRenderingStorage : IRemoteRenderingStorage
        {
            private TaskCompletionSource<bool> _initialized = new TaskCompletionSource<bool>();
            private IRemoteRenderingStorageAccountData _storageAccountData;
            private const string _modelExtension = ".arrAsset";
            private const string _imageExtension = ".png";
            private const string _modelIndexName = "models.xml";
            private static readonly Vector3 _minModelSize = new Vector3(0.5f, 0.5f, 0.5f);
            private static readonly Vector3 _maxModelSize = new Vector3(1.0f, 1.0f, 1.0f);
            private static LogHelper<RemoteRenderingStorage> _log = new LogHelper<RemoteRenderingStorage>();

            public void Initialize(IRemoteRenderingStorageAccountData storageAccountData)
            {
                _storageAccountData = storageAccountData;
                _initialized.TrySetResult(true);
            }

            /// <summary>
            /// Query an Azure container for all Azure Remote Rendering models. This uses the configured storage account id and key.
            /// </summary>
            /// <param name="containerName">
            /// The name of the container to query. If null or empty, the default container name is used.
            /// </param>
            /// <returns>
            /// A list of remote model containers that represent the ARR models within the given container.
            /// </returns>
            public async Task<RemoteContainer[]> QueryModels(string containerName = null)
            {
                await _initialized.Task;

                if (string.IsNullOrEmpty(containerName))
                {
                    containerName = _storageAccountData.DefaultContainer;
                }

                if (!_storageAccountData.IsValid())
                {
                    return null;
                }

                return await Task.Run(() =>
                {
                    return QueryModelsBackgroundWorker(_storageAccountData);
                });
            }

            /// <summary>
            /// Query an Azure container for all Azure Remote Rendering models. This uses the configured storage account id and key.
            /// </summary>
            /// <param name="storageAccountData">
            /// The account data used to retrieve models from storage
            /// </param>
            /// <returns>
            /// A list of remote model containers that represent the ARR models within the given container.
            /// </returns>
            private static async Task<RemoteContainer[]> QueryModelsBackgroundWorker(IRemoteRenderingStorageAccountData storageAccountData)
            {
                HashSet<string> remoteModelUrls  = new HashSet<string>();
                List<RemoteContainer> remoteModelContainers = new List<RemoteContainer>();
                Dictionary<string, string> remoteModelImageUrls = new Dictionary<string, string>();
                string nextMarker = null;

                string authData = await storageAccountData.GetAuthData();
                CloudBlobContainer cloudBlobContainer = null;
                SharedAccessBlobPolicy oneDayReadOnlyPolicy = null;

                // load all model blobs in the container
                do
                {
                    EnumerationResults enumerationResults = null;
                    try
                    {
                        StorageCredentials storageCredentials;
                        if (storageAccountData.AuthType == AuthenticationType.AccessToken)
                        {
                            string modelFolder = storageAccountData.ModelPathByUsername ? AADAuth.SelectedAccount.Username : null;
                            enumerationResults = await ContainerHelper.QueryWithAccessToken(storageAccountData.StorageAccountName, authData, storageAccountData.DefaultContainer, modelFolder, nextMarker);
                            storageCredentials = new StorageCredentials(new TokenCredential(authData));
                        }
                        else
                        {
                            string modelFolder = storageAccountData.ModelPathByUsername ? AKStorageAccountData.MODEL_PATH_BY_USERNAME_FOLDER : null;
                            enumerationResults = await ContainerHelper.QueryWithAccountKey(storageAccountData.StorageAccountName, authData, storageAccountData.DefaultContainer, modelFolder, nextMarker);
                            storageCredentials = new StorageCredentials(storageAccountData.StorageAccountName, authData);
                        }

                        var storageAccount = new CloudStorageAccount(storageCredentials, storageAccountData.StorageAccountName, null, true);
                        var blobClient = storageAccount.CreateCloudBlobClient();
                        cloudBlobContainer = blobClient.GetContainerReference(storageAccountData.DefaultContainer);

                        oneDayReadOnlyPolicy = new SharedAccessBlobPolicy()
                        {
                            SharedAccessExpiryTime = DateTime.UtcNow.AddHours(24),
                            Permissions = SharedAccessBlobPermissions.Read
                        };
                    }
                    catch (Exception ex)
                    {
                        _log.LogError("Failed to load container data. Reason: {0}", ex.Message);
                    }

                    if (enumerationResults != null && enumerationResults.Blobs != null && enumerationResults.Blobs.Length > 0)
                    {
                        // Step 1. Find all the PNG thumbnail images, and cache their blobIndex. These will be applied to model entries.
                        foreach (var blob in enumerationResults.Blobs)
                        {
                            if (string.Equals(Path.GetExtension(blob.Name), _imageExtension, StringComparison.OrdinalIgnoreCase))
                            {
                                // Assume all images will be used, so create a SAS url for all images
                                // The Azure Blob Storage file system uses case sensitive names, so using the raw blob name is fine.
                                remoteModelImageUrls.Add(
                                    Path.ChangeExtension(blob.Name, _modelExtension), 
                                    CreateSasUrl(blob, cloudBlobContainer, oneDayReadOnlyPolicy));
                            }
                        }

                        // Step 2. Load models from the cloud index file, to avoid adding double inserting indexed models during step 3. 
                        foreach (Blob blob in enumerationResults.Blobs)
                        {
                            RemoteModelFile indexFile = await ToModelIndex(storageAccountData, authData, enumerationResults.Container, blob);
                            if (indexFile != null)
                            {
                                AppendContainer(remoteModelContainers, remoteModelUrls, indexFile);
                            }
                        }

                        // Step 3. Add all remaining models that weren't defined in the model file
                        foreach (Blob blob in enumerationResults.Blobs)
                        {
                            RemoteContainer modelContainer = ToModelContainer(enumerationResults.Container, blob);
                            if (modelContainer != null && modelContainer.Items.Length == 1)
                            {
                                RemoteModel item = modelContainer.Items[0] as RemoteModel;
                                if (!string.IsNullOrEmpty(item?.Url) && !remoteModelUrls.Contains(item.Url))
                                {
                                    remoteModelContainers.Add(modelContainer);
                                }
                            }
                        }

                        // Keep loading next set of results
                        nextMarker = enumerationResults.NextMarker;
                    }
                    else
                    {
                        nextMarker = null;
                    }
                } while (!string.IsNullOrEmpty(nextMarker));

                // Step 4. Create SAS urls for set image urls, or apply found images to model containers.
                foreach (var modelContainer in remoteModelContainers)
                {
                    if (!string.IsNullOrEmpty(modelContainer.ImageUrl))
                    {
                        if (AzureStorageHelper.InContainer(modelContainer.ImageUrl, cloudBlobContainer))
                        {
                            modelContainer.ImageUrl = CreateSasUrl(AzureStorageHelper.GetBlobName(modelContainer.ImageUrl), cloudBlobContainer, oneDayReadOnlyPolicy);
                        }
                    }
                    else
                    {
                        foreach (var model in modelContainer.Items)
                        {
                            var remoteModel = model as RemoteModel;
                            if (remoteModel != null &&
                                remoteModelImageUrls.TryGetValue(remoteModel.ExtractBlobPath(), out var imageUrl))
                            {
                                modelContainer.ImageUrl = imageUrl;
                                break;
                            }
                        }
                    }
                }

                return remoteModelContainers.ToArray();
            }

            /// <summary>
            /// Create a sas url for the given blob.
            /// </summary>
            private static string CreateSasUrl(Blob blob, CloudBlobContainer container, SharedAccessBlobPolicy policy)
            {
                return CreateSasUrl(blob.Name, container, policy);
            }

            /// <summary>
            /// Create a sas url for the given blob name
            /// </summary>
            private static string CreateSasUrl(string blobName, CloudBlobContainer container, SharedAccessBlobPolicy policy)
            {
                CloudBlockBlob blockBlob = container.GetBlockBlobReference(blobName);
                string sasBlobToken = blockBlob.GetSharedAccessSignature(policy);
                string sasUrl = blockBlob.Uri.AbsoluteUri + sasBlobToken;
                return sasUrl;
            }

            /// <summary>
            /// Convert an Azure Blob metadata object to the app ModelContainer object.
            /// </summary>
            private static RemoteContainer ToModelContainer(string container, Blob blob)
            {
                if (blob == null || blob.Name == null)
                {
                    return null;
                }

                string modelBlob = blob.Name;
                bool isModelFile = modelBlob.EndsWith(_modelExtension, true, System.Globalization.CultureInfo.InvariantCulture);
                if (!isModelFile)
                {
                    return null;
                }

                int modelBlobPrefixLength = modelBlob.LastIndexOf('/');
                if (modelBlobPrefixLength < 0 || modelBlobPrefixLength >= modelBlob.Length)
                {
                    modelBlobPrefixLength = 0;
                }
                else
                {
                    // skip the slash
                    modelBlobPrefixLength++;
                }

                string modelName = modelBlob.Substring(modelBlobPrefixLength, (modelBlob.Length - _modelExtension.Length) - modelBlobPrefixLength);
                RemoteModel model = new RemoteModel()
                {
                    Name = modelName,
                    Url = $"{container}/{blob.Name}"
                };

                RemoteContainer modelContainer = new RemoteContainer()
                {
                    Name = modelName,
                    Items = new RemoteItemBase[] { model },
                    Transform = new RemoteItemBase.ModelTransform()
                    {
                        Center = true,
                        MaxSize = _maxModelSize,
                        MinSize = _minModelSize
                    }
                };

                return modelContainer;
            }

            /// <summary>
            /// Convert blob to model index file, if possible.
            /// </summary>
            private static async Task<RemoteModelFile> ToModelIndex(IRemoteRenderingStorageAccountData storageAccountData, string authData, string overrideContainer, Blob blob)
            {
                if (blob == null || blob.Name == null)
                {
                    return null;
                }

                string blobName = blob.Name;
                bool isModelIndexFile = blobName.EndsWith(_modelIndexName, StringComparison.InvariantCultureIgnoreCase);
                if (!isModelIndexFile)
                {
                    return null;
                }

                string blobUrl = $"{overrideContainer}/{blobName}";
                RemoteModelFile fileData = null;
                try
                {
                    if (storageAccountData.AuthType == AuthenticationType.AccessToken)
                        fileData = await AzureStorageHelper.GetWithAccessToken<RemoteModelFile>(blobUrl, storageAccountData.StorageAccountName, authData);
                    else
                        fileData = await AzureStorageHelper.GetWithAccountKey<RemoteModelFile>(blobUrl, storageAccountData.StorageAccountName, authData);
                }
                catch (Exception ex)
                {
                    _log.LogWarning("Failed to load data from model index file '{0}'. Reason: {1}", blobUrl, ex.Message);
                }
                return fileData;
            }

            /// <summary>
            /// Append file data containers to target
            /// </summary>
            private static void AppendContainer(List<RemoteContainer> target, HashSet<string> remoteUrls, RemoteModelFile fileData)
            {
                if (target == null || fileData?.Containers == null)
                {
                    return;
                }


                foreach (var container in fileData.Containers)
                {
                    target.Add(container);
                    foreach (var item in container.Items)
                    {
                        if (item is RemoteModel)
                        {
                            remoteUrls.Add(((RemoteModel)item).Url);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Represents a remote rendering machine.
        /// </summary>
        private class RemoteRenderingMachine : IRemoteRenderingMachine
        {
            RenderingSession _arrSession = null;
            RemoteRenderingSession _session = null;
            RemoteRenderingConnection _connection = null;
            RemoteRenderingActions _actions = null;
            RemoteRenderingSessionProperties _properties = null;
            LogHelper<RemoteRenderingMachine> _log = new LogHelper<RemoteRenderingMachine>();
            PoseMode _poseMode = PoseMode.Remote;

            /// <summary>
            /// Create a new machine wrapper from an RenderingSession
            /// </summary>
            public RemoteRenderingMachine(RenderingSession arrSession, IRemoteRenderingServiceProfile profile)
            {
                _arrSession = arrSession ?? throw new ArgumentNullException("ARR Session object can't be null.");
                _properties = new RemoteRenderingSessionProperties(_arrSession);
                _actions = new RemoteRenderingActions(_arrSession);
                _connection = new RemoteRenderingConnection(_arrSession, _properties)
                {
                    AutoReconnect = profile.AutoReconnect,
                    AutoReconnectDelay = TimeSpan.FromSeconds(profile.AutoReconnectRate),
                    ConnectingAction = OnConnection
                };
                _session = new RemoteRenderingSession(_arrSession, _properties, _connection)
                {
                    AutoRenewLease = profile.AutoRenewLease
                };

                ServiceStats = new ServiceStatistics();
            }

            /// <summary>
            /// Get if the machine is disposed
            /// </summary>
            public bool IsDisposed { get; private set; } = false;

            /// <summary>
            /// Get the current session stats.
            /// </summary>
            public ServiceStatistics ServiceStats { get; private set; }

            /// <summary>
            /// Action invoked when this should be made the primary machine
            /// </summary>
            public Action<RemoteRenderingMachine> PrimaryMachineAction { get; set; }

            /// <summary>
            /// The remote rendering actions that can be taken on the remote machine
            /// </summary>
            public IRemoteRenderingActions Actions => _actions;

            /// <summary>
            /// Details about a remote machine, and it's connection status.
            /// </summary>
            public IRemoteRenderingSession Session => _session;

            /// <summary>
            /// Get or set the requested pose mode.
            /// </summary>
            public PoseMode PoseMode
            {
                get => _poseMode;
                set
                {
                    if (_poseMode != value)
                    {
                        _poseMode = value;
                        TryUpdatePoseMode();
                    }
                }
            }

            /// <summary>
            /// Release resources
            /// </summary>
            public void Dispose()
            {
                if (IsDisposed)
                {
                    return;
                }

                IsDisposed = true;

                _connection.Dispose();
                _session.Dispose();

                PrimaryMachineAction = null;
                _arrSession = null;
            }

            /// <summary>
            /// Handle connection requests.
            /// </summary>
            private void OnConnection()
            {
                PrimaryMachineAction?.Invoke(this);
            }

            /// <summary>
            /// Update thee session stats.
            /// </summary>
            public void Update()
            {
                try
                {
                    ServiceStats.Update(_arrSession);
                }
                catch (InvalidOperationException)
                {
                    _log.LogWarning("Couldn't update service stats.");
                }             

                _arrSession.Connection?.Update();
            }

            private void TryUpdatePoseMode()
            {
                if (_arrSession == null || Application.isEditor)
                {
                    return;
                }

                try
                {
                    var result = _arrSession.GraphicsBinding.SetPoseMode(_poseMode);
                    if (result != Result.Success)
                    {
                        var msg = $"Failed to set pose mode. Reason: {result}";
                        AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
                        _log.LogError(msg);
                    }
                }
                catch (Exception ex)
                {
                    var msg = $"Failed to set pose mode. Exception reason: {ex.Message}";
                    AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
                    _log.LogError(msg);
                }
            }
        }

        /// <summary>
        /// A cache of the session properties. This manages updating the cached values.
        /// </summary>
        private class RemoteRenderingSessionProperties : IRemoteRenderingSessionProperties
        {
            private RenderingSession _arrSession = null;
            private TimeSpan _updateDelay = TimeSpan.FromSeconds(15);
            private Task<RenderingSessionProperties> _lastUpdateTask = null;
            private LogHelper<RemoteRenderingSessionProperties> _log = new LogHelper<RemoteRenderingSessionProperties>();

            /// <summary>
            /// Create a property cache for the given arr session.
            /// </summary>
            public RemoteRenderingSessionProperties(RenderingSession arrSession)
            {
                _arrSession = arrSession ?? throw new ArgumentNullException("ARR Session object can't be null.");
            }

            /// <summary>
            /// The current cached properties
            /// </summary>
            public RenderingSessionProperties Value { get; private set; }

            /// <summary>
            /// Get the last updated time
            /// </summary>
            public DateTime LastUpdated { get; private set; } = DateTime.MinValue;

            /// <summary>
            /// Try to update the current settings
            /// </summary>
            public Task<RenderingSessionProperties> TryUpdate()
            {
                if (_lastUpdateTask != null && !_lastUpdateTask.IsCompleted)
                {
                    return _lastUpdateTask;
                }

                _lastUpdateTask = ThrottledUpdate();
                return _lastUpdateTask;
            }

            private async Task<RenderingSessionProperties> ThrottledUpdate()
            {
                DateTime delayUntil = LastUpdated + _updateDelay;
                DateTime utcNow = DateTime.UtcNow;
                if (delayUntil > utcNow)
                {
                    await Task.Delay(delayUntil - utcNow);
                }

                try
                {
                    LastUpdated = DateTime.UtcNow;
                    var result = await _arrSession.GetPropertiesAsync();
                    Value = result.SessionProperties;

                    _updateDelay = TimeSpan.FromSeconds(result.MinimumRetryDelay);
                }
                catch (Exception ex)
                {
                    _updateDelay = TimeSpan.FromSeconds(15);
                    _log.LogWarning("Failed to update session properties. Reason: {0}", ex.Message);
                }

                return Value;
            }
        }

        /// <summary> 
        /// Details about a remote machine, and it's connection status.
        /// </summary>
        private class RemoteRenderingSession : IRemoteRenderingSession
        {
            private RenderingSession _arrSession = null;
            private IRemoteRenderingSessionProperties _properties;
            private bool _autoRenewLease = false;
            private bool _isDisposed = false;
            private TimeSpan _autoRenewBuffer = TimeSpan.FromMinutes(20);
            private TimeSpan _autoRenewProperties = TimeSpan.FromSeconds(15);
            private Timer _expirationTimer;
            private Timer _updatePropertiesTimer;
            LogHelper<RemoteRenderingSession> _log = new LogHelper<RemoteRenderingSession>();

            /// <summary>
            /// Create a new session wrapper from an RenderingSession
            /// </summary>
            public RemoteRenderingSession(RenderingSession arrSession, IRemoteRenderingSessionProperties properties, IRemoteRenderingConnection connection)
            {
                _arrSession = arrSession ?? throw new ArgumentNullException("ARR Session object can't be null.");
                _properties = properties ?? throw new ArgumentNullException("Properties object can't be null.");
                Connection = connection ?? throw new ArgumentNullException("Connection object can't be null.");

                string[] domainParts = null;
				var config = _arrSession.Client.Configuration;
                if (config.RemoteRenderingDomain != null)
                {
                    domainParts = config.RemoteRenderingDomain.Split('.');
                }

                // Assume the location is the first part of the domain
                if (domainParts != null && domainParts.Length > 0)
                {
                    Location = domainParts[0];
                }

                if (string.IsNullOrEmpty(Location))
                {
                    Location = "Unknown";
                }

                Connection.ConnectionStatusChanged +=
                    Connection_ConnectionStatusChanged;
            }

            /// <summary>
            /// Get the location of the session.
            /// </summary>
            public string Location { get; private set; }

            /// <summary>
            /// Get the session domain.
            /// </summary>
            public string Domain => _arrSession.Client.Configuration.RemoteRenderingDomain;

            /// <summary>
            /// Should the session lease auto renew
            /// </summary>
            public bool AutoRenewLease
            {
                get => _autoRenewLease;
                set
                {
                    if (_autoRenewLease != value)
                    {
                        _autoRenewLease = value;
                        InitializeAutoRenewTimer();
                    }
                }
            }

            /// <summary>
            /// The seconds before the session expires when an auto renew will occur.
            /// </summary>
            public TimeSpan AutoRenewBuffer
            {
                get => _autoRenewBuffer;
                set
                {
                    if (_autoRenewBuffer != value)
                    {
                        _autoRenewBuffer = value;
                        InitializeAutoRenewTimer();
                    }
                }
            }

            /// <summary>
            /// Get the session id
            /// </summary>
            public string Id => _arrSession?.SessionUuid ?? string.Empty;

            /// <summary>
            /// Get the host name
            /// </summary>
            public string HostName => _properties.Value.Hostname;

            /// <summary>
            /// Get the last message
            /// </summary>
            public string Message => _properties.Value.Message;

            /// <summary>
            /// Get the session status
            /// </summary>
            /// <remarks>
            /// Assume session is ready if using host name only.
            /// </remarks>
            public RenderingSessionStatus Status => _properties.Value.Status;

            /// <summary>
            /// Get a formatted status message
            /// </summary>
            public string StatusMessage => _properties.Value.Message;

            /// <summary>
            /// Get the session size
            /// </summary>
            public RenderingSessionVmSize Size => _properties.Value.Size;

            /// <summary>
            /// Get the session elapsed time
            /// </summary>
            public TimeSpan ElapsedTime
            {
                get
                {
                    if (_arrSession == null)
                    {
                        return TimeSpan.Zero;
                    }
                    else
                    {
                        TimeSpan elapsed = new TimeSpan(_properties.Value.ElapsedTimeInMinutes / 60, _properties.Value.ElapsedTimeInMinutes % 60, 0);
                        var realElapsedTime = (DateTime.UtcNow - _properties.LastUpdated) + elapsed;
                        var maxLeaseTime = MaxLeaseTime;

                        if (_properties.Value.Status != RenderingSessionStatus.Ready)
                        {
                            return TimeSpan.Zero;
                        }
                        else if (maxLeaseTime < realElapsedTime)
                        {
                            return maxLeaseTime;
                        }
                        else
                        {
                            return realElapsedTime;
                        }
                    }
                }
            }

            /// <summary>
            /// Get the session lease time
            /// </summary>
            public TimeSpan MaxLeaseTime => new TimeSpan(_properties.Value.MaxLeaseInMinutes / 60, _properties.Value.MaxLeaseInMinutes % 60, 0);

            /// <summary>
            /// Get the session expiration time in UTC
            /// </summary>
            public DateTime Expiration
            {
                get
                {
                    if (_properties.Value.Status == RenderingSessionStatus.Ready)
                    {
                        return DateTime.UtcNow + (MaxLeaseTime - ElapsedTime);
                    }

                    return DateTime.MaxValue;

                }
            }

            /// <summary>
            /// Get the connection operations for this session
            /// </summary>
            public IRemoteRenderingConnection Connection { get; private set; }

            /// <summary>
            /// Release resources
            /// </summary>
            public void Dispose()
            {
                Connection.ConnectionStatusChanged -= Connection_ConnectionStatusChanged;
                Connection = null;
                if (_expirationTimer != null)
                {
                    _expirationTimer.Dispose();
                    _expirationTimer = null;
                }
                _isDisposed = true;
            }

            /// <summary>
            /// Sync the session properties from the cloud.
            /// </summary>
            public async Task UpdateProperties()
            {
                if (_isDisposed)
                {
                    return;
                }

                await _properties.TryUpdate();
            }

            /// <summary>
            /// Renew the session so the expiration time is later
            /// </summary>
            public async Task<bool> Renew(TimeSpan increment)
            {
                if (_arrSession == null)
                {
                    return false;
                }

                var newLeaseTime = MaxLeaseTime + increment;

                var result = await _arrSession.RenewAsync(
                    new RenderingSessionUpdateOptions(newLeaseTime.Hours, newLeaseTime.Minutes));

                bool success = result.ErrorCode == Result.Success;
                if (success)
                {
                    await UpdateProperties();
                }

                return success;
            }

            /// <summary>
            /// Stop the session
            /// </summary>
            public Task Stop()
            {
                Connection.Disconnect();

                if (_arrSession == null)
                {
                    return Task.CompletedTask;
                }
                else
                {
                    return _arrSession.StopAsync();
                }
            }

            /// <summary>
            /// Open a web portal to an ARR inspector window
            /// </summary>
            public async Task OpenWebInspector()
            {
                string fileUrl = null;

                if (_arrSession != null)
                {
                    try
                    {
                        fileUrl = await _arrSession.ConnectToArrInspectorAsync();
                    }
                    catch (Exception ex)
                    {
                        var msg = $"Failed to connect to ARR Inspector. Reason: {ex.Message}";
                        AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
                        _log.LogError(msg);
                    }
                }

                if (!string.IsNullOrEmpty(fileUrl))
                {
#if !UNITY_EDITOR && WINDOWS_UWP
                    UnityEngine.WSA.Application.InvokeOnUIThread(async () =>
                    {
                        var file = await StorageFile.GetFileFromPathAsync(fileUrl);
                        bool result = await Launcher.LaunchFileAsync(file);
                        if (!result)
                        {
                            var msg = $"URI '{fileUrl}' failed to launch).";
                            AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
                            _log.LogError(msg);
                        }
                    }, false);
#else
                    Application.OpenURL(fileUrl);
#endif
                }
            }

            /// <summary>
            /// Initialize the auto update timer.
            /// </summary>
            private void InitializeAutoUpdateTimer()
            {
                DateTime updateAt = DateTime.UtcNow + _autoRenewProperties;

                // Create a new timer, or reset the existing one to fire are the new expiration. 
                if (_updatePropertiesTimer == null)
                {
                    _updatePropertiesTimer = new Timer(new TimerCallback(AutoUpdateProperties), null, _autoRenewProperties, Timeout.InfiniteTimeSpan);
                }
                else
                {
                    _updatePropertiesTimer.Change(_autoRenewProperties, Timeout.InfiniteTimeSpan);
                }
            }

            private void AutoUpdateProperties(object timerState)
            {
                MainThreadInvoker.Invoke(async () =>
                {
                    await UpdateProperties();
                    InitializeAutoUpdateTimer();
                }, false);
            }

            /// <summary>
            /// Initialize the auto renewing timer.
            /// </summary>
            private void InitializeAutoRenewTimer()
            {
                if (!CanAutoRenew())
                {
                    return;
                }

                DateTime renewAt = (Expiration - _autoRenewBuffer);
                TimeSpan renewDelay = TimeSpan.Zero;
                if (renewAt > DateTime.UtcNow)
                {
                    renewDelay = renewAt - DateTime.UtcNow;
                }

                // Create a new timer, or reset the existing one to fire are the new expiration. 
                if (_expirationTimer == null)
                {
                    _expirationTimer = new Timer(new TimerCallback(AutoRenewWithDefault), null, renewDelay, Timeout.InfiniteTimeSpan);
                }
                else
                {
                    _expirationTimer.Change(renewDelay, Timeout.InfiniteTimeSpan);
                }
            }

            /// <summary>
            /// Execute the auto renewing of the session.
            /// </summary>
            private void AutoRenewWithDefault(object timerState)
            {
                MainThreadInvoker.Invoke(async () =>
                {
                    const int maxRetry = 10;
                    int retry = 0;
                    while (CanAutoRenew() && retry++ < maxRetry)
                    {
                        if (await Renew(_autoRenewBuffer))
                        {
                            break;
                        }
                    }

                    InitializeAutoRenewTimer();
                }, false);
            }

            /// <summary>
            /// Can we auto renew a session in its current state.
            /// </summary>
            private bool CanAutoRenew()
            {
                return !_isDisposed &&
                    _autoRenewLease &&
                    _autoRenewBuffer > TimeSpan.Zero &&
                    _arrSession != null &&
                    Connection.ConnectionStatus == ConnectionStatus.Connected;
            }

            /// <summary>
            /// Handle connection changes
            /// </summary>
            private void Connection_ConnectionStatusChanged(ConnectionStatus status, Result error)
            {
                InitializeAutoRenewTimer();
            }
        }

        private class RemoteRenderingConnection : IRemoteRenderingConnection
        {
            private RenderingSession _arrSession = null;
            private IRemoteRenderingSessionProperties _properties;
            private bool _isDisposed = false;
            private bool _disconnectRequested = false;
            private bool _hasTriedConnecting = false;
            private bool _autoReconnect = false;
            private Timer _autoReconnectTimer = null;
            private static int _connectionCount;
            private static object _connectionCountLock = new object();
            private static readonly TimeSpan _autoReconnectDelay = TimeSpan.FromSeconds(15);
            private Result _lastConnectError = Result.Success;
            private LogHelper<RemoteRenderingConnection> _log = new LogHelper<RemoteRenderingConnection>();

            /// <summary>
            /// Create a new session wrapper from an RenderingSession
            /// </summary>
            public RemoteRenderingConnection(RenderingSession arrSession, IRemoteRenderingSessionProperties properties)
            {
                _arrSession = arrSession ?? throw new ArgumentNullException("ARR Session object can't be null.");
                _properties = properties ?? throw new ArgumentNullException("Properties object can't be null.");

                _arrSession.ConnectionStatusChanged += AzureSession_ConnectionStatusChanged;
            }

            /// <summary>
            /// Event raised when the connections status changes
            /// </summary>
            public event ConnectionStatusChangedEventHandler ConnectionStatusChanged;

            /// <summary>
            /// An action raised when a connection is requested.
            /// </summary>
            public Action ConnectingAction { get; set; }

            /// <summary>
            /// Should the connection auto reconnect if lost.
            /// </summary>
            public bool AutoReconnect
            {
                get => _autoReconnect;

                set
                {
                    if (_autoReconnect != value)
                    {
                        _autoReconnect = value;
                        InitializeAutoReconnectTimer();
                    }
                }
            }

            /// <summary>
            /// The time in seconds to attempt another reconnect
            /// </summary>
            public TimeSpan AutoReconnectDelay { get; set; } = TimeSpan.FromSeconds(10);

            /// <summary>
            /// Get the connection status
            /// </summary>
            public ConnectionStatus ConnectionStatus
            {
                get
                {
                    if (!IsValid())
                    {
                        return ConnectionStatus.Disconnected;
                    }

                    return _arrSession.ConnectionStatus;
                }
            }

            /// <summary>
            /// Get the last connection error
            /// </summary>
            public Result ConnectionError
            {
                get
                {
                    if (!IsValid())
                    {
                        return Result.NoConnection;
                    }

                    return _lastConnectError;
                }
            }

            /// <summary>
            /// Release resources
            /// </summary>
            public void Dispose()
            {
                if (_isDisposed)
                {
                    return;
                }

                _arrSession.ConnectionStatusChanged -= AzureSession_ConnectionStatusChanged;
                _arrSession = null;

                if (_autoReconnectTimer != null)
                {
                    _autoReconnectTimer.Dispose();
                    _autoReconnectTimer = null;
                }

                ConnectingAction = null;
                _isDisposed = true;
            }

            /// <summary>
            /// Connect to the remote rendering machine.
            /// </summary>
            public Task<bool> Connect()
            {
                return ConnectViaSession();
            }

            /// <summary>
            /// Disconnect from the remote rendering machine.
            /// </summary>
            public Task Disconnect()
            {
                if (!IsValid())
                {
                    return Task.CompletedTask;
                }

                _disconnectRequested = true;
                _hasTriedConnecting = false;

                try
                {
                    _arrSession?.Disconnect();
                }
                catch (Exception ex)
                {
                    var msg = $"Failed to disconnect from runtime. Reason: {ex.Message}";
                    AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
                    _log.LogError(msg);
                }

                if (_arrSession != null)
                {
                    while (_arrSession.IsConnected)
                    {
                        _arrSession.Connection.Update();
                    }
                }

                if (_arrSession == RemoteManagerUnity.CurrentSession)
                {
                    RemoteManagerUnity.CurrentSession = null;
                }

                return Task.CompletedTask;
            }

            /// <summary>
            /// Is this a valid connection object
            /// </summary>
            /// <returns></returns>
            private bool IsValid()
            {
                return !_isDisposed && _arrSession != null;
            }

            /// <summary>
            /// Connect to the remote rendering machine.
            /// </summary>
            private async Task<bool> ConnectViaSession()
            {
                _disconnectRequested = false;
                int connectionId = 0;

                lock (_connectionCountLock)
                {
                    connectionId = ++_connectionCount;
                }

                ConnectingAction?.Invoke();

                try
                {
                    while (!_isDisposed &&
                        (_properties.Value.Status == RenderingSessionStatus.Starting ||
                        _properties.Value.Status == RenderingSessionStatus.Unknown))
                    {
                        // This will throttle the update 
                        await _properties.TryUpdate();

                        if (_properties.Value.Status == RenderingSessionStatus.Error || _properties.Value.Status == RenderingSessionStatus.Expired)
                        {
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.LogWarning("Failed to get rendering properties during connection. Reason: {0}", ex.Message);
                }

                bool tryConnect = false;
                lock (_connectionCountLock)
                {
                    if (_isDisposed)
                    {
                        _log.LogWarning("Ignoring connection request, because object is disposed.");
                    }
                    else if (_connectionCount != connectionId)
                    {
                        _log.LogWarning("Ignoring connection request, because another connection request has been made.");
                    }
                    else if (_properties.Value.Status != RenderingSessionStatus.Ready)
                    {
                        _log.LogWarning("Ignoring connection request, because the session is still not ready ({0}).", _properties.Value.Status);
                    }
                    else if (_disconnectRequested)
                    {
                        _log.LogWarning("Ignoring connection request, because a disconnect request has been made.");
                    }
                    else
                    {
                        // Notify again because session size is now resolved
                        ConnectingAction?.Invoke();
                        _hasTriedConnecting = true;
                        tryConnect = true;
                    }
                }

                if (tryConnect)
                {
                    if (RemoteManagerUnity.CurrentSession != _arrSession)
                    {
                        RemoteManagerUnity.CurrentSession = _arrSession;
                    }

                    try
                    {
                        await _arrSession.ConnectAsync(new RendererInitOptions());
                    }
                    catch (Exception ex)
                    {
                        var msg = $"Failed to connect to session. Reason: {ex.Message}";
                        RRException rre = ex as RRException;
                        if (rre != null)
                        {
                            msg += $" Error code: {rre.ErrorCode.ToString()}";
                        }
                        AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
                        _log.LogError(msg);
                    }
                }

                return ConnectionStatus == ConnectionStatus.Connected;
            }

            /// <summary>
            /// Handle connection changes if this is the current session
            /// </summary>
            /// <param name="status"></param>
            /// <param name="error"></param>
            private void AzureSession_ConnectionStatusChanged(ConnectionStatus status, Result error)
            {
                _lastConnectError = error;

                if (!IsValid())
                {
                    return;
                }

                ConnectionStatusChanged?.Invoke(status, error);

                if (status == ConnectionStatus.Disconnected)
                {
                    InitializeAutoReconnectTimer();
                    if (!(error == Result.Success || error == Result.DisconnectRequest))
                    {
                        var msg = $"Session disconnected unexpectedly. Error: {error}";
                        AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
                        _log.LogError(msg);
                    }
                }
            }

            /// <summary>
            /// Start the auto reconnect timer
            /// </summary>
            private void InitializeAutoReconnectTimer()
            {
                if (!CanAutoReconnect())
                {
                    return;
                }

                if (_autoReconnectTimer == null)
                {
                    _autoReconnectTimer = new Timer(new TimerCallback(AutoReconnectWorker), null, _autoReconnectDelay, TimeSpan.FromMilliseconds(-1));
                }
                else
                {
                    _autoReconnectTimer.Change(_autoReconnectDelay, TimeSpan.FromMilliseconds(-1));
                }
            }

            /// <summary>
            /// Attempt to 
            /// </summary>
            /// <param name="timerState"></param>
            private void AutoReconnectWorker(object timerState)
            {
                MainThreadInvoker.Invoke(async () =>
                {
                    if (!CanAutoReconnect())
                    {
                        return;
                    }

                    bool connected = await Connect();

                    if (!connected)
                    {
                        InitializeAutoReconnectTimer();
                    }
                }, false);
            }

            /// <summary>
            /// Can we auto reconnect a session in its current state.
            /// </summary>
            private bool CanAutoReconnect()
            {
                return IsValid() && _autoReconnect && _hasTriedConnecting && ConnectionStatus == ConnectionStatus.Disconnected;
            }
        }

        /// <summary>
        /// A class containing all the remote rendering actions that can be performed on the remote machine
        /// </summary>
        private class RemoteRenderingActions : IRemoteRenderingActions
        {
            private RenderingSession _arrSession = null;
            private RemoteMaterialCache _materialCache = null;
            private LogHelper<RemoteRenderingActions> _log = new LogHelper<RemoteRenderingActions>();

            /// <summary>
            /// Create a new remote action wrapper from a known host name
            /// </summary>
            public RemoteRenderingActions(string hostName)
            {
                if (string.IsNullOrEmpty(hostName))
                {
                    throw new ArgumentNullException("ARR host name can't be null.");
                }

                _materialCache = new RemoteMaterialCache(this);
            }

            /// <summary>
            /// Create a new remote action wrapper from an RenderingSession
            /// </summary>
            public RemoteRenderingActions(RenderingSession arrSession)
            {
                _arrSession = arrSession ?? throw new ArgumentNullException("ARR Session object can't be null.");
                _materialCache = new RemoteMaterialCache(this);
            }

            /// <summary>
            /// Is this a valid action object
            /// </summary>
            public bool IsValid()
            {
                return _arrSession != null && _arrSession.IsConnected;
            }

            /// <summary>
            /// Asynchronously load a model. This call will return immediately with an object that will emit an event when the model load has completed on the server.
            /// </summary>
            /// <param name="model">The model to load.</param>
            /// <param name="parent">The parent of the model.</param>
            /// <returns></returns>
            public Task<LoadModelResult> LoadModelAsyncAsOperation(RemoteModel model, Entity parent, ModelProgressStatus progress)
            {
                if (!IsValid())
                {
                    return null;
                }

                // For builtin models the SAS load function must be used.
                if (model.Url.StartsWith("builtin://", StringComparison.InvariantCultureIgnoreCase))
                {
                    return _arrSession.Connection.LoadModelFromSasAsync(new LoadModelFromSasOptions(
                        model.Url,
                        parent), progress.OnProgressUpdated);
                }
                else
                {
                    return _arrSession.Connection.LoadModelAsync(LoadModelOptions.CreateForBlobStorage(
                        $"{AppServices.RemoteRendering.LoadedProfile.StorageAccountData.StorageAccountName}.blob.core.windows.net",
                        model.ExtractContainerName() ?? AppServices.RemoteRendering.LoadedProfile.StorageAccountData.DefaultContainer,
                        model.ExtractBlobPath(),
                        parent), progress.OnProgressUpdated);
                }
            }

            /// <summary>
            /// Load model with extended parameters.
            /// </summary>
            /// <param name="model">The model to load.</param>
            /// <param name="parent">The parent of the model.</param>
            /// <returns></returns>
            public Task<LoadModelResult> LoadModelAsync(RemoteModel model, Entity parent)
            {
                if (!IsValid())
                {
                    return Task.FromResult<LoadModelResult>(null);
                }

                // For builtin models the SAS load function must be used.
                if (model.Url.StartsWith("builtin://", StringComparison.InvariantCultureIgnoreCase))
                {
                    return _arrSession.Connection.LoadModelFromSasAsync(new LoadModelFromSasOptions(
                        model.Url,
                        parent), null);
                }
                else
                {
                    return _arrSession.Connection.LoadModelAsync(LoadModelOptions.CreateForBlobStorage(
                        $"{AppServices.RemoteRendering.LoadedProfile.StorageAccountData.StorageAccountName}.blob.core.windows.net",
                        model.ExtractContainerName() ?? AppServices.RemoteRendering.LoadedProfile.StorageAccountData.DefaultContainer,
                        model.ExtractBlobPath(),
                        parent), null);
                }
            }

            /// <summary>
            /// Asynchronously load a texture. This call will return immediately with an object that will emit an event when the texture load has completed on the server.
            /// </summary>
            /// <param name="textureId">String identifier for the texture.</param>
            /// <returns></returns>
            public Task<Remote.Texture> LoadTextureAsync(string storageAccountName, string containerName, string blobPath, TextureType type)
            {
                if (!IsValid())
                {
                    return Task.FromResult<Remote.Texture>(null);
                }

                return _arrSession.Connection.LoadTextureAsync(LoadTextureOptions.CreateForBlobStorage(
                    $"{storageAccountName}.blob.core.windows.net",
                    containerName,
                    blobPath,
                    type));
            }

            /// <summary>
            /// Asynchronously perform a raycast query on the remote scene.  This call will return immediately with an object that will emit an event when the raycast has returned from the server.
            /// The raycast will be performed on the server against the state of the world on the frame that the raycast was issued on.  Results will be sorted by distance, with the closest
            /// intersection to the user being the first item in the array.
            /// </summary>
            /// <param name="cast">Outgoing RayCast.</param>
            /// <returns></returns>
            public Task<RayCastQueryResult> RayCastQueryAsync(RayCast cast)
            {
                if (!IsValid())
                {
                    return Task.FromResult<RayCastQueryResult>(null);
                }

                return _arrSession.Connection.RayCastQueryAsync(cast);
            }

            /// <summary>
            ///  Create a new entity on the server. The new entity can be inserted into the scenegraph and have components added to it.
            /// </summary>
            /// <returns>Newly created entity.</returns>
            public Entity CreateEntity()
            {
                if (!IsValid())
                {
                    return null;
                }

                return _arrSession.Connection.CreateEntity();
            }

            /// <summary>
            ///  Create a new material on the server. The new material can be set to mesh components.
            /// </summary>
            /// <param name="type">Type of created material.</param>
            /// <returns>Newly created material.</returns>
            public Remote.Material CreateMaterial(MaterialType type)
            {
                if (!IsValid())
                {
                    return null;
                }

                return _arrSession.Connection.CreateMaterial(type);
            }


            /// <summary>
            ///  Create a new component locally and on the server. This call can fail if the entity already has a component of componentType on it.
            /// </summary>
            /// <param name="componentType">Component type to create.</param>
            /// <param name="owner">Owner of the component.</param>
            /// <returns>A newly created component or null if the call failed.</returns>
            public ComponentBase CreateComponent(ObjectType componentType, Entity owner)
            {
                if (!IsValid())
                {
                    return null;
                }

                return _arrSession.Connection.CreateComponent(componentType, owner);
            }

            /// <summary>
            /// Returns global camera settings.
            /// </summary>
            public CameraSettings GetCameraSettings()
            {
                if (!IsValid())
                {
                    return default;
                }

                return _arrSession.Connection.CameraSettings;
            }

            /// <summary>
            /// Returns global sky reflection settings.
            /// </summary>
            public SkyReflectionSettings GetSkyReflectionSettings()
            {
                if (!IsValid())
                {
                    return default;
                }

                return _arrSession.Connection.SkyReflectionSettings;
            }

            /// <summary>
            /// Returns global outline settings.
            /// </summary>
            public OutlineSettings GetOutlineSettings()
            {
                if (!IsValid())
                {
                    return default;
                }

                return _arrSession.Connection.OutlineSettings;
            }

            /// <summary>
            /// Returns global z-fighting mitigation state.
            /// </summary>
            public ZFightingMitigationSettings GetZFightingMitigationSettings()
            {
                if (!IsValid())
                {
                    return default;
                }

                return _arrSession.Connection.ZFightingMitigationSettings;
            }

            /// <summary>
            /// Load a remote material, from a data object.
            /// </summary>
            /// <remarks>
            /// Move this to material factory...Session should know about RemoteMaterial, as it leads to a circular data flow.
            /// </remarks>
            public Task<Remote.Material> LoadMaterial(RemoteMaterial material)
            {
                if (!IsValid())
                {
                    return null;
                }

                return _materialCache.LoadMaterial(material);
            }

            /// <summary>
            /// Load a 2D texture.
            /// </summary>
            public async Task<Remote.Texture> LoadTexture2D(string url)
            {
                try
                {
                    return await LoadTexture(url, Remote.TextureType.Texture2D);
                }
                catch (Exception ex)
                {
                    _log.LogError("Failed to load 2D texture '{0}'. Reason: {1}", url, ex.Message);
                    return null;
                }
            }

            /// <summary>
            /// Load a cube map texture.
            /// </summary>
            public async Task<Remote.Texture> LoadTextureCubeMap(string url)
            {
                try
                {
                    return await LoadTexture(url, Remote.TextureType.CubeMap);
                }
                catch (Exception ex)
                {
                    _log.LogError("Failed to load cube map texture '{0}'. Reason: {1}", url, ex.Message);
                    return null;
                }
            }

            /// <summary>
            /// Load a texture.
            /// </summary>
            private Task<Remote.Texture> LoadTexture(string url, Remote.TextureType type)
            {
                if (string.IsNullOrEmpty(url))
                {
                    return null;
                }

                if (!IsValid())
                {
                    return null;
                }

                return _arrSession.Connection.LoadTextureFromSasAsync(new LoadTextureFromSasOptions(url, type));
            }

            /// <summary>
            /// Load and set cube map
            /// </summary>
            public async Task SetLighting(string url)
            {
                if (!IsValid())
                {
                    return;
                }

                if (string.IsNullOrEmpty(url))
                {
                    return;
                }

                Remote.Texture texture = null;
                try
                {
                    texture = await LoadTexture(url, Remote.TextureType.CubeMap);
                }
                catch (Exception ex)
                {
                    _log.LogError("Failed to load lighting texture '{0}'. Reason: {1}", url, ex.Message);
                }

                if (texture != null)
                {
                    var skySettings = GetSkyReflectionSettings();
                    skySettings.SkyReflectionTexture = texture;
                }
            }
        }

        [Serializable]
        private struct LastSessionData
        {
            public string Id;
            public string PreferredDomain;
            public RenderingSessionVmSize Size;

            public static LastSessionData Empty = new LastSessionData()
            {
                Id = null,
                PreferredDomain = "",
                Size = RenderingSessionVmSize.None
            };

            public static bool operator ==(LastSessionData v1, LastSessionData v2)
            {
                return v1.Id == v2.Id && v1.PreferredDomain == v2.PreferredDomain && v1.Size == v2.Size;
            }

            public static bool operator !=(LastSessionData v1, LastSessionData v2)
            {
                return !(v1 == v2);
            }

            public override bool Equals(object obj)
            {
                if (!(obj is LastSessionData))
                {
                    return false;
                }

                return ((LastSessionData)obj) == this;
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }

            public string ToJson()
            {
                return JsonUtility.ToJson(this);
            }

            public static LastSessionData FromJson(string json)
            {
                try
                {
                    return JsonUtility.FromJson<LastSessionData>(json);
                }
                catch
                {
                    return Empty;
                }
            }

            public bool HasId()
            {
                return !string.IsNullOrEmpty(Id);
            }
        }
        #endregion Private Classes
    }
}
