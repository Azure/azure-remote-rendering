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

    /// <summary>
    /// Await a task with a timeout.
    /// </summary>
    public static async Task<TimeoutResult> AwaitWithTimeout(this Task task, TimeSpan timeout)
    {
        if (await Task.WhenAny(task, Task.Delay(timeout)) == task)
        {
            return new TimeoutResult() { success = true };

        }
        else
        {
            return new TimeoutResult() { success = false };
        }
    }

    /// <summary>
    /// Await a task with a timeout.
    /// </summary>
    public static async Task<TimeoutResult<T>> AwaitWithTimeout<T>(this Task<T> task, TimeSpan timeout)
    {
        if (await Task.WhenAny(task, Task.Delay(timeout)) == task)
        {
            return new TimeoutResult<T>() { success = true, result = task.Result };

        }
        else
        {
            return new TimeoutResult<T>() { success = false, result = default };
        }
    }

    /// <summary>
    /// A timeout result with no return value.
    /// </summary>
    public struct TimeoutResult
    {
        public bool success;
    }

    /// <summary>
    /// A timeout result with a return value.
    /// </summary>
    public struct TimeoutResult<T>
    {
        public bool success;
        public T result;
    }
}





