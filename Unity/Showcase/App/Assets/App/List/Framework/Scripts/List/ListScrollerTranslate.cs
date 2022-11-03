// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// A list component that can translate a list container so to reveal more list items.
/// </summary>
public class ListScrollerTranslate : ListScrollerBase
{
    private float _goal;
    private TaskCompletionSource<bool> _scrollingTaskSource = null;
    private object _scrollingTaskSourceLock = new object();
    private Vector3 _initialPosition;

    #region Serialized Fields

    [Header("List Scroller Translate Fields")]

    [SerializeField]
    [Tooltip("Position lerp multiplier.")]
    private float moveLerpTime = 0.1f;

    /// <summary>
    /// Position lerp multiplier.
    /// </summary>
    public float MoveLerpTime
    {
        get => moveLerpTime;
        set => moveLerpTime = value;
    }

    [SerializeField]
    [Tooltip("Working output is smoothed if true.")]
    private bool smoothing = true;

    /// <summary>
    /// Working output is smoothed if true.
    /// </summary>
    public bool Smoothing
    {
        get => smoothing;
        set => smoothing = value;
    }

    [SerializeField]
    [Tooltip("The axis in which list movement occurs on.")]
    private ListScrollerAxis pageMovementAxis = ListScrollerAxis.X;

    /// <summary>
    /// The axis in which list movement occurs on.
    /// </summary>
    public override ListScrollerAxis PageMovementAxis
    {
        get => pageMovementAxis;

        set
        {
            if (value != pageMovementAxis)
            {
                pageMovementAxis = value;
                // To do - update scroll position
            }
        }
    }
    #endregion Serialized Fields

    #region Public Properties
    /// <summary>
    /// Get the current goal value.
    /// </summary>
    public override float Goal { get => _goal; }

    /// <summary>
    /// The final position to be attained.
    /// </summary>
    public Vector3 GoalPosition { get; private set; }

    /// <summary>
    /// The current position of the list.
    /// </summary>
    public Vector3 WorkingPosition
    {
        get
        {
            return transform.localPosition;
        }

        private set
        {
            TaskCompletionSource<bool> setTaskSource = null;
            lock (_scrollingTaskSourceLock)
            {
                if (transform.localPosition != value)
                {
                    transform.localPosition = value;
                    VisibleRange = GetVisibleRange(value);
                    if (Mathf.Approximately((GoalPosition - transform.localPosition).sqrMagnitude, 0.0f))
                    {
                        setTaskSource = _scrollingTaskSource;
                        _scrollingTaskSource = null;
                    }

                }
            }

            if (setTaskSource != null)
            {
                setTaskSource.TrySetResult(true);
            }
        }
    }

    /// <summary>
    /// The timestamp the solvers will use to calculate with.
    /// </summary>
    public float DeltaTime { get; set; }
    #endregion Public Properties

    #region Private Properties
    /// <summary>
    /// The last time updated
    /// </summary>
    private float LastUpdateTime { get; set; }

    /// <summary>
    /// Half the content size.
    /// </summary>
    private float ContentSizeHalf { get; set; }

    /// <summary>
    /// The number of item nodes per unit of content size (Total Nodes / Content Size).
    /// </summary>
    private float NodesPerScrollUnit { get; set; }

    /// <summary>
    /// The number of item nodes per page (NodesPerUnit * Page Size).
    /// </summary>
    private float NodesPerPage { get; set; }

    /// <summary>
    /// The number of nodes in movable direction
    /// </summary>
    private int NodesInMovableDirection { get; set; }

    /// <summary>
    /// The number of nodes in non movable direction
    /// </summary>
    private int NodesInNonMovableDirection { get; set; }
    #endregion Private Properties

    #region MonoBehavior Functions
    private void Awake()
    {
        _initialPosition = WorkingPosition;
        LastUpdateTime = DeltaTime = Time.deltaTime;
        SnapTo(0);
    }

    private void Update()
    {
        DeltaTime = Time.realtimeSinceStartup - LastUpdateTime;
        LastUpdateTime = Time.realtimeSinceStartup;
        UpdateWorkingPositionToGoal();
    }
    #endregion MonoBehavior Functions

    #region Public Functions
    /// <summary>
    /// Snap to praticular point in the list.
    /// </summary>
    /// <param name="progress">A value from 0.0 to 1.0.</param>
    public override void SnapTo(float progress)
    {
        _goal = progress;
        SnapTo(ProgressToPosition(progress));
    }

    /// <summary>
    /// Snap goal to particular point in the list.
    /// </summary>
    /// <param name="progress">A value from 0.0 to 1.0.</param>
    public override Task SnapGoalTo(float progress)
    {
        _goal = progress;
        return SnapGoalTo(ProgressToPosition(progress));
    }

    /// <summary>
    /// Handle the setting of list size and update the scroll area size.
    /// </summary>
    protected override void OnSetScrollSize(Vector2 listSize)
    {
        this._initialPosition = new Vector3(0, 0, _initialPosition.z);

        var listContainer = gameObject.GetComponent<ListContainer>();
        if (listContainer == null)
        {
            this.PageSize = 0;
            this.PageCount = 0;
            this.ContentSize = 0;
            this.ContentSizeHalf = 0;
            this.NodesPerScrollUnit = 0;
            this.NodesPerPage = 0;
            this.NodesInMovableDirection = 0;
            this.NodesInNonMovableDirection = 0;
            return;
        }

        var movementAxis = GetMovementAxis();
        var nonmovementAxis = GetNonMovementAxis();
        this.ContentSize = Vector3.Dot(movementAxis, listContainer.ContainerSize);
        this.ContentSizeHalf = this.ContentSize * 0.5f;
        this.PageSize = Vector3.Dot(movementAxis, listSize);
        this.PageCount = this.PageSize == 0 ? 0 : this.ContentSize / this.PageSize;

        // Calculate how many nodes are within a page
        this.NodesInMovableDirection = (int)Vector3.Dot(movementAxis, listContainer.ContainerItemSize);
        this.NodesInNonMovableDirection = (int)Vector3.Dot(nonmovementAxis, listContainer.ContainerItemSize);
        this.NodesPerPage = this.PageCount == 0 ? 0 : Mathf.Ceil(this.NodesInMovableDirection / this.PageCount) * this.NodesInNonMovableDirection;
        this.NodesPerScrollUnit = this.ContentSize == 0 ? 0 : this.NodesInMovableDirection / this.ContentSize;

        this._initialPosition += -0.5f * GetMovementAxis() * (this.ContentSize - this.PageSize);
        this.SnapTo(this._goal);
    }
    #endregion Public Functions

    #region Private Functions
    /// <summary>
    /// Calculate a goal position based on a progress value (0.0 - 1.0), the initial list position, the content size, 
    /// and the list size.
    /// </summary>
    private Vector3 ProgressToPosition(float progress)
    {
        return _initialPosition + (progress * GetMovementAxis() * (this.ContentSize - this.PageSize));
    }

    /// <summary>
    /// Snaps the solver to the desired pose.
    /// </summary>
    /// <remarks>
    /// SnapTo may be used to bypass smoothing to a certain position if the object is teleported or spawned.
    /// </remarks>
    private void SnapTo(Vector3 position)
    {
        SnapGoalTo(position);
        WorkingPosition = position;
    }

    /// <summary>
    /// SnapGoalTo only sets the goal orientation.  Not really useful.
    /// </summary>
    private Task SnapGoalTo(Vector3 position)
    {
        GoalPosition = position;

        Task result;
        lock (_scrollingTaskSourceLock)
        {
            if (_scrollingTaskSource == null)
            {
                _scrollingTaskSource = new TaskCompletionSource<bool>();
            }

            result = _scrollingTaskSource.Task;
        }
        return result;
    }

    /// <summary>
    /// Add an offset position to the target goal position.
    /// </summary>
    private void AddOffset(Vector3 offset)
    {
        GoalPosition += offset;
    }

    /// <summary>
    /// Lerps Vector3 source to goal.
    /// </summary>
    /// <remarks>
    /// Handles lerpTime of 0.
    /// </remarks>
    /// <returns></returns>
    private static Vector3 SmoothTo(Vector3 source, Vector3 goal, float deltaTime, float lerpTime)
    {
        return Vector3.Lerp(source, goal, lerpTime.Equals(0.0f) ? 1f : deltaTime / lerpTime);
    }

    /// <summary>
    /// Updates only the working position to goal with smoothing, if enabled
    /// </summary>
    private void UpdateWorkingPositionToGoal()
    {
        WorkingPosition = smoothing ? SmoothTo(WorkingPosition, GoalPosition, DeltaTime, moveLerpTime) : GoalPosition;
    }

    /// <summary>
    /// Get the visible range based on the current scroll position.
    /// </summary>
    private ListScrollerRange GetVisibleRange(Vector3 scrollPosition)
    {
        ListScrollerRange result = ListScrollerRange.Empty;
        result.startIndex = Mathf.RoundToInt(NodesPerScrollUnit * Vector3.Dot(GetMovementAxis(), scrollPosition - _initialPosition)) * this.NodesInNonMovableDirection;
        result.endIndex = Mathf.CeilToInt(result.startIndex + this.NodesPerPage);
        return result;

    }
    #endregion Private Functions
}
