// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A list component that can scroll a list container so to reveal more list items.
/// </summary>
public abstract class ListScrollerBase : MonoBehaviour
{
    #region Serialize Fields
    [Header("List Scroller Translate Events")]

    [SerializeField]
    [Tooltip("Event raised when the list scroller sizes have been updated.")]
    private UnityEvent sizeChanged = new UnityEvent();

    /// <summary>
    /// Event raised when the list scroller sizes have been updated.
    /// </summary>
    public UnityEvent SizeChanges => sizeChanged;
    #endregion

    #region Public Properties
    /// <summary>
    /// Get the visible index range.
    /// </summary>
    public ListScrollerRange VisibleRange { get; protected set; }

    /// <summary>
    /// The total page count.
    /// </summary>
    public float PageCount { get; protected set; }

    /// <summary>
    /// Get the size of a single page increment
    /// </summary>
    public float PageSize { get; protected set; }

    /// <summary>
    /// The total size of the content
    /// </summary>
    public float ContentSize { get; protected set; }

    /// <summary>
    /// The page's movement axis
    /// </summary>
    public abstract ListScrollerAxis PageMovementAxis { get; set; }

    /// <summary>
    /// A value that represents the goal scroll position.
    /// </summary>
    public abstract float Goal { get; }

    /// <summary>
    /// Snap the goal scroll position to a value from 0.0 to 1.0.
    /// </summary>
    public abstract Task SnapGoalTo(float progress);

    /// <summary>
    /// Snap the goal and the current scroll position to a value from 0.0 to 1.0.
    /// </summary>
    public abstract void SnapTo(float progress);
    #endregion Public Properties

    #region Public Functions
    /// <summary>
    /// Set and update the scroll area size.
    /// </summary>
    public void SetScrollSize(Vector2 listSize)
    {
        OnSetScrollSize(listSize);
        sizeChanged?.Invoke();
    }

    /// <summary>
    /// Get the axis which movement occurs along.
    /// </summary>
    public Vector3 GetMovementAxis()
    {
        switch (PageMovementAxis)
        {
            case ListScrollerAxis.X:
                return Vector3.right;

            case ListScrollerAxis.Y:
                return Vector3.up;

            case ListScrollerAxis.Z:
                return Vector3.forward;

            default:
                throw new System.NotSupportedException();
        }
    }

    /// <summary>
    /// Get the axes that movement doesn't occur along.
    /// </summary>
    public Vector3 GetNonMovementAxis()
    {
        switch (PageMovementAxis)
        {
            case ListScrollerAxis.X:
                return new Vector3(0, 1, 1);

            case ListScrollerAxis.Y:
                return new Vector3(1, 0, 1);

            case ListScrollerAxis.Z:
                return new Vector3(1, 1, 0);

            default:
                throw new System.NotSupportedException();
        }
    }
    #endregion Public Functions

    #region Protected Functions
    /// <summary>
    /// Handle the setting of list size and update the scroll area size.
    /// </summary>
    protected abstract void OnSetScrollSize(Vector2 listSize);
    #endregion Protected Functions
}

public struct ListScrollerRange
{
    public int startIndex;
    public int endIndex;

    public ListScrollerRange(int startIndex, int endIndex)
    {
        this.startIndex = startIndex;
        this.endIndex = endIndex;
    }
    public override bool Equals(object obj) => obj is ListScrollerRange other && this.Equals(other);

    public bool Equals(ListScrollerRange other) =>
        other.endIndex == endIndex &&
        other.startIndex == startIndex;

    public override int GetHashCode() => 
        (startIndex, endIndex).GetHashCode();

    public static bool operator ==(ListScrollerRange lhs, ListScrollerRange rhs) => lhs.Equals(rhs);

    public static bool operator !=(ListScrollerRange lhs, ListScrollerRange rhs) => !(lhs == rhs);

    public static ListScrollerRange Empty { get; } = new ListScrollerRange(-1, 0);
}
