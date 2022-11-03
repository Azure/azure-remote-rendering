// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A class for helping to load filterable data into the app's list repeater
/// </summary>
public abstract class ListDataLoaderBase : MonoBehaviour
{
    private LogHelper<ListDataLoaderBase> _logger = new LogHelper<ListDataLoaderBase>();
    private CancellationTokenSource _cancellationTokenSource = null;
    private ListDataLoaderState _state = ListDataLoaderState.Unknown;

    #region Serialized Fields
    [Header("Parts Settings")]

    [SerializeField]
    [Tooltip("The list target for the loaded data.")]
    private ListItemRepeater target = null;

    /// <summary>
    /// The list target for the loaded data.
    /// </summary>
    public ListItemRepeater Target
    {
        get => target;
        set => target = value;
    }

    [Header("Data Settings")]

    [SerializeField]
    [Tooltip("Should this loader insert an action button to the top of the resulting list.")]
    public bool insertTopActionButton = false;

    /// <summary>
    /// Should this loader insert an action button to the top of the resulting list.
    /// </summary>
    private bool InsertTopActionButton
    {
        get => insertTopActionButton;
        set => insertTopActionButton = value;
    }

    [SerializeField]
    [Tooltip("The primary label for the top action.")]
    public string topActionPrimaryLabel = null;

    /// <summary>
    /// The primary label for the top action.
    /// </summary>
    private string TopActionPrimaryLabel
    {
        get => topActionPrimaryLabel;
        set => topActionPrimaryLabel = value;
    }

    [SerializeField]
    [Tooltip("The secondary label for the top action.")]
    public string topActionSecondaryLabel = null;

    /// <summary>
    /// The secondary label for the top action.
    /// </summary>
    private string TopActionSecondaryLabel
    {
        get => topActionSecondaryLabel;
        set => topActionSecondaryLabel = value;
    }

    [SerializeField]
    [Tooltip("The fancy icon type for the top action.")]
    public FancyIconType topActionIconType = FancyIconType.Unknown;

    /// <summary>
    /// The fancy icon type for the top action.
    /// </summary>
    private FancyIconType TopActionIconType
    {
        get => topActionIconType;
        set => topActionIconType = value;
    }

    [SerializeField]
    [Tooltip("The icon override prefab for the top action.")]
    public GameObject topActionIconOverridePrefab = null;

    /// <summary>
    /// The icon override prefab for the top action.
    /// </summary>
    private GameObject TopActionIconOverridePrefab
    {
        get => topActionIconOverridePrefab;
        set => topActionIconOverridePrefab = value;
    }

    [Header("Events")]

    [SerializeField]
    [Tooltip("Event invoked when the top action is executed.")]
    public UnityEvent topActionExecuted = new UnityEvent();

    /// <summary>
    /// Event invoked when the top action is executed.
    /// </summary>
    private UnityEvent TopActionExecuted
    {
        get => topActionExecuted;
    }

    [SerializeField]
    [Tooltip("Event invoked when data is being loaded.")]
    public UnityEvent dataLoading = new UnityEvent();

    /// <summary>
    /// Event invoked when data is being loaded.
    /// </summary>
    private UnityEvent DataLoading
    {
        get => dataLoading;
    }

    [SerializeField]
    [Tooltip("Event invoked when data is loaded.")]
    public UnityEvent dataLoaded = new UnityEvent();

    /// <summary>
    /// Event invoked when data is loaded.
    /// </summary>
    private UnityEvent DataLoaded
    {
        get => dataLoaded;
    }

    [SerializeField]
    [Tooltip("Event invoked when data failed load.")]
    public UnityEvent dataLoadFailed = new UnityEvent();

    /// <summary>
    /// Event invoked when data failed to load.
    /// </summary>
    private UnityEvent DataLoadFailed
    {
        get => dataLoadFailed;
    }
    #endregion Serialized Fields

    #region Public Properties
    /// <summary>
    /// Get the list state.
    /// </summary>
    public ListDataLoaderState State
    {
        get => _state;

        set
        {
            if (_state != value)
            {
                var oldState = _state;
                _state = value;
                OnStateChanged(oldState, _state);
            }
        }
    }
    #endregion Public Properties

    #region MonoBehavior Functions
    /// <summary>
    /// Load the last filter request
    /// </summary>
    protected virtual void OnEnable()
    {
        Load();
    }

    /// <summary>
    /// Cancel the current filter request
    /// </summary>
    protected virtual void OnDisable()
    {
        CancelLoad();
    }
    #endregion MonoBehavior Functions

    #region Public Functions
    public async void Load()
    {
        if (State == ListDataLoaderState.Loading)
        {
            return;
        }

        State = ListDataLoaderState.Loading;
        dataLoading?.Invoke();

        if (await LoadWorker())
        {
            State = ListDataLoaderState.Loaded;
            dataLoaded?.Invoke();
        }
        else
        {
            State = ListDataLoaderState.Failed;
            dataLoadFailed?.Invoke();
        }
    }
    #endregion Public Functions

    #region Protected Functions
    /// <summary>
    /// Get the data that will be put into the target list.
    /// </summary>
    protected abstract Task<IList<object>> GetData(CancellationToken cancellation);
   
    /// <summary>
    /// Force setting the data to a given list.
    /// </summary>
    protected void SetData(IList<object> data)
    {
        CancelLoad();

        if (data == null)
        {
            data = new List<object>();
        }

        ListItemActionData topActionButton = null;
        if (insertTopActionButton)
        {
            topActionButton = new ListItemActionData(() => topActionExecuted?.Invoke())
            {
                PrimaryLabel = TopActionPrimaryLabel,
                SecondaryLabel = TopActionSecondaryLabel,
                IconType = TopActionIconType,
                IconOverridePrefab = TopActionIconOverridePrefab
            };
        }

        if (topActionButton != null && target != null)
        {
            target.DataSource = new List<object>() { topActionButton };
        }

        if (topActionButton != null)
        {
            data.Insert(0, topActionButton);
        }

        if (target != null)
        {
            target.DataSource = data;
        }
    }

    /// <summary>
    /// Invoked when the state changes
    /// </summary>
    protected virtual void OnStateChanged(ListDataLoaderState oldState, ListDataLoaderState newState)
    {
    }
    #endregion Protected Functions

    #region Private Functions
    private async Task<bool> LoadWorker()
    {
        bool succeeded = false;

        CancelLoad();
        _cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _cancellationTokenSource.Token;

        IList<object> loaded = null;
        try
        {
            loaded = await GetData(cancellationToken);
            succeeded = true;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to load data. Exception {0}", ex);
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            SetData(loaded);
        }

        return succeeded;
    }

    public void CancelLoad()
    {
        if (_cancellationTokenSource != null)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }
    }
    #endregion Private Functions
}

public enum ListDataLoaderState
{
    Unknown,
    Loading,
    Loaded,
    Failed
}

