// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class ExecuteOnUnityThread : MonoBehaviour
{
    private static CancellationTokenSource applicationTokenSource;
    public static CancellationToken ApplicationToken => applicationTokenSource.Token;

    private static Queue<Action> actions = new Queue<Action>();

    private static ExecuteOnUnityThread instance;

    public void Awake()
    {
        if (instance != null)
        {
            Destroy(this);
        } else
        {
            instance = this;
            applicationTokenSource = new CancellationTokenSource();
        }
    }

    public void OnDestroy()
    {
        applicationTokenSource?.Cancel();
    }

    public void Update()
    {
        lock (actions)
        {
            while(actions.Count > 0)
            {
                var action = actions.Dequeue();
                action?.Invoke();
            }
        }
    }

    public static void Enqueue(Action action)
    {
        lock(actions)
        {
            actions.Enqueue(action);
        }
    }
}
