// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sets the visual states of the clipping gizmo
/// </summary>
public class ClippingGizmoHighlight : MonoBehaviour
{
    private Coroutine _changeColorRoutine = null;
    private Coroutine _highlightScaleRoutine = null;
    private Coroutine _defaultScaleRoutine = null;
    private List<Vector3> _initialScales = null;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The default material color.")]
    private Color defaultColor = Color.white;

    /// <summary>
    /// The default material color.
    /// </summary>
    public Color DefaultColor
    {
        get => defaultColor;
        set => defaultColor = value;
    }

    [SerializeField]
    [Tooltip("The material color applied on hover.")]
    private Color hoverColor = Color.red;

    /// <summary>
    /// The material color applied on hover.
    /// </summary>
    public Color HoverColor
    {
        get => hoverColor;
        set => hoverColor = value;
    }

    [SerializeField]
    [Tooltip("The material color applied on grabbed.")]
    private Color grabbedColor = Color.blue;

    /// <summary>
    /// The material color applied on grabbed.
    /// </summary>
    public Color GrabbedColor
    {
        get => grabbedColor;
        set => grabbedColor = value;
    }

    [SerializeField]
    [Tooltip("The time of the color and scaling animations.")]
    private float animationTime = 0.25f;

    /// <summary>
    /// The time of the color and scaling animations.
    /// </summary>
    public float AnimationTime
    {
        get => animationTime;
        set => animationTime = value;
    }

    [SerializeField]
    [Tooltip("The animation curve of the color and scaling animations")]
    private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    /// <summary>
    /// The animation curve of the color and scaling animations
    /// </summary>
    public AnimationCurve Curve
    {
        get => curve;
        set => curve = value;
    }

    [SerializeField]
    [Tooltip("The materials of these renderers will be modified.")]
    private Renderer[] gizmoRenderers = new Renderer[0];

    /// <summary>
    /// The materials of these renderers will be modified.
    /// </summary>
    public Renderer[] GizmoRenderers
    {
        get => gizmoRenderers;
        set => gizmoRenderers = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Functions
    private void Start()
    {
        if (gizmoRenderers != null)
        {
            _initialScales = new List<Vector3>(gizmoRenderers.Length);
            foreach (Renderer r in gizmoRenderers)
            {
                _initialScales.Add(r.transform.localScale);
            }
        }
    }
    #endregion MonoBehavior Functions

    #region Public Functions
    public void SetDefaultColor()
    {
        if (gizmoRenderers == null)
        {
            return;
        }

        StopCoroutines();
        _changeColorRoutine = StartCoroutine(ChangeColorRoutine(defaultColor));
        _defaultScaleRoutine = StartCoroutine(DefaultScaleRoutine());
    }

    public void SetHoverColor()
    {
        if (gizmoRenderers == null)
        {
            return;
        }

        StopCoroutines();
        _changeColorRoutine = StartCoroutine(ChangeColorRoutine(hoverColor));
        _defaultScaleRoutine = StartCoroutine(HighlightScaleRoutine());
    }

    public void SetGrabbedColor()
    {
        if (gizmoRenderers == null)
        {
            return;
        }

        StopCoroutines();
        _changeColorRoutine = StartCoroutine(ChangeColorRoutine(grabbedColor));
        _defaultScaleRoutine = StartCoroutine(DefaultScaleRoutine());
    }
    #endregion Public Functions

    #region Private Functions
    private void StopCoroutines()
    {
        if (_changeColorRoutine != null)
        {
            StopCoroutine(_changeColorRoutine);
            _changeColorRoutine = null;
        }

        if (_highlightScaleRoutine != null)
        {
            StopCoroutine(_highlightScaleRoutine);
            _highlightScaleRoutine = null;
        }

        if (_defaultScaleRoutine != null)
        {
            StopCoroutine(_defaultScaleRoutine);
            _defaultScaleRoutine = null;
        }
    }

    private IEnumerator ChangeColorRoutine(Color c)
    {
        Color startColor = GetColor();
        float time = 0;
        while (time < animationTime)
        {
            time += Time.deltaTime;
            SetColor(Color.Lerp(startColor, c, curve.Evaluate(time / animationTime)));
            yield return null;
        }
    }

    private IEnumerator HighlightScaleRoutine()
    {
        List<Vector3> startScales = new List<Vector3>();
        foreach (Renderer renderer in gizmoRenderers)
        {
            startScales.Add(renderer.transform.localScale);
        }

        float time = 0;
        while (time < animationTime)
        {
            time += Time.deltaTime;
            for(int i = 0; i < gizmoRenderers.Length; ++i)
            {
                gizmoRenderers[i].transform.localScale = Vector3.Lerp(startScales[i], _initialScales[i] * 1.1f, curve.Evaluate(time / animationTime));
            }
            yield return null;
        }
    }

    private IEnumerator DefaultScaleRoutine()
    {
        List<Vector3> startScales = new List<Vector3>();
        foreach (Renderer renderer in gizmoRenderers)
        {
            startScales.Add(renderer.transform.localScale);
        }

        float time = 0;
        while (time < animationTime)
        {
            time += Time.deltaTime;
            for (int i = 0; i < gizmoRenderers.Length; ++i)
            {
                gizmoRenderers[i].transform.localScale = Vector3.Lerp(startScales[i], _initialScales[i], curve.Evaluate(time / animationTime));
            }
            yield return null;
        }
    }

    private void SetColor(Color materialColor)
    {
        MaterialPropertyBlock material = new MaterialPropertyBlock();
        foreach (Renderer renderer in gizmoRenderers)
        {
            renderer.GetPropertyBlock(material);
            material.SetColor("_Color", materialColor);
            renderer.SetPropertyBlock(material);
        }
    }

    private Color GetColor()
    {
        if (gizmoRenderers.Length > 0)
        {
            MaterialPropertyBlock material = new MaterialPropertyBlock();
            gizmoRenderers[0].GetPropertyBlock(material);
            return material.GetColor("_Color");
        }
        else
        {
            return Color.white;
        }
    }
    #endregion Private Functions
}
