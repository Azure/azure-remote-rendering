// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Used to position the game object's transform on top of the target. The object's orientation is also changed to "Rotation Euler Default".
/// </summary>
public class PlaceOnTarget : MonoBehaviour
{
    Coroutine _placementCoroutine = null;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The target whose position will be changed.")]
    private GameObject target = null;

    /// <summary>
    /// The target whose position will be changed.
    /// </summary>
    public GameObject Target
    {
        get => target;
        set => target = value;
    }

    [SerializeField]
    [Tooltip("The source for the position that will be used when resetting the position of this transform.")]
    private GameObject source = null;

    /// <summary>
    /// The source for the position position will be used when resetting the position of this transform.
    /// </summary>
    public GameObject Source
    {
        get => source;
        set => source = value;
    }

    [SerializeField]
    [Tooltip("Use the main camera as the component's source, if the source value is null.")]
    private bool sourceMainCamera = false;

    /// <summary>
    /// Use the main camera as the component's source, if the source value is null.
    /// </summary>
    public bool SourceMainCamera
    {
        get => sourceMainCamera;
        set => sourceMainCamera = value;
    }

    [SerializeField]
    [EnumFlag]
    [Tooltip("Should 'Rotation Euler Default' value be applied relative to the source object.")]
    private RotateFlags rotatationType = RotateFlags.SourceAll;

    /// <summary>
    /// Should 'Rotation Euler Default' value be applied relative to the source object.
    /// </summary>
    public RotateFlags RotatationType
    {
        get => rotatationType;
        set => rotatationType = value;
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

    [Header("Initialization settings")]

    [SerializeField]
    [Tooltip("During the creation of this object, delay the initial placement of this object by the given amount of seconds. This is useful for things placed during app launch, if the object is being placed relative to the main camera.")]
    private float initialPlacementDelay = 0;

    /// <summary>
    /// During the creation of this object, delay the initial placement of this object by the given amount of seconds. This is useful for things placed during app launch, if the object is being placed relative to the main camera.
    /// </summary>
    public float InitialPlacementDelay
    {
        get => initialPlacementDelay;
        set => initialPlacementDelay = value;
    }

    [SerializeField]
    [Tooltip("Should this place the target transform when this behavior is enabled.")]
    private bool autoPlace = true;

    /// <summary>
    /// Should this place the target transform when this behavior is enabled.
    /// </summary>
    public bool AutoPlace
    {
        get => autoPlace;
        set => autoPlace = value;
    }

    [Header("Events")]

    [SerializeField]
    [Tooltip("Event raised when object is placed.")]
    private UnityEvent onPlaced = new UnityEvent();

    /// <summary>
    /// Event raised when object is placed
    /// </summary>
    public UnityEvent OnPlaced => onPlaced;
    #endregion Serialized Fields

    #region MonoBehavior Functions
    private void OnEnable()
    {
        if (_placementCoroutine != null)
        {
            StopCoroutine(_placementCoroutine);
            _placementCoroutine = null; 
        }

        if (autoPlace)
        {
            _placementCoroutine = StartCoroutine(PlacementCoroutine());
        }
    }
    #endregion MonoBehavior Functions

    #region Public Functions
    /// <summary>
    /// Position the game object's transform to it's original orientation in relation to a given source.
    /// </summary>
    public void Place()
    {
        var useSource = source;
        if (useSource == null && sourceMainCamera)
        {
            useSource = CameraCache.Main.gameObject;
        }

        if (useSource == null)
        {
            return;
        }

        if (target == null)
        {
            target = this.gameObject;
        }

        target.transform.position = useSource.transform.position
            + (useSource.transform.forward * positionOffSet.z)
            + (useSource.transform.up * positionOffSet.y)
            + (useSource.transform.right * positionOffSet.x);

        target.transform.localScale = scaleDefault;

        if (rotatationType != RotateFlags.None)
        {
            Vector3 sourceRotation = Vector3.zero;
            if ((rotatationType & RotateFlags.SourcePitch) == RotateFlags.SourcePitch)
            {
                sourceRotation.x = useSource.transform.rotation.eulerAngles.x;
            }
            if ((rotatationType & RotateFlags.SourceYaw) == RotateFlags.SourceYaw)
            {
                sourceRotation.y = useSource.transform.rotation.eulerAngles.y;
            }
            if ((rotatationType & RotateFlags.SourceRoll) == RotateFlags.SourceRoll)
            {
                sourceRotation.z = useSource.transform.rotation.eulerAngles.z;
            }

            target.transform.rotation = Quaternion.Euler(sourceRotation) * Quaternion.Euler(rotationEulerDefault);
        }
        else
        {
            target.transform.rotation = Quaternion.Euler(rotationEulerDefault);
        }

        onPlaced?.Invoke();
    }
    #endregion Public Functions

    #region Private Functions
    /// <summary>
    /// Start placement with an initial delay, if any delay.
    /// </summary>
    /// <returns></returns>
    private IEnumerator PlacementCoroutine()
    {
        if (initialPlacementDelay > 0)
        {
            yield return new WaitForSeconds(initialPlacementDelay);
        }
        initialPlacementDelay = 0;
        Place();
        yield break;
    }
    #endregion Private Functions

    #region Public Enum Flags
    [Flags]
    public enum RotateFlags
    {
        [Tooltip("Apply rotation in world space.")]
        None = 0,
        [Tooltip("Rotate in the source's space, using it's X rotation.")]
        SourcePitch = 1,
        [Tooltip("Rotate in the source's space, using it's Y rotation.")]
        SourceYaw = 2,
        [Tooltip("Rotate in the source's space, using it's Z rotation.")]
        SourceRoll = 4,
        [Tooltip("Rotate in the source's space, using it's X, Y, and Z rotation.")]
        SourceAll = SourcePitch | SourceYaw | SourceRoll,
        [Tooltip("Rotate in the source's space, using it's X and Y rotation.")]
        SourceWorldUp = SourcePitch | SourceYaw,
    }
    #endregion Public Enum Flags
}
