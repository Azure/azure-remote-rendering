// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Used to position the game object's transform ontop of the target. The object's orientation is also changed to "Rotation Euler Default".
/// </summary>
public class PlaceOnTarget : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    [Tooltip("The target whose position will be used when resetting the position of this transform.")]
    private GameObject target = null;

    /// <summary>
    /// The target whose position will be used when resetting the position of this transform.
    /// </summary>
    public GameObject Target
    {
        get => target;
        set => target = value;
    }

    [SerializeField]
    [Tooltip("Should 'Rotation Euler Default' value be applied relative to the target object.")]
    private bool rotateInTargetSpace = true;

    /// <summary>
    /// Should 'Rotation Euler Default' value be applied relative to the target object.
    /// </summary>
    public bool RotateInTargetSpace
    {
        get => rotateInTargetSpace;
        set => rotateInTargetSpace = value;
    }

    [Header("Transform settings")]

    [SerializeField]
    [Tooltip("The position offset, from the menu object, to set during enablement.")]
    private Vector3 positionOffSet = Vector3.zero;

    /// <summary>
    /// The position offset, from the menu object, to set during enablement.
    /// </summary>
    public Vector3 PositionOffSet
    {
        get => positionOffSet;
        set => positionOffSet = value;
    }

    [SerializeField]
    [Tooltip("The rotation to set on enablement.")]
    private Vector3 rotationEulerDefault = Vector3.zero;

    /// <summary>
    /// The rotation to set on enablement.
    /// </summary>
    public Vector3 RotationEulerDefault
    {
        get => rotationEulerDefault;
        set => rotationEulerDefault = value;
    }

    [SerializeField]
    [Tooltip("The scale to set on enablement.")]
    private Vector3 scaleDefault = Vector3.one;

    /// <summary>
    /// The scale to set on enablement.
    /// </summary>
    public Vector3 ScaleDefault
    {
        get => scaleDefault;
        set => scaleDefault = value;
    }

    [SerializeField]
    [Tooltip("During the creation of this object, delay the initial placement of this object by the given amount of seconds. This is useful for things placed during app launch, if the object is being placed relative to the main camera.")]
    private float initialPlacementDelay = 0;

    /// <summary>
    /// During the creation of this object, delay the initial placement of this object by the given amount of seconds. This is useful for things placed during app launch, if the object is being placed relative to the main camera.
    /// </summary>
    public float IniitialPlacementDelay
    {
        get => initialPlacementDelay;
        set => initialPlacementDelay = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Functions
    private async void Awake()
    {
        // Enablement will still do a placement. So if iniitialPlacementDelay is zero,
        // don't perform an extra placement calculation.
        if (initialPlacementDelay > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(initialPlacementDelay));
            Place();
        }
    }

    private void OnEnable()
    {
        Place();
    }
    #endregion MonoBehavior Functions

    #region Public Functions
    /// <summary>
    /// Position the game object's transform to it's original orientation in relation to a given target.
    /// </summary>
    public void Place()
    {
        if (target == null)
        {
            return;
        }

        this.transform.position = target.transform.position
            + (target.transform.forward * positionOffSet.z)
            + (target.transform.up * positionOffSet.y)
            + (target.transform.right * positionOffSet.x);

        this.transform.localScale = scaleDefault;

        if (rotateInTargetSpace)
        {
            this.transform.rotation = target.transform.rotation * Quaternion.Euler(rotationEulerDefault);
        }
        else
        {
            this.transform.rotation = Quaternion.Euler(rotationEulerDefault);
        }
    }
    #endregion Public Functions
}
