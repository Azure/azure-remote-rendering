// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.SpatialAnchors;
using Microsoft.Azure.SpatialAnchors.Unity;
using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// This finds, deletes, and creates Azure Spatial Anchors. The Azure Spatial Anchors found by this class are wrapped inside an IAppAnchor name.
    /// </summary>
    [MixedRealityExtensionService(SupportedPlatforms.WindowsStandalone|SupportedPlatforms.WindowsUniversal|SupportedPlatforms.WindowsEditor)]
	public class AnchoringService : BaseExtensionService, IAnchoringService, IMixedRealityExtensionService
    {
        private object _startStopWatchLock = new object();
        private AnchoringServiceProfile _defaultProfile;
        private Task<AnchoringServiceProfile> _loadedProfile;
        private SynchronizationContext _appContext;
        private CloudSpatialAnchorWatcher _anchorWatcher;
        private Task<bool> _connecting;
        private CancellationTokenSource _connectingTokenSource;
        private Dictionary<string, FindEntry> _pendingFinds = new Dictionary<string, FindEntry>();
        private LogHelper<AnchoringService> _log = new LogHelper<AnchoringService>();
        private float _updateWatcherAtSecondsSinceStartup = float.MaxValue;
        private float _disconnectSessionAtSecondsSinceStartup = float.MaxValue;
        private const float _updateWatcherDelayInSeconds = 3;
        private const float _disconnectSessionDelayInSeconds = 3;
        private int _locks;
        private bool _hasAccount;
        private TaskCompletionSource<bool> _serviceInit = new TaskCompletionSource<bool>();
        private Dictionary<string, CloudSpatialAnchor> _tracked = new Dictionary<string, CloudSpatialAnchor>();

        public AnchoringService(string name, uint priority, BaseMixedRealityProfile profile) :
            base(name, priority, profile)
        {
            _defaultProfile = profile as AnchoringServiceProfile;
            if (_defaultProfile == null)
            {
                _defaultProfile = ScriptableObject.CreateInstance<AnchoringServiceProfile>();
            }
            _log.Verbose = _defaultProfile.VerboseLogging ? LogHelperState.Always : LogHelperState.Default;
            _loadedProfile = LoadProfile();
        }

        #region IAnchoringService Properties
        /// <summary>
        /// Get the current anchor manager used to find, delete, and create Azure Spatial Anchors. 
        /// </summary>
        public SpatialAnchorManager AnchorManager { get; private set; }

        /// <summary>
        /// Get the current location provider being used to determine the anchors near a device.
        /// </summary>
        public PlatformLocationProvider LocationProvider { get; private set; }

        /// <summary>
        /// The number of active searches.
        /// </summary>
        public int ActiveSearchesCount => _pendingFinds.Count;

        /// <summary>
        /// Get if the service is currently searching for cloud anchors in the real-world.
        /// </summary>
        public bool IsSearching => _pendingFinds.Count > 0;

        /// The number of active anchor creations
        /// </summary>
        public int ActiveCreationsCount { get; private set; }

        /// <summary>
        /// Get if the service is currently creating new cloud anchors.
        /// </summary>
        public bool IsCreating => ActiveCreationsCount > 0;

        /// <summary>
        /// Get or set the current find options. After setting this, the following Find() operations will use these settings.
        /// </summary>
        public AnchoringServiceFindOptions FindOptions { get; set; }

        /// <summary>
        /// Can cloud anchors be created.
        /// </summary>
        public bool IsCloudEnabled => IsNativeEnabled && _hasAccount;

        /// <summary>
        /// Can native anchors be created
        /// </summary>
        public bool IsNativeEnabled => AnchorSupport.IsNativeEnabled;
        #endregion IAnchoringService Properties

        #region Public Properties
        /// <summary>
        /// An anchor property field that describes the time the app updated the anchor
        /// </summary>
        public static string AppUpdateTimeField = "AppUpdateTime";
        #endregion Public Properties

        #region IAnchoringService Events
        /// <summary>
        /// Event raised when the SearchesCount value has changed.
        /// </summary>
        public event Action<IAnchoringService, AnchoringServiceSearchingArgs> ActiveSearchesCountChanged;

        /// <summary>
        /// Event raised when the ActiveCreationsCount value has changed.
        /// </summary>
        public event Action<IAnchoringService, AnchoringServiceCreatingArgs> ActiveCreationsCountChanged;
        #endregion IAnchoringService Events

        #region IMixedRealityExtensionService Methods
        /// <summary>
        /// Initialize the service. This will create a new SpatialAnchorManager if one doesn't exist.
        /// </summary>
        public override async void Initialize()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            LogVerbose("Initializing");
            _appContext = SynchronizationContext.Current;

            if (AnchorManager == null)
            {
                AnchorManager = MixedRealityPlayspace.Transform.GetComponentInChildren<SpatialAnchorManager>();
            }

            if (AnchorManager == null)
            {
                GameObject anchorManagerObject = new GameObject("SpatialAnchorManager");
                MixedRealityPlayspace.AddChild(anchorManagerObject.transform);
                anchorManagerObject.SetActive(false);
                AnchorManager = anchorManagerObject.AddComponent<SpatialAnchorManager>();
            }

            LogVerbose("Initializing. Loading profile.");
            AnchorManager.enabled = false;
            AnchorManager.gameObject.SetActive(false);
            var asaProfile = await _loadedProfile;

            LogVerbose("Initializing. Loaded profile. IsCouldEnabled:{0} IsNativeEnabled:{1}", IsCloudEnabled, IsNativeEnabled);
            AnchorManager.SpatialAnchorsAccountId = asaProfile.AnchorAccountId;
            AnchorManager.SpatialAnchorsAccountKey = asaProfile.AnchorAccountKey;
            AnchorManager.SpatialAnchorsAccountDomain = asaProfile.AnchorAccountDomain;
            AnchorManager.AuthenticationMode = AuthenticationMode.ApiKey;
            AnchorManager.gameObject.SetActive(IsCloudEnabled);
            AnchorManager.enabled = IsCloudEnabled;

            AnchorManager.SessionCreated += AnchorManagerSessionCreateOrChanged;
            AnchorManager.SessionChanged += AnchorManagerSessionCreateOrChanged;
            AnchorManager.AnchorLocated += AnchorLocated;

            if (LocationProvider == null && !Application.isEditor)
            {
                LocationProvider = new PlatformLocationProvider();
                LocationProvider.Sensors.BluetoothEnabled = false;
                LocationProvider.Sensors.GeoLocationEnabled = true;
                LocationProvider.Sensors.WifiEnabled = true;
            }

            _serviceInit.SetResult(true);

            // Auto disable this service if anchoring is not available.
            if (!IsCloudEnabled)
            {
                Disable();
            }
        }

        /// <summary>
        /// Update watcher or disconnect session if needed
        /// </summary>
        public override void LateUpdate()
        {
            if (_updateWatcherAtSecondsSinceStartup != float.MaxValue &&
                Time.realtimeSinceStartup >= _updateWatcherAtSecondsSinceStartup)
            {
                _updateWatcherAtSecondsSinceStartup = float.MaxValue;
                UpdateWatcher();
            }

            if (_disconnectSessionAtSecondsSinceStartup != float.MaxValue &&
                Time.realtimeSinceStartup >= _disconnectSessionAtSecondsSinceStartup)
            {
                _disconnectSessionAtSecondsSinceStartup = float.MaxValue;
                DisconnectSession();
            }
        }

        /// <summary>
        /// Destroy the service, and disconnect from the Azure Spatial Anchor service.
        /// </summary>
        public override void Destroy()
        {
            if (AnchorManager != null)
            {
                AnchorManager.SessionCreated -= AnchorManagerSessionCreateOrChanged;
                AnchorManager.SessionChanged -= AnchorManagerSessionCreateOrChanged;
                AnchorManager.AnchorLocated -= AnchorLocated;
            }

            foreach (var entry in _pendingFinds)
            {
                entry.Value?.TaskCompletionSource?.TrySetCanceled();
            }
            _pendingFinds.Clear();

            DisconnectSessionWorker();
        }
        #endregion IMixedRealityExtensionService Methods

        #region IAnchoringService Methods
        /// <summary>
        /// Wait for the anchoring service to initialize
        /// </summary>
        public async Task<bool> IsReady()
        {
            await _serviceInit.Task;
            return IsCloudEnabled;
        }

        /// <summary>
        /// Start finding cloud spatial anchor, once found returned task is compelted.
        /// </summary>
        public Task<CloudSpatialAnchor> Find(string cloudSpatialAnchorId)
        {
            return Find(cloudSpatialAnchorId, CancellationToken.None);
        }

        /// <summary>
        /// Start finding cloud spatial anchor, once found returned task is compelted.
        /// </summary>
        public Task<CloudSpatialAnchor> Find(string cloudSpatialAnchorId, CancellationToken cancellationToken)
        {
            LogVerbose("Find(string) called. (anchor id: {0})", cloudSpatialAnchorId);
            if (cloudSpatialAnchorId == AnchorSupport.EmptyAnchorId)
            {
                return Task.FromResult<CloudSpatialAnchor>(null);
            }
            else
            {
                FindEntry findEntry;
                if (!_pendingFinds.TryGetValue(cloudSpatialAnchorId, out findEntry))
                {
                    _pendingFinds[cloudSpatialAnchorId] = findEntry = new FindEntry(cloudSpatialAnchorId);
                }
                HandleFindRequest(findEntry, cancellationToken);
                return findEntry.TaskCompletionSource.Task;
            }
        }

        /// <summary>
        /// Start finding cloud spatial anchor, once found returned task is compelted.
        /// </summary>
        /// <param name="cancellationToken">Cancel the search by setting this cancellation token.</param>
        public Task<CloudSpatialAnchor> FindNearest(CancellationToken cancellationToken)
        {
            LogVerbose("FindNearest() called");

            FindEntry findEntry;
            if (!_pendingFinds.TryGetValue(string.Empty, out findEntry))
            {
                _pendingFinds[string.Empty] = findEntry = new FindEntry();
                HandleFindRequest(findEntry, cancellationToken);
            }

            return findEntry.TaskCompletionSource.Task;
        }

        /// <summary>
        /// Start finding cloud spatial anchor, once found returned task is compelted.
        /// </summary>
        /// <param name="cancellationToken">Cancel the search by setting this cancellation token.</param>
        /// <param name="timeoutForFirstInSeconds">The timeout for finding at least one anchor.</param>
        /// <param name="timeoutForOthersInSeconds">After the first anchor is found, the timeout for finding all other anchors.</param>
        public async Task<CloudSpatialAnchor[]> FindAll(string[] cloudSpatialAnchorIds, float timeoutForFirstInSeconds, float timeoutForOthersInSeconds, CancellationToken cancellationToken)
        {
            LogVerbose("FindAll(string[]) called (1st timeout: {0}) (2nd timeout {1}) ({2})", timeoutForFirstInSeconds, timeoutForOthersInSeconds, cloudSpatialAnchorIds);
            List<Task<CloudSpatialAnchor>> tasks = new List<Task<CloudSpatialAnchor>>();
            CancellationTokenSource firstFindCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            CancellationTokenSource otherFindCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            foreach (var cloudSpatialAnchorId in cloudSpatialAnchorIds)
            {
                FindEntry findEntry;
                if (!_pendingFinds.TryGetValue(cloudSpatialAnchorId, out findEntry))
                {
                    LogError("New find entry ({0}).", cloudSpatialAnchorId);
                    _pendingFinds[cloudSpatialAnchorId] = findEntry = new FindEntry(cloudSpatialAnchorId);
                }
                HandleFindRequest(findEntry, otherFindCancellation.Token);
                tasks.Add(findEntry.TaskCompletionSource.Task);
            }

            try
            {
                firstFindCancellation.CancelAfter(TimeSpan.FromSeconds(timeoutForFirstInSeconds));
                await Task.WhenAny(tasks.ToArray()).WithCancellation(firstFindCancellation.Token);

                otherFindCancellation.CancelAfter(TimeSpan.FromSeconds(timeoutForOthersInSeconds));
                await Task.WhenAll(tasks.ToArray()).WithCancellation(otherFindCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                LogError("FindAll was canceled.");
            }
            catch (Exception ex)
            {
                LogError("Failed to find all anchors. Exception: {0}", ex);
            }
            finally
            {
                // cancel other finds, if firstFindCancellation was canceled
                if (!otherFindCancellation.IsCancellationRequested)
                {
                    otherFindCancellation.Cancel();
                }

                firstFindCancellation.Dispose();
                otherFindCancellation.Dispose();
            }

            CloudSpatialAnchor[] result = new CloudSpatialAnchor[tasks.Count];
            for (int i = 0; i < tasks.Count; i++)
            {
                var currentTask = tasks[i];
                if (currentTask.Status == TaskStatus.RanToCompletion)
                {
                    result[i] = currentTask.Result;
                }
            }

            return result;
        }


        /// <summary>
        /// Save the given cloud spatial anchor
        /// </summary>
        public async Task<string> Save(CloudSpatialAnchor cloudSpatialAnchor)
        {
            LogVerbose("Save(CloudSpatialAnchor) called.");

            if (cloudSpatialAnchor == null)
            {
                LogVerbose("Unable to save spatial anchor, as the given anchor is null.");
                return null;
            }

            StartSaving();

            bool connectedAndLocked = false;
            try
            {
                connectedAndLocked = await ConnectAndLockSession();
                if (connectedAndLocked)
                {
                    cloudSpatialAnchor.AppProperties[AppUpdateTimeField] = DateTime.UtcNow.ToString();

                    if (_defaultProfile.AnchorExpirationInDays > 0)
                    {
                        cloudSpatialAnchor.Expiration = DateTimeOffset.UtcNow + TimeSpan.FromDays(_defaultProfile.AnchorExpirationInDays);
                        LogVerbose("Anchor expiration set to {0}", cloudSpatialAnchor.Expiration);
                    }

                    if (string.IsNullOrEmpty(cloudSpatialAnchor.Identifier) || cloudSpatialAnchor.Identifier == AnchorSupport.EmptyAnchorId)
                    {
                        LogVerbose("Creating a new Azure Spatial Anchor.");
                        await AnchorManager.Session.CreateAnchorAsync(cloudSpatialAnchor);
                        LogVerbose("Created a new Azure Spatial Anchor. (anchor id: {0})", cloudSpatialAnchor.Identifier);
                    }
                    else
                    {
                        LogVerbose("Updating an Azure Spatial Anchor. (anchor id: {0})", cloudSpatialAnchor.Identifier);
                        await AnchorManager.Session.UpdateAnchorPropertiesAsync(cloudSpatialAnchor);
                        LogVerbose("Updated an Azure Spatial Anchor. (anchor id: {0})", cloudSpatialAnchor.Identifier);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("Failed to save or update Azure Spatial Anchor.\r\nException: {0}", ex);
            }
           
            if (connectedAndLocked)
            {
                UnlockSession();
            }

            CompletedSaving();

            return cloudSpatialAnchor.Identifier;
        }

        /// <summary>
        /// Save the given cloud spatial anchor
        /// </summary>
        public async void Delete(CloudSpatialAnchor cloudSpatialAnchor)
        {
            LogVerbose("Delete(CloudSpatialAnchor) called.");

            if (cloudSpatialAnchor == null || string.IsNullOrEmpty(cloudSpatialAnchor.Identifier))
            {
                LogVerbose("Unable to delete spatial anchor, as the given anchor is null.");
                return;
            }

            bool connectedAndLocked = false;
            try
            {
                connectedAndLocked = await ConnectAndLockSession();
                if (connectedAndLocked)
                {
                    string id = cloudSpatialAnchor.Identifier;
                    LogVerbose("Deleting an Azure Spatial Anchor. (anchor id: {0})", id);
                    await AnchorManager.Session.DeleteAnchorAsync(cloudSpatialAnchor);
                    LogVerbose("Deleted an Azure Spatial Anchor. (anchor id: {0})", id);
                }
            }
            catch (Exception ex)
            {
                LogError("Failed to delete Azure Spatial Anchor.\r\nException: {0}", ex);
            }

            if (connectedAndLocked)
            {
                UnlockSession();
            }
        }

        /// <summary>
        /// Extract the time the app service updated this anchor.
        /// </summary>
        public DateTime UpdateTime(CloudSpatialAnchor cloudSpatialAnchor)
        {
            string timeString = null;
            cloudSpatialAnchor?.AppProperties.TryGetValue(AppUpdateTimeField, out timeString);

            DateTime result;
            if (!DateTime.TryParse(timeString, out result))
            {
                result = DateTime.MinValue;
            }

            return result;
        }
        #endregion IAnchoringService Methods

        #region Private Methods
        /// <summary>
        /// Load the profile
        /// </summary>
        private async Task<AnchoringServiceProfile> LoadProfile()
        {
            var loadedProfile = await AnchoringServiceProfileLoader.Load(_defaultProfile); ;
            _hasAccount = !string.IsNullOrEmpty(loadedProfile.AnchorAccountId);
            return loadedProfile;
        }

        /// <summary>
        /// Handle a new find request, and manage the lifetime of this additional find request.
        /// </summary>
        private async void HandleFindRequest(FindEntry findEntry, CancellationToken cancellationToken)
        {
            _log.LogAssert(_appContext == SynchronizationContext.Current, "HandleFind must be called on the main Unity app thread.");
            LogVerbose("Searching for anchor. (anchor id: {0})", findEntry.AnchorId);

            findEntry.Count++;

            // If the first find request, kick of internal search for the anchor
            if (findEntry.Count == 1)
            {
                InvalidateWatcher();

                // Notify listeners of the count change
                ActiveSearchesCountChanged?.Invoke(this, new AnchoringServiceSearchingArgs(_pendingFinds.Count));
            }

            Task findTask;
            if (_defaultProfile.SearchTimeout >= 0)
            {
                findTask = Task.WhenAny(
                    findEntry.TaskCompletionSource.Task,
                    Task.Delay(TimeSpan.FromSeconds(_defaultProfile.SearchTimeout)));
            }
            else
            {
                findTask = findEntry.TaskCompletionSource.Task;
            }

            try
            {
                await findTask.WithCancellation(cancellationToken);
                LogVerbose("Find operation completed or timed out. (anchor id: {0}) (found: {1})", 
                    findEntry.AnchorId, findEntry.TaskCompletionSource.Task.IsCompleted);
            }
            catch (TaskCanceledException)
            {
                LogVerbose("Find was canceled. (anchor id: {0})", findEntry.AnchorId);
            }
            catch (Exception)
            {
                // ignore other failures.
            }

            // If nothing is wanting to find this anchor id, remove it from the searches.
            if (--findEntry.Count == 0 && _pendingFinds.Remove(findEntry.AnchorId))
            {
                LogVerbose("No more find requests for this anchor. (anchor id: {0})", findEntry.AnchorId);

                // If search hasn't completed, notify task listeners of the cancelation.
                findEntry.TaskCompletionSource?.TrySetCanceled();

                // Notify listeners of the count change
                ActiveSearchesCountChanged?.Invoke(this, new AnchoringServiceSearchingArgs(_pendingFinds.Count));
            }
            else
            {
                LogVerbose("There are still active find requests for this anchor. (anchor id: {0}) (requests: {1})", findEntry.AnchorId, findEntry.Count);
            }

            // If there are no more find requests, invalidate the watcher to stop it.
            if (_pendingFinds.Count == 0)
            {
                InvalidateWatcher();
            }
        }

        /// <summary>
        /// Connect to an azure spatial anchor session.
        /// </summary>
        private Task<bool> ConnectSession()
        {
            if (_connecting != null)
            {
                return _connecting;
            }

            if (AnchorManager == null)
            {
                LogVerbose("Failed to start anchor session. There is no anchor manager.");
                return Task.FromResult(false);
            }

            if (AnchorManager.IsSessionStarted)
            {
                LogVerbose("Session already started.");
                return Task.FromResult(true);
            }

            _connectingTokenSource?.Dispose();
            _connectingTokenSource = new CancellationTokenSource();
            return _connecting = ConnectSessionWorker(_connectingTokenSource.Token);
        }

        /// <summary>
        /// Connect to an azure spatial anchor session, and lock it so to prevent it from being disconnected.
        /// </summary>
        private async Task<bool> ConnectAndLockSession()
        {
            LockSession();
            bool result = await ConnectSession();
            if (!result)
            {
                UnlockSession();
            }
            return result;
        }

        /// <summary>
        /// Disconnect from an azure spatial anchor session, if there are no locks
        /// </summary>
        private void DisconnectSession()
        {
            if (_locks == 0)
            {
                LogVerbose($"DisconnectSession() disconnecting session ({_locks})");
                _connectingTokenSource?.Cancel();
                AnchorManager?.StopSession();
                _connecting = null;
            }
        }

        /// <summary>
        /// Lock the current session, and prevent it from being disconnected.
        /// </summary>
        private void LockSession()
        {
            int locks = Interlocked.Increment(ref _locks);
            LogVerbose($"LockSession() Locked session ({locks})");
        }

        /// <summary>
        /// Unlock the current session, and disconnect it if needed.
        /// </summary>
        private void UnlockSession()
        {
            int locks = Interlocked.Decrement(ref _locks);
            if (locks < 0)
            {
                locks = Interlocked.Increment(ref _locks);
            }

            LogVerbose($"UnlockSession() Unlocked session ({locks})");
            InvalidateSession();
        }

        /// <summary>
        /// Connect to an azure spatial anchor session.
        /// </summary>
        private async Task<bool> ConnectSessionWorker(CancellationToken cancellationToken)
        {
            LogVerbose($"ConnectSessionWorker() entered.");
            if (!IsCloudEnabled)
            {
                return false;
            }

            // make sure service is initialized
            await _serviceInit.Task;

            try
            {
                if (!Application.isEditor && !cancellationToken.IsCancellationRequested)
                {
                    LogVerbose("Starting anchor manager's session.");
                    await AnchorManager.StartSessionAsync();

                    if (AnchorManager.IsSessionStarted)
                    {
                        LogInformation("Connected to anchor session '{0}'", AnchorManager.Session?.SessionId);
                    }
                    else
                    {
                        LogError("Failed to start anchor session.");
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("Failed to start anchor session.\r\nException: {0}", ex);
            }

            return AnchorManager.IsSessionStarted;
        }

        /// <summary>
        /// Handle session creations or changes and set location provider
        /// </summary>
        private void AnchorManagerSessionCreateOrChanged(object sender, EventArgs e)
        {
            _log.LogVerbose("Anchor manager session was created or changed.");
            if (AnchorManager.Session != null && LocationProvider != null)
            {
                _log.LogVerbose("Setting location provider on session.");
                AnchorManager.Session.LocationProvider = LocationProvider;
            }
        }

        /// <summary>
        /// Disconnect from an azure spatial anchor session.
        /// </summary>
        private void DisconnectSessionWorker()
        {
            LogVerbose("DisconnectSessionWorker() called.");
            _locks = 0;
            _connectingTokenSource?.Cancel();
            AnchorManager?.StopSession();
            _connecting = null;
        }

        /// <summary>
        /// Schedule a check to see if the session should be disconnected after a short delay. 
        /// The delay is to avoid disconnect and immediate reconnects.
        /// </summary>
        private void InvalidateSession()
        {
            LogVerbose("InvalidateSession() called.");
            _disconnectSessionAtSecondsSinceStartup = Time.realtimeSinceStartup + _disconnectSessionDelayInSeconds;
        }


        /// <summary>
        /// Schedule the watcher to be updated after a short delay. The delay is to avoid
        /// immediate restarts of anchor queries.
        /// </summary>
        private void InvalidateWatcher()
        {
            LogVerbose("InvalidateWatcher() called.");
            _updateWatcherAtSecondsSinceStartup = Time.realtimeSinceStartup + _updateWatcherDelayInSeconds;
        }

        /// <summary>
        /// Attempt to start watching for the current set anchor ids stored inside _pendingFinds. If _pendingFinds is
        /// empty, stop the active watcher
        /// </summary>
        private async void UpdateWatcher()
        {
            LogVerbose("UpdateWatcher() called.");

            // If there are no active searches, verify there is not an active watcher. 
            if (_pendingFinds.Count == 0)
            {
                LogVerbose("There are no pending searches.");
                StopWatching();
                return;
            }

            // Connect to session if needed
            await ConnectSession();

            // Verify a session was created
            if (AnchorManager?.Session == null)
            {
                LogVerbose("There is no active anchor session.");
                return;
            }

            lock (_startStopWatchLock)
            {
                // Lock if no watcher. Old watcher would have locked already
                if (_anchorWatcher == null)
                {
                    LockSession();
                }
                else
                {
                    DestroyWatcher(_anchorWatcher);
                }

                AnchorLocateCriteria locateCriteria = CreateDefaultWatcherSettings();
                AddNearDeviceSettings(locateCriteria);
                AddAnchorIdSettings(locateCriteria);
                AddBypassCacheSettings(locateCriteria);
                _anchorWatcher = CreateWatcher(locateCriteria);

                // Free lock if watch creation failed
                if (_anchorWatcher == null)
                {
                    UnlockSession();
                }
            }
        }

        /// <summary>
        /// Create the default watcher settings for the given anchor ids.
        /// </summary>
        private AnchorLocateCriteria CreateDefaultWatcherSettings()
        {
            return new AnchorLocateCriteria()
            {
                RequestedCategories = AnchorDataCategory.Spatial,
                Strategy = LocateStrategy.AnyStrategy
            };
        }

        private void AddAnchorIdSettings(AnchorLocateCriteria locateCriteria)
        {
            if (locateCriteria.NearDevice == null)
            {
                var anchorIds = _pendingFinds.Keys.ToArray();
                _log.LogVerbose("Seaching for anchor ids. (count: {0}) {1}", anchorIds.Length, anchorIds);
                _log.LogAssert(anchorIds.Length <= 35, "Unable to search for more than 35 anchors at a time.");
                locateCriteria.Identifiers = anchorIds;
            }
        }

        /// <summary>
        /// Insert 'Near Device' settings if enabled.
        /// </summary>
        private void AddNearDeviceSettings(AnchorLocateCriteria locateCriteria)
        {
            if (_pendingFinds.ContainsKey(string.Empty))
            {
                _log.LogVerbose("Key's contain a wildcard. Searching for near device anchors");
                locateCriteria.NearDevice = new NearDeviceCriteria();
            }
            else if (FindOptions.NearDevice != null)
            {
                locateCriteria.NearDevice = new NearDeviceCriteria();
            }

            if (locateCriteria.NearDevice != null)
            {
                _log.LogVerbose("Searching for near device anchors");

                if (FindOptions.MaxDistanceInMeters != null)
                {
                    _log.LogVerbose("Setting NearDevice.DistanceInMeters {0}", FindOptions.MaxDistanceInMeters.Value);
                    locateCriteria.NearDevice.DistanceInMeters = FindOptions.MaxDistanceInMeters.Value;
                }

                if (FindOptions.MaxNearResults != null)
                {
                    _log.LogVerbose("Setting NearDevice.MaxResultCount {0}", FindOptions.MaxNearResults.Value);
                    locateCriteria.NearDevice.MaxResultCount = FindOptions.MaxNearResults.Value;
                }
            }
        }

        /// <summary>
        /// Insert 'Bypass Cache' settings if enabled.
        /// </summary>
        private void AddBypassCacheSettings(AnchorLocateCriteria locateCriteria)
        {
            if (FindOptions.BypassCache != null)
            {
                locateCriteria.BypassCache = FindOptions.BypassCache.Value;
            }
        }

        /// <summary>
        /// Stop the current anchor watcher.
        /// </summary>
        private void StopWatching()
        {
            lock (_startStopWatchLock)
            {
                // Destroy and free lock that was created during start watching
                if (_anchorWatcher != null)
                {
                    LogVerbose("Stopping anchor watcher.");
                    DestroyWatcher(_anchorWatcher);
                    _anchorWatcher = null;
                    UnlockSession();
                }
            }
        }

        /// <summary>
        /// Create a new anchor watcher
        /// </summary>
        private CloudSpatialAnchorWatcher CreateWatcher(AnchorLocateCriteria anchorLocateCriteria)
        {
            LogVerbose("Creating anchor watcher.");
            CloudSpatialAnchorWatcher result = null;
            try
            {
                result = AnchorManager.Session.CreateWatcher(anchorLocateCriteria);
            }
            catch (Exception ex)
            {
                LogError("Failed to create anchor watcher.\r\nException: {0}", ex);
            }
            return result;
        }

        /// <summary>
        /// Create a old anchor watcher
        /// </summary>
        private void DestroyWatcher(CloudSpatialAnchorWatcher watcher)
        {
            if (watcher == null)
            {
                return;
            }

            LogVerbose("Destroying anchor watcher.");
            try
            {
                watcher.Stop();
            }
            catch (Exception ex)
            {
                LogError("Failed to destroy anchor watcher.\r\nException: {0}", ex);
            }
        }

        /// <summary>
        /// Handle a new anchor being located.
        /// </summary>
        private void AnchorLocated(object sender, AnchorLocatedEventArgs args)
        {
            _appContext.Send(contextState =>
            {
                LogVerbose("Anchor located event raised. (anchor id: {0}) (anchor status: {1}) (anchor strategy: {2})", args.Identifier, args.Status, args.Strategy);
                
                CloudSpatialAnchor anchor = null;
                if (args.Status == LocateAnchorStatus.Located)
                {
                    anchor = args.Anchor;
                }
                else if (args.Status == LocateAnchorStatus.AlreadyTracked)
                {
                    if (!_tracked.TryGetValue(args.Identifier, out anchor))
                    {
                        anchor = args.Anchor;
                    }
                }

                if (anchor != null)
                {
                    _tracked[args.Identifier] = anchor;

                    FindEntry findEntry;
                    if (_pendingFinds.TryGetValue(args.Identifier, out findEntry))
                    {
                        LogVerbose("Anchor located, and notifying listeners. (anchor id: {0}) (anchor status: {1}) (anchor strategy: {2})", args.Identifier, args.Status, args.Strategy);
                        findEntry.TaskCompletionSource.TrySetResult(anchor);
                    }

                    if (_pendingFinds.TryGetValue(string.Empty, out findEntry))
                    {
                        LogVerbose("Wildcard anchor located, and notifying listeners. (anchor id: {0}) (anchor status: {1}) (anchor strategy: {2})", args.Identifier, args.Status, args.Strategy);
                        findEntry.TaskCompletionSource.TrySetResult(anchor);
                    }
                }
            }, null);
        }

        /// <summary>
        /// Invoked at the start of saving an anchor to the cloud.
        /// </summary>
        private void StartSaving()
        {
            _appContext.Send(contextState =>
            {
                ActiveCreationsCount++;
                ActiveCreationsCountChanged?.Invoke(this, new AnchoringServiceCreatingArgs(ActiveCreationsCount));
            }, null);
        }

        /// <summary>
        /// Invoked at the end of saving an anchor to the cloud.
        /// </summary>
        private void CompletedSaving()
        {
            _appContext.Send(contextState =>
            {
                if (ActiveCreationsCount > 0)
                {
                    ActiveCreationsCount--;
                    ActiveCreationsCountChanged?.Invoke(this, new AnchoringServiceCreatingArgs(ActiveCreationsCount));
                }
            }, null);
        }

        /// <summary>
        /// Log a message if information logging is enabled.
        /// </summary>
        private void LogInformation(string message, params object[] args)
        {
            _log.LogInformation(message, args);
        }

        /// <summary>
        /// Log a message if verbose logging is enabled.
        /// </summary>
        private void LogVerbose(string message)
        {
            _log.LogVerbose(message);
        }

        /// <summary>
        /// Log a message if verbose logging is enabled. 
        /// </summary>
        private void LogVerbose(string messageFormat, params object[] args)
        {
            _log.LogVerbose(messageFormat, args);
        }

        /// <summary>
        /// Log a message if verbose logging is enabled.
        /// </summary>
        private void LogError(string message)
        {
            _log.LogError(message);
        }

        /// <summary>
        /// Log a message if verbose logging is enabled. 
        /// </summary>
        private void LogError(string messageFormat, params object[] args)
        {
            _log.LogError(messageFormat, args);
        }
        #endregion Private Methods

        #region Private Class
        /// <summary>
        /// Represent a search for a cloud anchor.
        /// </summary>
        private class FindEntry
        {
            public FindEntry(string anchorId)
            {
                AnchorId = anchorId;
            }

            /// <summary>
            /// Create an entry with no anchor ids, meaning find any anchor near the device.
            /// </summary>
            public FindEntry()
            {
                AnchorId = string.Empty;
            }

            /// <summary>
            /// The anchor id being searched for.
            /// </summary>
            public string AnchorId { get; }

            /// <summary>
            /// The number of active "find" requests for this anchor.
            /// </summary>
            public int Count { get; set; }

            /// <summary>
            /// The task completion source that will single when an anchor has been found, or search cancelled.
            /// </summary>
            public TaskCompletionSource<CloudSpatialAnchor> TaskCompletionSource { get; } = new TaskCompletionSource<CloudSpatialAnchor>();
        }

        #endregion Private Class
    }
}
