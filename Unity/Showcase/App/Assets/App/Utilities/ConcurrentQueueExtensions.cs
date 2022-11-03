// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Concurrent;

public static class ConcurrentQueueExtensions
{
    public static void Clear<T>(this ConcurrentQueue<T> queue)
    {
        T item;
        while (queue.TryDequeue(out item))
        {
            // do nothing
        }
    }
}
