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
        private bool _verboseLogging = false;
        private AnchoringServiceProfile _defaultProfile;
        private AnchoringServiceProfile _loadedProfile;
        private SynchronizationContext _appContext;
        private CloudSpatialAnchorWatcher _anchorWatcher;
        private Task<bool> _connecting;
        private CancellationTokenSource _connectingTokenSource;
        private Dictionary<string, FindEntry> _pendingFinds = new Dictionary<string, FindEntry>();

        public AnchoringService(string name, uint priority, BaseMixedRealityProfile profile) :
            base(name, priority, profile)
        {
            _defaultProfile = profile as AnchoringServiceProfile;
            if (_defaultProfile == null)
            {
                _defaultProfile = ScriptableObject.CreateInstance<AnchoringServiceProfile>();
            }
            _verboseLogging = _defaultProfile.VerboseLogging;
        }

        #region IAnchoringService Properties
        /// <summary>
        /// Get the current anchor manager used to find, delete, and create Azure Spatial Anchors. 
        /// </summary>
        public SpatialAnchorManager AnchorManager { get; private set; }

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
        #endregion IAnchoringService Properties

        #region Public Properties
        /// <summary>
        /// An id representing an anchor that hasn't been saved to the cloud yet.
        /// </summary>
        public static string EmptyAnchorId => Guid.Empty.ToString();
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
        public override void Initialize()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            _appContext = SynchronizationContext.Current;

            if (AnchorManager == null)
            {
                AnchorManager = MixedRealityPlayspace.Transform.GetComponentInChildren<SpatialAnchorManager>();
            }

            if (AnchorManager == null)
            {
                GameObject anchorManagerObject = new GameObject("SpatialAnchorManager");
                anchorManagerObject.SetActive(false);
                AnchorManager = anchorManagerObject.AddComponent<SpatialAnchorManager>();
                AnchorManager.enabled = false;
                MixedRealityPlayspace.AddChild(anchorManagerObject.transform);
                anchorManagerObject.SetActive(true);
            }

            AnchorManager.AnchorLocated += AnchorLocated;
        }

        /// <summary>
        /// Destroy the service, and disconnect from the Azure Spatial Anchor service.
        /// </summary>
        public override void Destroy()
        {
            AnchorManager.AnchorLocated -= AnchorLocated;
            foreach (var entry in _pendingFinds)
            {
                entry.Value?.TaskCompletionSource?.TrySetCanceled();
            }
            _pendingFinds.Clear();

            DisconnectSession();
        }
        #endregion IMixedRealityExtensionService Methods

        #region IAnchoringService Methods
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

            FindEntry findEntry;
            if (!_pendingFinds.TryGetValue(cloudSpatialAnchorId, out findEntry))
            {
                _pendingFinds[cloudSpatialAnchorId] = findEntry = new FindEntry(cloudSpatialAnchorId);
            }

            HandleFindRequest(findEntry, cancellationToken);
            return findEntry.TaskCompletionSource.Task;
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

            try
            {
                if (await ConnectSession())
                {
                    if (string.IsNullOrEmpty(cloudSpatialAnchor.Identifier) || cloudSpatialAnchor.Identifier == EmptyAnchorId)
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
                LogErrorVerbose("Failed to save or update Azure Spatial Anchor.\r\nException: {0}", ex);
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

            if (cloudSpatialAnchor == null)
            {
                LogVerbose("Unable to delete spatial anchor, as the given anchor is null.");
                return;
            }

            try
            {
                if (await ConnectSession())
                {
                    string id = cloudSpatialAnchor.Identifier;
                    LogVerbose("Deleting an Azure Spatial Anchor. (anchor id: {0})", id);
                    await AnchorManager.Session.DeleteAnchorAsync(cloudSpatialAnchor);
                    LogVerbose("Deleted an Azure Spatial Anchor. (anchor id: {0})", id);
                }
            }
            catch (Exception ex)
            {
                LogErrorVerbose("Failed to delete  Azure Spatial Anchor.\r\nException: {0}", ex);
            }
        }
        #endregion IAnchoringService Methods

        #region Private Methods
        /// <summary>
        /// Handle a new find request, and manage the lifetime of this additional find request.
        /// </summary>
        private async void HandleFindRequest(FindEntry findEntry, CancellationToken cancellationToken)
        {
            Debug.Assert(_appContext == SynchronizationContext.Current, "HandleFind must be called on the main Unity app thread.");
            findEntry.Count++;

            // If the first find request, kick of internal search for the anchor
            if (findEntry.Count == 1)
            {
                TryWatching();

                // Notify listeners of the count change
                ActiveSearchesCountChanged?.Invoke(this, new AnchoringServiceSearchingArgs(_pendingFinds.Count));
            }

            Task findTask;
            if (_defaultProfile.SearchTimeout >= 0)
            {
                findTask = Task.WhenAny(findEntry.TaskCompletionSource.Task, Task.Delay(TimeSpan.FromSeconds(_defaultProfile.SearchTimeout)));
            }
            else
            {
                findTask = findEntry.TaskCompletionSource.Task;
            }

            try
            {
                await findTask.WithCancellation(cancellationToken);
                LogVerbose("Found anchor. (anchor id: {0})", findEntry.AnchorId);
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

                // In order for this anchor id search to stop, we have to restart all searches.
                TryWatching();

                // Notify listeners of the count change
                ActiveSearchesCountChanged?.Invoke(this, new AnchoringServiceSearchingArgs(_pendingFinds.Count));
            }
            else
            {
                LogVerbose("There are still active find requests for this anchor. (anchor id: {0}) (requests: {1})", findEntry.AnchorId, findEntry.Count);
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

            _connectingTokenSource = new CancellationTokenSource();
            return _connecting = ConnectSessionWorker(_connectingTokenSource.Token);
        }

        /// <summary>
        /// Connect to an azure spatial anchor session.
        /// </summary>
        private async Task<bool> ConnectSessionWorker(CancellationToken cancellationToken)
        {
            LogVerbose("ConnectSessionWorker() called.");

            var fileData = await AnchoringServiceProfileLoader.Load(_defaultProfile);
            AnchorManager.SpatialAnchorsAccountId = fileData.AnchorAccountId;
            AnchorManager.SpatialAnchorsAccountKey = fileData.AnchorAccountKey;
            AnchorManager.SpatialAnchorsAccountDomain = fileData.AnchorAccountDomain;
            AnchorManager.AuthenticationMode = AuthenticationMode.ApiKey;
            AnchorManager.enabled = true;

            try
            {
                if (!Application.isEditor && !cancellationToken.IsCancellationRequested)
                {
                    LogVerbose("Starting anchor manager's session.");
                    await AnchorManager.StartSessionAsync();
                    LogVerbose("Started anchor manager's session.");
                }
            }
            catch (Exception ex)
            {
                LogErrorVerbose("Failed to start anchor session.\r\nException: {0}", ex);
            }

            return AnchorManager.IsSessionStarted;
        }

        /// <summary>
        /// Disconnect from an azure spatial anchor session.
        /// </summary>
        private void DisconnectSession()
        {
            LogVerbose("DisconnectSession() called.");
            _connectingTokenSource?.Cancel();
            AnchorManager?.StopSession();
            _connecting = null;
        }

        /// <summary>
        /// Attempt to start watching for the current set anchor ids stored inside _pendingFinds.
        /// </summary>
        private async void TryWatching()
        {
            LogVerbose("TryWatching() called.");

            await ConnectSession();
            if (AnchorManager?.Session == null)
            {
                LogVerbose("There is no active anchor session.");
                return;
            }

            StopWatching();
            if (_pendingFinds.Count == 0)
            {
                LogVerbose("There no pending searches.");
                return;
            }

            AnchorLocateCriteria anchorLocateCriteria = new AnchorLocateCriteria()
            {
                Identifiers = _pendingFinds.Keys.ToArray<string>(),
                RequestedCategories = AnchorDataCategory.Spatial,
                Strategy = LocateStrategy.AnyStrategy
            };

            LogVerbose("Starting anchor searches.");
            _anchorWatcher = AnchorManager.Session.CreateWatcher(anchorLocateCriteria);
        }

        /// <summary>
        /// Stop the current anchor watcher.
        /// </summary>
        private void StopWatching()
        {
            LogVerbose("StopWatching() called.");

            if (_anchorWatcher != null)
            {
                LogVerbose("Stopping anchor searches.");
                _anchorWatcher.Stop();
                _anchorWatcher = null;
            }
        }

        /// <summary>
        /// Handle a new anchor being located.
        /// </summary>
        private void AnchorLocated(object sender, AnchorLocatedEventArgs args)
        {
            LogVerbose("AnchorLocated() called. (anchor id: {0})", args.Identifier);

            _appContext.Send(contextState =>
            {
                FindEntry appAnchor;
                if (_pendingFinds.TryGetValue(args.Identifier, out appAnchor))
                {
                    LogVerbose("Anchor located, and notifying listeners. (anchor id: {0})", args.Identifier);
                    appAnchor.TaskCompletionSource.TrySetResult(args.Anchor);
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
        /// Log a message if verbose logging is enabled.
        /// </summary>
        private void LogVerbose(string message)
        {
            if (_verboseLogging)
            {
                Debug.LogFormat(LogType.Log, LogOption.None, null, $"[{nameof(AnchoringService)}] {message}");
            }
        }

        /// <summary>
        /// Log a message if verbose logging is enabled. 
        /// </summary>
        private void LogVerbose(string messageFormat, params object[] args)
        {
            if (_verboseLogging)
            {
                Debug.LogFormat(LogType.Log, LogOption.None, null, $"[{nameof(AnchoringService)}] {messageFormat}", args);
            }
        }

        /// <summary>
        /// Log a message if verbose logging is enabled.
        /// </summary>
        private void LogErrorVerbose(string message)
        {
            if (_verboseLogging)
            {
                Debug.LogFormat(LogType.Error, LogOption.None, null, $"[{nameof(AnchoringService)}] {message}");
            }
        }

        /// <summary>
        /// Log a message if verbose logging is enabled. 
        /// </summary>
        private void LogErrorVerbose(string messageFormat, params object[] args)
        {
            if (_verboseLogging)
            {
                Debug.LogFormat(LogType.Error, LogOption.None, null, $"[{nameof(AnchoringService)}] {messageFormat}", args);
            }
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
