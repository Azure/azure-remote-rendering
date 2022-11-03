// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using UnityEngine.Events;

public class CreateGameObject : MonoBehaviour
{
    private GameObject _createdObject = null;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The prefab to use when creating object. If null, nothing is created.")]
    private GameObject objectPrefab = null;

    /// <summary>
    /// The prefab to use when creating object. If null, nothing is created.
    /// </summary>
    public GameObject ObjectPrefab
    {
        get => objectPrefab;
        set => objectPrefab = value;
    }

    [SerializeField]
    [Tooltip("The created object will be parented to this target. If null, the create object will be parented to the root.")]
    private Transform parentTarget = null;

    /// <summary>
    /// The created object will be parented to this target. If null, the create object will be parented to the root.
    /// </summary>
    public Transform ParentTarget
    {
        get => parentTarget;
        set => parentTarget = value;
    }

    [SerializeField]
    [Tooltip("Event raised when the object is created.")]
    private UnityEvent objectCreated = new UnityEvent();

    /// <summary>
    /// Event raised when the object is destroyed.
    /// </summary>
    public UnityEvent ObjectCreated => objectCreated;

    [SerializeField]
    [Tooltip("Event raised when the object is created.")]
    private UnityEvent objectDestroyed = new UnityEvent();

    /// <summary>
    /// Event raised when the object is destroyed.
    /// </summary>
    public UnityEvent ObjectDestroyed => objectDestroyed;
    #endregion Serialized Fields

    #region MonoBehavior Functions
    private void OnDestroy()
    {
        ReleaseObject();
    }
    #endregion MonoBehavior Functions

    #region Public Functions
    public void Make()
    {
        CreateAndSaveObject();
        objectCreated?.Invoke();
    }

    public void Clear()
    {
        ReleaseObject();
        objectDestroyed?.Invoke();
    }
    #endregion Public Functions

    #region Private Functions
    private void CreateAndSaveObject()
    {
        ReleaseObject();
        _createdObject = CreateObject();
    }

    private void ReleaseObject()
    {
        if (_createdObject != null)
        {
            Destroy(_createdObject);
            _createdObject = null;
        }
    }

    private GameObject CreateObject()
    {
        if (objectPrefab == null)
        {
            return null;
        }

        GameObject result = Instantiate(objectPrefab);
        result.transform.SetParent(ResolveParent(), worldPositionStays: false);
        return result;
    }

    private Transform ResolveParent()
    {
        return (parentTarget == null) ? transform : parentTarget;
    }
    #endregion Private Functions
}
