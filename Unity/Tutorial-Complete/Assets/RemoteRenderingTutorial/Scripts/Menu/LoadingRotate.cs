// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

public class LoadingRotate : MonoBehaviour
{
    public float rotationsPerSecond = 1;
    private Quaternion originalRotation;

    private float DegreesPerFrame => (rotationsPerSecond * 360) * Time.deltaTime;

    public void Awake()
    {
        originalRotation = transform.rotation;
    }

    public void Update()
    {
        transform.Rotate(0f, 0f, DegreesPerFrame);
    }

    public void OnDisable()
    {
        transform.rotation = originalRotation;
    }
}
