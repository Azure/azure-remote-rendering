// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;

/// <summary>
/// A behavior to visualize the remote sphere pointer.
/// </summary>
public class RemoteSpherePointerVisual : MonoBehaviour
{
    private IMixedRealityNearPointer _nearPointer = null;

    public Transform TetherEndPoint => tetherEndPoint;

    public bool TetherVisualsEnabled { get; private set; }

    [SerializeField]
    [Tooltip("The pointer these visuals decorate")]
    private BaseControllerPointer pointer = null;

    [SerializeField]
    [Tooltip("Tether will not be shown unless it is at least this long")]
    private float minTetherLength = 0.03f;

    [SerializeField]
    private Transform visualsRoot = null;

    [SerializeField]
    private Transform tetherEndPoint = null;

    /// Assumption: Tether line is a child of the visuals!
    [SerializeField]
    private BaseMixedRealityLineDataProvider tetherLine = null;

    public void OnEnable()
    {
        CheckInitialization();
    }

    public void OnDestroy()
    {
        if (visualsRoot != null)
        {
            Destroy(visualsRoot.gameObject);
        }
    }

    public void Start()
    {
        // put it at root of scene
        MixedRealityPlayspace.AddChild(visualsRoot.transform);
        visualsRoot.gameObject.name = $"{gameObject.name}_NearTetherVisualsRoot";
    }

    private void CheckInitialization()
    {
        _nearPointer = pointer as IMixedRealityNearPointer;
        if (_nearPointer == null)
        {
            Debug.LogError($"No near pointer found on {gameObject.name}.");
        }

        CheckAsset(visualsRoot, "Visuals Root");
        CheckAsset(tetherEndPoint, "Tether End Point");
        CheckAsset(tetherLine, "Tether Line");
    }

    private void CheckAsset(object asset, string fieldname)
    {
        if (asset == null)
        {
            Debug.LogError($"No {fieldname} specified on {gameObject.name}.SpherePointerVisual. Did you forget to set the {fieldname}?");
        }
    }

    public void Update()
    {
        TetherVisualsEnabled = false;
        if (_nearPointer != null && _nearPointer.IsFocusLocked && _nearPointer.IsTargetPositionLockedOnFocusLock && _nearPointer.Result != null)
        {
            NearInteractionGrabbable grabbedObject = GetGrabbedObject();
            if (grabbedObject != null && grabbedObject.ShowTetherWhenManipulating)
            {
                Vector3 graspPosition;
                _nearPointer.TryGetNearGraspPoint(out graspPosition);
                tetherLine.FirstPoint = graspPosition;
                Vector3 endPoint = pointer.Result.Details.Object.transform.TransformPoint(pointer.Result.Details.PointLocalSpace);
                tetherLine.LastPoint = endPoint;
                TetherVisualsEnabled = Vector3.Distance(tetherLine.FirstPoint, tetherLine.LastPoint) > minTetherLength;
                tetherLine.enabled = TetherVisualsEnabled;
                tetherEndPoint.gameObject.SetActive(TetherVisualsEnabled);
                tetherEndPoint.position = endPoint;
            }
        }

        visualsRoot.gameObject.SetActive(TetherVisualsEnabled);
    }

    private NearInteractionGrabbable GetGrabbedObject()
    {
        if (pointer.Result?.Details.Object != null)
        {
            return pointer.Result.Details.Object.GetComponent<NearInteractionGrabbable>();
        }
        else
        {
            return null;
        }
    }
}
