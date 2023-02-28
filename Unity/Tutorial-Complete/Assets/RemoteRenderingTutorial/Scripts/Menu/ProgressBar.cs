// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProgressBar : MonoBehaviour
{
    public Image progressBarFillImage;
    public TextMeshPro loadingText;

    public void SetProgress(float progress)
    {
        if (progressBarFillImage != null)
            progressBarFillImage.fillAmount = progress;

        if (loadingText != null)
            loadingText.text = $"Loading: {Math.Round(progress * 100, 2)}%...";
    }

    public void Show()
    {
		if(this.gameObject != null)
			this.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if(this.gameObject != null)
            this.gameObject.SetActive(false);
    }
}
