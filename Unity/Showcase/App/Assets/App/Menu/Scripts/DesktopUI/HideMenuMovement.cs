// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;

/// <summary>
/// A behavior for hiding the menu while any camera movement is happening.
/// </summary>
public class HideMenuMovement:MonoBehaviour
{
    public bool IsVisible { get; private set; }
    
    private Transform camTransform;
    private Vector3 prevPos;
    private Quaternion prevRot;
    private Quaternion prevMouse;

    private Vector3 lerpPosVel;
    private float lerpRotVel;

    private void Start()
    {
        camTransform = CameraCache.Main.transform;
        prevPos = camTransform.position;
        prevRot = camTransform.rotation;
        IsVisible = true;
    }

    private void LateUpdate()
    {
        // Check for any movement input
        var keyX = Mathf.Abs(Input.GetAxis("Horizontal"));
        var keyY = Mathf.Abs(Input.GetAxis("Vertical"));
        var mouseButtons = Input.GetMouseButton(1) || Input.GetMouseButton(2);
        var mouse = Mathf.Abs(Input.GetAxis("Mouse X")) + Mathf.Abs(Input.GetAxis("Mouse Y"));
        var panRotateKeys = Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.R) ||
                            Input.GetKey(KeyCode.F);
        var movementInput = keyX > 0f || keyY > 0f || panRotateKeys || mouseButtons && mouse > 0f;
        
        if(IsVisible)
        {
            if(movementInput)
            {
                // Hide
                transform.localPosition = Vector3.back * 1500f;
                IsVisible = false;
            }
        }
        else
        {
            // Check for the virtual camera positioning settling back
            if(!movementInput && 
               Vector3.Distance(prevPos, camTransform.position) < 0.05f &&
               Quaternion.Angle(prevRot, camTransform.rotation) < 0.001f)
            {
                // Show
                transform.localPosition = Vector3.zero;
                IsVisible = true;
            }
        }
        
        // Lerp prev position and rotation to current to imitate the rendering delay
        prevPos = Vector3.SmoothDamp(prevPos, camTransform.position, ref lerpPosVel, 0.2f);
        float angle = Quaternion.Angle(prevRot, camTransform.rotation);
        float lerpAngle = Mathf.SmoothDampAngle(0f, angle, ref lerpRotVel, 0.2f);
        if(lerpAngle > 0f) prevRot = Quaternion.Lerp(prevRot, camTransform.rotation, lerpAngle / angle);
    }
}