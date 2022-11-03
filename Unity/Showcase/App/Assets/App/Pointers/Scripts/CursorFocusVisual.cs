// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using System.Collections;
using UnityEngine;

/// <summary>
/// Change the attached cursor visual with the material specified by the focused object's context.
/// If there is no focused object, or the focus object has no context, the original materials are used.
/// </summary>
public class CursorFocusVisual : InputSystemGlobalHandlerListener, IMixedRealityFocusChangedHandler
{
    private BaseCursor _cursor = null;
    private Renderer _renderer = null;
    private Material[] _defaultMaterials = null;
    private Vector3 _defaultScale = Vector3.zero;
    private CursorFocusVisualContext _focusedContext = null;
    private CursorFocusVisualContext _appliedContext = null;
    private bool _showingDefaults = true;
    private Coroutine _sizeAnimation = null;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The animation curve used to scale the visual.")]
    public AnimationCurve sizeAnimationCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    /// <summary>
    /// The animation curve used to scale the visual.
    /// </summary>
    public AnimationCurve SizeAnimationCurve
    {
        get => sizeAnimationCurve;
        set => sizeAnimationCurve = value;
    }

    [SerializeField]
    [Tooltip("The time, in seconds, taken to fill the bar to the current goal.")]
    public float sizeAnimationTime = 0.2f;

    /// <summary>
    /// The time, in seconds, taken to scale the visual to the current goal.
    /// </summary>
    public float SizeAnimationTime
    {
        get => sizeAnimationTime;
        set => sizeAnimationTime = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Methods
    protected override void Start()
    {
        base.Start();

        _cursor = GetComponentInParent<BaseCursor>();

        _renderer = GetComponent<Renderer>();
        if (_renderer != null && _renderer.sharedMaterials != null)
        {
            int materialCount = _renderer.sharedMaterials.Length;
            _defaultMaterials = new Material[materialCount];
            for (int i = 0; i < materialCount; i++)
            {
                _defaultMaterials[i] = _renderer.sharedMaterials[i];
            }
        }
        else
        {
            _defaultMaterials = new Material[0];
        }

        HandleFocusUpdates();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (_defaultScale == Vector3.zero)
        {
            _defaultScale = transform.localScale;
        }

        HandleFocusUpdates();
    }

    private void LateUpdate()
    {
        ApplyContext();
    }
    #endregion MonoBehavior Methods

    #region IMixedRealityFocusChangedHandler Methods
    public void OnBeforeFocusChange(FocusEventData eventData)
    {
    }

    /// <summary>
    /// Handle focus change events.
    /// </summary>
    public void OnFocusChanged(FocusEventData eventData)
    {
        if (_cursor == null ||
            _cursor.Pointer == null ||
            _cursor.Pointer != eventData.Pointer)
        {
            return;
        }

        HandleFocusUpdates();
    }
    #endregion IMixedRealityFocusChangedHandler Methods

    #region InputSystemGlobalHandlerListener
    /// <summary>
    /// Register to global input source changes.
    /// </summary>
    protected override void RegisterHandlers()
    {
        CoreServices.InputSystem?.RegisterHandler<IMixedRealityFocusChangedHandler>(this);
    }

    /// <summary>
    /// Unregister from global input source changes.
    /// </summary>
    protected override void UnregisterHandlers()
    {
        CoreServices.InputSystem?.UnregisterHandler<IMixedRealityFocusChangedHandler>(this);
    }
    #endregion InputSystemGlobalHandlerListener

    #region Private Methods
    /// <summary>
    /// Handle focus changes, and update the focused material.
    /// </summary>
    private void HandleFocusUpdates()
    {
        if (_cursor != null &&
            _cursor.Pointer != null &&
            _cursor.Pointer.Result != null && 
            _cursor.Pointer.Result.CurrentPointerTarget != null)
        {
            _focusedContext = _cursor.Pointer.Result.CurrentPointerTarget.GetComponentInParent<CursorFocusVisualContext>();
        }
        else
        {
            _focusedContext = null;
        }
    }

    /// <summary>
    /// Update the materials later, so to avoid many material changes within single frame.
    /// </summary>
    private void ApplyContext()
    {
        if ((_appliedContext == _focusedContext) &&
            (_appliedContext != null || _showingDefaults))
        {
            return;
        }

        _appliedContext = _focusedContext;

        Material[] materials = _defaultMaterials;
        float scaleBy = 1.0f;
        if (_appliedContext != null)
        {
            _showingDefaults = false;

            if (_appliedContext.OverrideMaterials != null)
            {
                materials = _appliedContext.OverrideMaterials;
            }

            if (_appliedContext.Resize && _appliedContext.ResizeScale > 0f)
            {
                scaleBy = _appliedContext.ResizeScale;
            }
        }
        else
        {
            _showingDefaults = true;
        }

        if (_renderer != null)
        {
            if (materials.Length == 1)
            {
                _renderer.sharedMaterial = materials[0];
            }
            else
            {
                _renderer.sharedMaterials = materials;
            }
        }

        if (_sizeAnimation != null)
        {
            StopCoroutine(_sizeAnimation);
        }
        _sizeAnimation = StartCoroutine(AnimateScale(scaleBy));
    }

    private IEnumerator AnimateScale(float goalScaleBy)
    {
        float time = 0f;

        while (time < sizeAnimationTime)
        {
            time += Time.deltaTime;
            transform.localScale = _defaultScale * Mathf.Lerp(1.0f, goalScaleBy, sizeAnimationCurve.Evaluate(time / sizeAnimationTime));
            yield return null;
        }
        _sizeAnimation = null;
    }
    #endregion Private Methods
}
