// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// A class for helping to load filterable data into the app's list repeater
/// </summary>
public class ListFilterableDataLoader : ListDataLoaderBase
{
    private string _lastFilter = null;
    private Coroutine _loadRoutine = null;

    #region Serialized Fields
    [Header("Filter Settings")]

    [SerializeField]
    [Tooltip("The current filter.")]
    private string filter = string.Empty;

    /// <summary>
    /// The current filter
    /// </summary>
    public string Filter
    {
        get => filter;
        set => BeginFilter(value);
    }
    #endregion Serialized Fields

    #region Public Properties
    /// <summary>
    /// The data to filter on
    /// </summary>
    public IFilterableDataSource FilterableDataSource { get; set; }
    #endregion Public Properties

    #region Public Functions
    public void BeginFilter(string value)
    {
        filter = value ?? string.Empty;
        if (filter != _lastFilter && State != ListDataLoaderState.Loading)
        {
            if (_loadRoutine != null)
            {
                StopCoroutine(_loadRoutine);
            }
            _loadRoutine = StartCoroutine(DelayBeginLoad());
        }
    }
    #endregion Public Functions

    #region Protected Functions
    /// <summary>
    /// Invoked when the state changes
    /// </summary>
    protected override void OnStateChanged(ListDataLoaderState oldState, ListDataLoaderState newState)
    {
        // try loading the pending filter
        BeginFilter(filter);
    }

    /// <summary>
    /// Get the data that will be put into the target list.
    /// </summary>
    protected override Task<IList<object>> GetData(CancellationToken cancellation)
    {
        _lastFilter = Filter;
        if (FilterableDataSource == null)
        {
            return Task.FromResult<IList<object>>(null);
        }
        else
        {
            return FilterableDataSource.Filter(_lastFilter, cancellation);
        }
    }
    #endregion Protected Functions

    #region Private Functions
    private IEnumerator DelayBeginLoad()
    {
        yield return new WaitForSeconds(seconds: 1.0f);
        if (filter != _lastFilter && State != ListDataLoaderState.Loading)
        {
            Load();
        }
        _loadRoutine = null;
    }
    #endregion Private Functions
}
