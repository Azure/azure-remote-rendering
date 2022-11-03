// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.OpenXR;
using Microsoft.MixedReality.OpenXR.ARSubsystems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// A helper behavior for loading and saving anchors to device.
/// </summary>
public class ARAnchorStore : MonoBehaviour
{
    private Dictionary<TrackableId, TaskCompletionSource<ARAnchor>> _pendingLoads = new Dictionary<TrackableId, TaskCompletionSource<ARAnchor>>();
    private LogHelper<ARAnchorStore> _log = new LogHelper<ARAnchorStore>();
    private static object _instanceLock = new object();
    private static ARAnchorStore _instance = null;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The anchor manager used to load anchors into.")]
    private ARAnchorManager anchorManager = null;

    /// <summary>
    /// The anchor manager used to load anchors into.
    /// </summary>
    public ARAnchorManager AnchorManager
    {
        get => anchorManager;
        set
        {
            if (anchorManager != value)
            {
                UnregisterAnchorManagerEvents();
                anchorManager = value;
                RegisterAnchorManagerEvents();
            }
        }
    }
    #endregion Serialized Fields

    #region MonoBehavior Methods
    private void Awake()
    {
        RegisterAnchorManagerEvents();
        EnsureAnchorManager();
    }

    /// <summary>
    /// Free resources.
    /// </summary>
    private void OnDestroy()
    {
        if (anchorManager != null)
        {
            anchorManager.anchorsChanged -= OnAnchorsChanged;
        }

        List<TaskCompletionSource<ARAnchor>> _cancelLoads = new List<TaskCompletionSource<ARAnchor>>();
        lock (_pendingLoads)
        {
            foreach (var pending in _pendingLoads)
            {
                _cancelLoads.Add(pending.Value);
            }
            _pendingLoads.Clear();
        }

        foreach (var cancel in _cancelLoads)
        {
            cancel.TrySetCanceled();
        }
    }
    #endregion MonoBehavior Methods

    #region Public Properties
    public static ARAnchorStore Instance
    { 
        get
        {
            ARAnchorStore instance = null;
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<ARAnchorStore>();
                }

                instance = _instance;
            }
            return instance;
        }
    }
    #endregion Public Properties

    #region Public Methods
    /// <summary>
    /// Load an anchor from the anchor store.
    /// </summary>
    public async Task<ARAnchor> LoadAnchor(string name, CancellationToken ct)
    {
        ARAnchor anchor = null;
        XRAnchorStore store = await LoadAnchorStore();
        if (store == null)
        {
            LogError("Unable to load anchor '{0}'. There is no anchor store.", name);
        }
        else if (store.PersistedAnchorNames == null ||
            !store.PersistedAnchorNames.Contains(name))
        {
            LogVerbose("Unable to load anchor '{0}'. The anchor store does not contain anchor name.", name);
        }
        else if (ct.IsCancellationRequested)
        {
            LogVerbose("Unable to load anchor '{0}'. Operation cancelled.", name);
        }
        else
        {
            LogVerbose("Loading anchor '{0}'", name);
            TrackableId anchorId = store.LoadAnchor(name);
            if (TrackableId.invalidId != anchorId)
            {
                anchor = await LoadAnchor(anchorId);
                if (anchor == null)
                {
                    LogError("Failed to load anchor '{0}:{1}'", name, anchorId);
                }
                else
                {
                    await WaitForTracked(anchor, TimeSpan.FromSeconds(30));
                    LogVerbose("Loaded anchor '{0}:{1}' @ {2} ({3})", name, anchorId, anchor.transform.position, anchor.trackingState);
                }
            }
            else
            {
                LogError("Failed to load anchor '{0}'. Invalid trackable id.", name);
            }
        }
        return anchor;
    }

    /// <summary>
    /// Create an anchor from an existing anchor, and save an anchor to the anchor store. If force is true, old anchors will be deleted first.
    /// </summary>
    public async Task<ARAnchor> CopyAndSaveAnchor(string name, ARAnchor copy, bool force, CancellationToken ct)
    {
        ARAnchor anchor = null;
        if (copy != null)
        {
            anchor = await CreateAndSaveAnchor(name, copy.transform, force, ct);
        }
        return anchor;
    }

    /// <summary>
    /// Create an anchor from a transform, and save an anchor to the anchor store. If force is true, old anchors will be deleted first.
    /// </summary>
    public async Task<ARAnchor> CreateAndSaveAnchor(string name, Transform transform, bool force, CancellationToken ct)
    {
        ARAnchor anchor = null;
        if (transform != null)
        {
            GameObject anchorObject = new GameObject();
            anchorObject.transform.SetPositionAndRotation(transform.position, transform.rotation);
            anchor = anchorObject.AddComponent<ARAnchor>();
        }

        await SaveAnchor(name, anchor, force, ct);
        if (ct.IsCancellationRequested)
        {
            return null;
        }
        else
        {
            return anchor;
        }
    }

    /// <summary>
    /// Save an anchor to the anchor store. If force is true, old anchors will be deleted first.
    /// </summary>
    public async Task SaveAnchor(string name, ARAnchor anchor, bool force, CancellationToken ct)
    {
        if (anchor == null)
        {
            LogError("Unable to save anchor '{0}'. Anchor is null.", name);
        }
        else
        {
            var anchorStoreTask = LoadAnchorStore();
            await WaitForTracked(anchor, TimeSpan.FromSeconds(30));

            XRAnchorStore store = await anchorStoreTask;
            if (store == null)
            {
                LogVerbose("Unable to save anchor '{0}:{1}' @ {2}. There is no anchor store.", name, anchor.trackableId, anchor.transform.position);
            }
            else if (anchor == null)
            {
                LogVerbose("Unable to save anchor '{0}'. Anchor has been destroyed.", name);
            }
            else if (ct.IsCancellationRequested)
            {
                LogVerbose("Unable to save anchor '{0}:{1}' @ {2}. Operation cancelled.", name, anchor.trackableId, anchor.transform.position);
            }
            else
            {
                if (force &&
                    store.PersistedAnchorNames != null &&
                    store.PersistedAnchorNames.Contains(name))
                {
                    store.UnpersistAnchor(name);
                }

                LogVerbose("Saving anchor '{0}:{1}' @ {2} ({3})", name, anchor.trackableId, anchor.transform.position, anchor.trackingState);
                if (store.TryPersistAnchor(anchor.trackableId, name))
                {
                    LogVerbose("Saved anchor '{0}:{1}' @ {2} ({3})", name, anchor.trackableId, anchor.transform.position, anchor.trackingState);
                }
                else
                {
                    LogError("Failed to save anchor '{0}:{1}' @ {2} ({3})", name, anchor.trackableId, anchor.transform.position, anchor.trackingState);
                }
            }
        }
    }
    #endregion Public Methods

    #region Private Methods
    private async Task WaitForTracked(ARAnchor anchor, TimeSpan timeout)
    {
        if (anchor == null)
        {
            return;
        }

        await Task.Run(() =>
        {
            var stopAt = DateTimeOffset.UtcNow + timeout; 
            while (stopAt > DateTimeOffset.UtcNow &&
                anchor != null &&
                anchor.trackingState != TrackingState.Tracking)
            {
                Thread.Sleep(TimeSpan.FromSeconds(3));                
            }
        });
    }

    private void EnsureAnchorManager()
    {
        if (anchorManager == null)
        {
            anchorManager = UnityEngine.Object.FindObjectOfType<ARAnchorManager>();
            RegisterAnchorManagerEvents();
        }
    }

    private void RegisterAnchorManagerEvents()
    {
        if (anchorManager != null)
        {
            anchorManager.anchorsChanged += OnAnchorsChanged;
        }
    }

    private void UnregisterAnchorManagerEvents()
    {
        if (anchorManager != null)
        {
            anchorManager.anchorsChanged -= OnAnchorsChanged;
        }
    }

    private void OnAnchorsChanged(ARAnchorsChangedEventArgs args)
    {
        if (args.added != null)
        {
            foreach (var added in args.added)
            {
                lock (_pendingLoads)
                {
                    if (_pendingLoads.TryGetValue(added.trackableId, out TaskCompletionSource<ARAnchor> taskSource))
                    {
                        _pendingLoads.Remove(added.trackableId);
                        taskSource.TrySetResult(added);
                    }
                }
            }
        }
    }

    private Task<XRAnchorStore> LoadAnchorStore()
    {
        EnsureAnchorManager();

        if (anchorManager == null)
        {
            _log.LogError("Failed to load anchor store. Could not find anchor manager.");
            return Task.FromResult<XRAnchorStore>(null);
        }
        else if (anchorManager.subsystem == null)
        {
            _log.LogError("Failed to load anchor store. Could not find anchor sub system.");
            return Task.FromResult<XRAnchorStore>(null);
        }
        else
        {
            return anchorManager.subsystem.LoadAnchorStoreAsync();
        }
    }

    private Task<ARAnchor> LoadAnchor(TrackableId trackerId)
    {
        EnsureAnchorManager();

        Task<ARAnchor> result;
        lock (_pendingLoads)
        {
            if (trackerId == TrackableId.invalidId)
            {
                result = Task.FromException<ARAnchor>(new InvalidOperationException("Tracker id was invalid"));
            }
            else if (_pendingLoads.TryGetValue(trackerId, out TaskCompletionSource<ARAnchor> taskSource))
            {
                result = taskSource.Task;
            }
            else 
            {
                ARAnchor existing = null;
                if (anchorManager != null)
                {
                    existing = anchorManager.GetAnchor(trackerId);
                }

                if (existing == null)
                {
                    taskSource = new TaskCompletionSource<ARAnchor>();
                    _pendingLoads[trackerId] = taskSource;
                    result = taskSource.Task;
                }
                else
                {
                    result = Task.FromResult(existing);
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Log a message if error logging is enabled. 
    /// </summary>
    private void LogVerbose(string messageFormat, params object[] args)
    {
        _log.LogVerbose(messageFormat, args);
    }

    /// <summary>
    /// Log a message if error logging is enabled. 
    /// </summary>
    private void LogError(string messageFormat, params object[] args)
    {
        _log.LogError(messageFormat, args);
    }
    #endregion Private Methods
}
