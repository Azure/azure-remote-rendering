// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// A class for test list layout in editor
/// </summary>
public class ListTestDataSource : ListFilterableDataLoader
{
    #region Serialized Fields
    [Header("Test Settings")]

    [SerializeField]
    [Tooltip("The test data.")]
    private string[] testData = new string[0];

    /// <summary>
    /// The test data.
    /// </summary>
    public string[] TestData
    {
        get => testData;
        set => testData = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Functions
    private void Awake()
    {
        FilterableDataSource = new TestFilteredDataSource(testData);
    }
    #endregion MonoBehavior Functions

    private class TestFilteredDataSource : IFilterableDataSource
    {
        private string[] _data;

        public TestFilteredDataSource(string[] data)
        {
            _data = data;
        }

        public async Task<IList<object>> Filter(string value, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            return new List<object>(_data.Where((test) => test != null && test.StartsWith(value)));
        }
    }
}
