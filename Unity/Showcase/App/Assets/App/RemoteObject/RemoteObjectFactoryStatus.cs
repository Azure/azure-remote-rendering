// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using TMPro;
using UnityEngine;

public class RemoteObjectFactoryStatus : MonoBehaviour
{
    [Tooltip("The status text mesh.")]
    public TextMeshPro Status;

    [Tooltip("The status format string.")]
    public string StatusFormat = "Loading Objects {0:n}%";

    private float progress = 1.0f;

    private void Start()
    {
        UpdateActiveAndStatusText();
    }

    // Update is called once per frame
    public void UpdateStatus(float progress)
    {
        this.progress = progress;
        UpdateActiveAndStatusText();
    }

    private void UpdateActiveAndStatusText()
    {
        this.gameObject.SetActive(progress > 0.0f && progress < 1.0f);
        string status = string.Empty;
        float precentage = progress * 100;

        if (progress < 1.0f)
        {
            try
            {
                status = string.Format(StatusFormat, precentage);
            }
            catch (Exception)
            {
                status = precentage.ToString("n");
            }
        }

        if (Status != null)
        {
            Status.text = status;
        }
    }
}
