// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;
using UnityEngine.Events;

public abstract class BaseRemoteBounds : MonoBehaviour
{
    public UnityEvent OnBoundsReady = new UnityEvent();
    public UnityEvent OnBoundsNotReady = new UnityEvent();

    private BoxCollider boxCollider = null;
    public BoxCollider BoundsBoxCollider
    {
        get
        {
            if (boxCollider == null)
            {
                boxCollider = GetComponent<BoxCollider>();
                if (boxCollider == null)
                    boxCollider = this.gameObject.AddComponent<BoxCollider>();
            }
            return boxCollider;
        }
    }

    public abstract RemoteBoundsState CurrentBoundsState { get; protected set; }

    public abstract event Action<RemoteBoundsState> BoundsStateChange;

    protected void HandleUnityEvents(RemoteBoundsState boundsState)
    {
        switch (boundsState)
        {
            case RemoteBoundsState.Ready:
                OnBoundsReady?.Invoke();
                break;
            default:
                OnBoundsNotReady?.Invoke();
                break;
        }
    }
}