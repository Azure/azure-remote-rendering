// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// A behavior for moving the camera around when running app on a desktop.
/// </summary>
public class CameraControl : MonoBehaviour 
{
    #region Serialized Fields
    [SerializeField]
    [Tooltip("The sensitivity of camera pan movement.")]
    private Vector2 panSensitivity = new Vector2(60.0f, 60.0f);

    /// <summary>
    /// The sensitivity of camera pan movement.
    /// </summary>
    public Vector2 PanSensitivity
    {
        get => panSensitivity;
        set => panSensitivity = value;
    }

    [SerializeField]
    [Tooltip("The sensitivity of camera rotation.")]
    public Vector2 rotateSensitivity = new Vector2(120.0f, 120.0f);

    /// <summary>
    /// The sensitivity of camera rotation.
    /// </summary>
    public Vector2 RotateSensitivity
    {
        get => rotateSensitivity;
        set => rotateSensitivity = value;
    }
    #endregion Serialized Fields
    
    #region MonoBehavior Functions
    private void Awake()
    {
        // Destroy if editor, MRTK's input simulation will be used
        // Destroy if using an XR device, camera moves with head.
        bool isXrDevice = XRSettings.enabled &&
            XRSettings.isDeviceActive &&
            !string.IsNullOrEmpty(XRSettings.loadedDeviceName) &&
            XRSettings.loadedDeviceName != "None";

        if (Application.isEditor && !isXrDevice)
        {
            Component.DestroyImmediate(this);
        }
    }

    void Update() 
	{
        //
        // Move slower when holding down shift.
        //
        float timeDelta = Time.deltaTime * (Input.GetKey(KeyCode.LeftShift) ? 0.1f : 1.0f);

        //
        // Apply movement
        //
        transform.Translate(new Vector3(
            Input.GetAxis("Horizontal") * 2.5f * timeDelta,
            0,
            Input.GetAxis("Vertical") * 2.5f * timeDelta));

        //
        // Apply Panning and Rotation
        //
        if (Input.GetMouseButton(2) || Input.GetKey(KeyCode.R) || Input.GetKey(KeyCode.F))
        {
            float y = Input.GetKey(KeyCode.F) ? -.01f : 0;
            y += Input.GetKey(KeyCode.R) ? .01f : 0;
            y += Input.GetAxis("Mouse Y");

            transform.Translate(new Vector3(
                Input.GetAxis("Mouse X") * panSensitivity.x * timeDelta,
                y * panSensitivity.y * timeDelta,
                0.0f));
        }
        else if (Input.GetMouseButton(1) || Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.E))
        {
            float y = Input.GetKey(KeyCode.Q) ? -.5f : 0;
            y += Input.GetKey(KeyCode.E) ? .5f : 0;
            y += Input.GetAxis("Mouse X");

            transform.eulerAngles += new Vector3(
                -Input.GetAxis("Mouse Y") * rotateSensitivity.y * timeDelta,
                y * rotateSensitivity.x * timeDelta,
                0.0f);
        }
    }
    #endregion MonoBehavior Functions
}
