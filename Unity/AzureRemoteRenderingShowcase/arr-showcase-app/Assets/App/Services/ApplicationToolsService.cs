// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;
using UnityEngine.Events;

public class ApplicationToolsService : MonoBehaviour
{
    public Interactable ResetCameraButton;
    public Interactable QuitButton;

    public UnityEvent QuitButtonAction;

    private void Start()
    {
        if(ResetCameraButton != null)
        {
            ResetCameraButton.OnClick.AddListener(ResetCamera);
        }

        if(QuitButton != null)
        {
            QuitButton.OnClick.AddListener(Quit);
        }
    }

    private void ResetCamera()
    {
        CameraCache.Main.transform.position = Vector3.zero;
        CameraCache.Main.transform.rotation = Quaternion.identity;
    }

    private void Quit()
    {
        QuitButtonAction?.Invoke();
    }
}