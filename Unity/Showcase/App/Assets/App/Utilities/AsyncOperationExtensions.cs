// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;
using UnityEngine;

public static class AsyncOperationExtensions
{
    public static async Task AsTask(this AsyncOperation asyncOperation)
    {
        if (asyncOperation == null)
        {
            return;
        }

        AsyncOperationWrapper wrapper = new AsyncOperationWrapper(asyncOperation);
        await wrapper.Task;
    }

    public static async Task<T> AsTask<T>(this ResourceRequest asyncOperation) where T : class
    {
        if (asyncOperation == null)
        {
            return default;
        }

        AsyncOperationWrapper wrapper = new AsyncOperationWrapper(asyncOperation);
        await wrapper.Task;
        return asyncOperation.asset as T;
    }

    private class AsyncOperationWrapper
    {
        private UnityEngine.AsyncOperation asyncOperation = null;
        private TaskCompletionSource<bool> taskSource = new TaskCompletionSource<bool>();
        public Task<bool> Task => taskSource.Task;

        public AsyncOperationWrapper(AsyncOperation asyncOperation)
        {
            this.asyncOperation = asyncOperation;
            this.asyncOperation.completed += OnCompleted;
        }

        private void OnCompleted(AsyncOperation completedAction)
        {
            this.asyncOperation.completed -= OnCompleted;
            taskSource.TrySetResult(true);
        }
    }
}
