// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Used as a target for the "Reparent" components. ReparentTargets are given ids, and the Reparent sources search for these ids.
/// </summary>
public class ReparentTarget : MonoBehaviour
{
    private static Dictionary<ReparentTargetId, ReparentTarget> _targets = new Dictionary<ReparentTargetId, ReparentTarget>();

    #region Serialized Fields

    [SerializeField]
    [Tooltip("The target to place children. If null, children our placed at this transform.")]
    private Transform target = null;

    /// <summary>
    /// The target to place children. If null, children our placed at this transform.
    /// </summary>
    public Transform Target
    {
        get => target;
        set => target = value;
    }

    [SerializeField]
    [Tooltip("The target id. Reparent components will search for this id.")]
    private ReparentTargetId id = ReparentTargetId.Unknown;

    /// <summary>
    /// The target id. Reparent components will search for this id.
    /// </summary>
    public ReparentTargetId ID
    {
        get => id;
        set => id = value;
    }

    [Header("Events")]

    [SerializeField]
    [Tooltip("Event fired when child was added.")]
    private ReparentTargetChildEvent childAdded = new ReparentTargetChildEvent();

    /// <summary>
    /// Event fired when child was added
    /// </summary>
    public ReparentTargetChildEvent ChildAdded => childAdded;

    [SerializeField]
    [Tooltip("Event fired when child was removed.")]
    private ReparentTargetChildEvent childRemoved = new ReparentTargetChildEvent();

    /// <summary>
    /// Event fired when child was removed
    /// </summary>
    public ReparentTargetChildEvent ChildRemoved => childRemoved;
    #endregion Serialized Fields

    #region MonoBehavior Functions
    private void Awake()
    {
        Register(this);
    }
    #endregion MonoBehavior Functions

    #region Public Functions
    public static void Add(ReparentTargetId id, Transform child)
    {
        ReparentTarget parentTarget = Find(id);
        if (parentTarget != null && child.parent != parentTarget.transform)
        {
            var target = parentTarget.target == null ? parentTarget.transform : parentTarget.target;
            child.SetParent(target, false);
            parentTarget.childAdded?.Invoke(new ReparentTargetChildEventArgs(child));
        }
    }

    public static void Remove(Transform child)
    {
        ReparentTarget parentTarget = Find(child);
        if (parentTarget != null)
        {
            child.SetParent(null, true);
            parentTarget.childRemoved?.Invoke(new ReparentTargetChildEventArgs(child));
        }
    }
    #endregion Public Functions

    #region Private Functions
    private static ReparentTarget Find(ReparentTargetId id)
    {
        ReparentTarget result;
        if (!_targets.TryGetValue(id, out result))
        {
            Register(FindObjectsOfType<ReparentTarget>());
            _targets.TryGetValue(id, out result);
        }
        return result;
    }

    private static ReparentTarget Find(Transform child)
    {
        ReparentTarget result = null;
        if (child != null && child.parent != null)
        {
            result = child.parent.GetComponentInParent<ReparentTarget>();

            if (result == null)
            {
                foreach (var entry in _targets)
                {
                    var current = entry.Value;
                    if (current != null &&
                        current.target == child.parent)
                    {
                        result = current;
                        break;
                    }
                }
            }
        }
        return result;
    }

    private static void Register(ReparentTarget[] targets)
    {
        if (targets != null)
        {
            int length = targets.Length;
            for (int i = 0; i < length; i++)
            {
                Register(targets[i]);
            }
        }
    }
    private static void Register(ReparentTarget target)
    {
        if (target != null && target.id != ReparentTargetId.Unknown)
        {
            _targets[target.id] = target;
        }
    }
    #endregion Private Functions
}

[Serializable]
public class ReparentTargetChildEvent : UnityEvent<ReparentTargetChildEventArgs>
{
}

[Serializable]
public class ReparentTargetChildEventArgs
{
    public ReparentTargetChildEventArgs(Transform child)
    {
        child = Child;
    }

    public Transform Child { get; }
}


/// <summary>
/// The ids for reparenting targets
/// </summary>
public enum ReparentTargetId
{
    Unknown,
    MenuSidebar,
    Stage,
    MainCamera,
    SharedVideoSources,
    SharedRoot,
}