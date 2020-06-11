// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

public static class TaskExtensions
{
    /// <summary>
    /// Create a task that completes when this task completes, or throws an exception when a cancellation occurs.
    /// </summary>
    public static Task WithCancellation(this Task task, CancellationToken token)
    {
        return task.ContinueWith(t => t.GetAwaiter().GetResult(), token);
    }

    /// <summary>
    /// Create a task that completes when this task completes or when a cancellation occurs.
    /// </summary>
    public static Task<T> WithCancellation<T>(this Task<T> task, CancellationToken token)
    {
        return task.ContinueWith(t => t.GetAwaiter().GetResult(), token);
    }
}





