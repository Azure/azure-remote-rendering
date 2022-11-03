// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// An interface for filter data by a string.
/// </summary>
public interface IFilterableDataSource
{
    /// <summary>
    /// Start filtering the data and return a task that will complete once the filter is done.
    /// </summary>
    Task<IList<object>> Filter(string value, CancellationToken cancellationToken);
}
