using System;
using System.Collections.Generic;
using UnityEngine;

public class ExecuteOnUnityThread : MonoBehaviour
{
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
        }
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
