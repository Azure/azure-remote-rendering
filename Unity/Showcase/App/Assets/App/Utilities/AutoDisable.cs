// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections;
using UnityEngine;

public class AutoDisable : MonoBehaviour
{
    private Coroutine _delayedDisable = null;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The target to disable on awake. If null, this game object will be disabled.")]
    private GameObject target = null;

    /// <summary>
    /// The target to disable on awake. If null, this game object will be disabled.
    /// </summary>
    public GameObject Target
    {
        get => target;
        set => target = value;
    }

    [SerializeField]
    [Tooltip("The behaviours to disable on awake and start.")]
    private Behaviour[] behaviours = null;

    /// <summary>
    /// The behaviours to disable on awake and start.
    /// </summary>
    public Behaviour[] Behaviours
    {
        get => behaviours;
        set => behaviours = value;
    }

    [SerializeField]
    [Tooltip("The disable when this is awoken.")]
    private bool disableOnAwake = true;

    /// <summary>
    /// The disable when this is awoken.
    /// </summary>
    public bool DisableOnAwake
    {
        get => disableOnAwake;
        set => disableOnAwake = value;
    }

    [SerializeField]
    [Tooltip("The disable when this is started.")]
    private bool disableOnStart = false;

    /// <summary>
    /// The disable when this is started.
    /// </summary>
    public bool DisableOnStart
    {
        get => disableOnStart;
        set => disableOnStart = value;
    }

    [SerializeField]
    [Tooltip("The disable on the first update.")]
    private bool disableOnUpdate = false;

    /// <summary>
    /// The disable when this is started.
    /// </summary>
    public bool DisableOnUpdate
    {
        get => disableOnUpdate;
        set => disableOnUpdate = value;
    }

    [SerializeField]
    [Tooltip("Delay when targets are disabled, in seconds.")]
    private float delayInSeconds = 0;

    /// <summary>
    /// Delay when targets are disabled, in seconds.
    /// </summary>
    public float DelayInSeconds
    {
        get => delayInSeconds;
        set => delayInSeconds = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Functions
    private void Awake()
    {
        if (disableOnAwake)
        {
            Disable();
        }
    }

    private void Start()
    {
        if (disableOnStart)
        {
            Disable();
        }
    }

    private void Update()
    {
        if (disableOnUpdate)
        {
            Disable();
        }
    }
    #endregion MonoBehavior Functions

    #region Private Functions
    private void Disable()
    {
        if (_delayedDisable == null)
        {
            if (delayInSeconds <= 0)
            {
                DisableWorker();
            }
            else
            {
                _delayedDisable = StartCoroutine(DelayDisable());
            }
        }
    }

    private IEnumerator DelayDisable()
    {
        yield return new WaitForSeconds(delayInSeconds);
        DisableWorker();
    }

    private void DisableWorker()
    {
        if (target != null)
        {
            target.SetActive(false);
        }

        if (behaviours != null)
        {
            int length = behaviours.Length;
            for (int i = 0; i < length; i++)
            {
                behaviours[i].enabled = false;
            }
        }

        this.enabled = false;
    }
    #endregion Private Function
}
