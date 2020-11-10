// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine;

/// <summary>
/// A list component that combines a 'Drag Value', a 'Pinch Slider', and a 'List Scroller' so to scroll a list container.
/// </summary>
public class ListScrollerProxy : MonoBehaviour
{
    private float _initialScrollerGoal;
    private float _scrollerGoalAtDragStart;
    private MovingAverageFloat _draggingVelocity = new MovingAverageFloat(5);
    private bool _dragging = false;
    private float _draggingGoal;
    private float _draggingUpdateTime;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("The list scroller component.")]
    private ListScrollerBase scroller = null;

    /// <summary>
    ///
    /// </summary>
    public ListScrollerBase Scroller
    {
        get => scroller;
        set => scroller = value;
    }

    [SerializeField]
    [Tooltip("The drag value for the list. This handle pointer drags along the list.")]
    private DragValue dragValue = null;

    /// <summary>
    /// The drag value for the list. This handle pointer drags along the list.
    /// </summary>
    public DragValue DragValue
    {
        get => dragValue;
        set => dragValue = value;
    }

    [SerializeField]
    [Tooltip("The pinch slider for the list.")]
    private PinchSlider listSlider = null;

    /// <summary>
    /// The pinch slider for the list.
    /// </summary>
    public PinchSlider ListSlider
    {
        get => listSlider;
        set => listSlider = value;
    }

    [SerializeField]
    [Tooltip("How fast the drag velocity decays.")]
    private float draggingMomentumDecay = 1.0f;

    /// <summary>
    /// How fast the drag velocity decays.
    /// </summary>
    public float DraggingMomentumDecay
    {
        get => draggingMomentumDecay;
        set => draggingMomentumDecay = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Functions
    private void OnEnable()
    {
        if (scroller != null)
        {
            _initialScrollerGoal = scroller.Goal;
        }
        else
        {
            _initialScrollerGoal = 0;
        }

        if (listSlider != null)
        {
            listSlider.OnValueUpdated.AddListener(OnListSliderValueUpdated);
        }

        if (dragValue != null)
        {
            dragValue.OnInteractionStarted.AddListener(OnDragValueInteractionStarted);
            dragValue.OnInteractionEnded.AddListener(OnDragValueInteractionEnded);
            dragValue.OnValueUpdated.AddListener(OnDragValueUpdated);
        }

        if (scroller != null)
        {
            scroller.SizeChanges.AddListener(OnScrollerSizeChanged);
        }

        CommitScrollerGoal(_initialScrollerGoal);
        OnScrollerSizeChanged();
    }

    private void OnDisable()
    {
        if (listSlider != null)
        {
            listSlider.OnValueUpdated.RemoveListener(OnListSliderValueUpdated);
        }

        if (dragValue != null)
        {
            dragValue.OnInteractionStarted.RemoveListener(OnDragValueInteractionStarted);
            dragValue.OnInteractionEnded.RemoveListener(OnDragValueInteractionEnded);
            dragValue.OnValueUpdated.RemoveListener(OnDragValueUpdated);
        }

        if (scroller != null)
        {
            scroller.SizeChanges.RemoveListener(OnScrollerSizeChanged);
        }
    }

    private void Update()
    {
        //TODO Drag is broken
        //ApplyDragMomentum();
    }
    #endregion MonoBehavior Functions

    #region Private Functions
    private void OnDragValueInteractionStarted(DragValueEventData eventData)
    {
        this._scrollerGoalAtDragStart = _initialScrollerGoal;
        this._draggingUpdateTime = 0;
        this._dragging = true;
    }

    private void OnDragValueUpdated(DragValueEventData eventData)
    {
        if (scroller)
        {
            var newDragTime = Time.time;
            var newDragGoal = _scrollerGoalAtDragStart + (eventData.NewValue / scroller.PageCount);

            if (this._draggingUpdateTime > 0 &&
                this._draggingUpdateTime != newDragTime)
            {
                this._draggingVelocity.AddSample(
                    (newDragGoal - this._draggingGoal) /
                    (newDragTime - this._draggingUpdateTime));
            }

            this._draggingUpdateTime = newDragTime;
            this._draggingGoal = newDragGoal;
            CommitScrollerGoal(this._draggingGoal);
        }
    }

    private void OnDragValueInteractionEnded(DragValueEventData eventData)
    {
        this._dragging = false;
    }

    private void OnListSliderValueUpdated(SliderEventData eventData)
    {
        CommitScrollerGoal(eventData.NewValue);
    }

    private void OnScrollerSizeChanged()
    {
        bool canScroll = scroller != null && scroller.PageCount > 1;
        if (listSlider != null)
        {
            listSlider.gameObject.SetActive(canScroll);
        }
    }

    private void ApplyDragMomentum()
    {
        if (this._draggingVelocity.NumSamples == 0 || this._dragging)
        {
            return;
        }

        float deceleration = 0;
        this._draggingVelocity.AddSample(
            Mathf.SmoothDamp(this._draggingVelocity.Average, 0, ref deceleration, this.draggingMomentumDecay));
      
        this._draggingGoal += this._draggingVelocity.Average * Time.deltaTime;
        this.CommitScrollerGoal(this._draggingGoal);

        if (Mathf.Abs(this._draggingVelocity.Average) <= 0.001f)
        {
            this._draggingVelocity.Clear();
        }
    }

    private void CommitScrollerGoal(float value)
    {
        value = Mathf.Clamp(value, 0, 1);
        scroller?.SnapGoalTo(value);
        _initialScrollerGoal = scroller?.Goal ?? 0;
        if (listSlider && listSlider.SliderValue != value)
        {
            listSlider.SliderValue = value;
        }
    }
    #endregion Private Functions
}
