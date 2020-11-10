// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections;
using UnityEngine;

/// <summary>
/// Lerps the object's material from FlashColor to the original color over time.
/// </summary>
public class FlashOnEnable : MonoBehaviour
{
    private Renderer _renderer;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The flash color that the material's color will be animated to.")]
    private Color flashColor = Color.white;

    /// <summary>
    /// The flash color that the material's color will be animated to.
    /// </summary>
    public Color FlashColor
    {
        get => flashColor;
        set => flashColor = value;
    }

    [SerializeField]
    [Tooltip("The animation time, in seconds, of the flash animation.")]
    private float fadeTime = 1f;

    /// <summary>
    /// The animation time, in seconds, of the flash animation.
    /// </summary>
    public float FadeTime
    {
        get => fadeTime;
        set => fadeTime = value;
    }

    [SerializeField]
    [Tooltip("The animation curve of the flash animation.")]
    private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    /// <summary>
    /// The animation curve of the flash animation.
    /// </summary>
    public AnimationCurve Curve
    {
        get => curve;
        set => curve = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Functions
    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
    }

    private void OnEnable()
    {
        StartCoroutine(ColorRoutine(FlashColor));
    }
    #endregion MonoBehavior Functions

    #region Private Functions
    private void SetColor(Color c)
    {
        MaterialPropertyBlock material = new MaterialPropertyBlock();
        _renderer.GetPropertyBlock(material);
        material.SetColor("_Color", c);
        _renderer.SetPropertyBlock(material);
    }

    private IEnumerator ColorRoutine(Color flash)
    {
        Color startColor = _renderer.material.color;
        float time = 0f;
        while(time < fadeTime)
        {
            time += Time.deltaTime;
            SetColor(Color.Lerp(flash, startColor, curve.Evaluate(time / fadeTime)));
            yield return null;
        }
    }
    #endregion Private Functions
}
