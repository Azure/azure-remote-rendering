// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class ProgressStatus
{
    private float _currentProgress = 0.0f;
    private float _maxProgress = 0.0f;

    /// <summary>
    /// Get the total loading progress
    /// </summary>
    public float Progress
    {
        get
        {
            if (_maxProgress == 0.0f)
            {
                return 1.0f;
            }

            float progress = (float)(_currentProgress / _maxProgress);
            if (Mathf.Approximately(progress, 1.0f) || progress > 1.0f)
            {
                return 1.0f;
            }

            return progress;
        }
    }

    /// <summary>
    /// Add or remote progress change handlers.
    /// </summary>
    public event EventHandler<ProgressTaskChangeArgs> ProgressChanged;

    /// <summary>
    /// Fired when progress reaches 100%
    /// </summary>
    public event EventHandler<ProgressTaskChangeArgs> Completed;

    /// <summary>
    /// Update the max value of the inner progress value.
    /// </summary>
    protected void UpdateMax(float max)
    {
        float oldTotalProgress = Progress;
        _maxProgress = max;
        float newTotalProgress = Progress;
        if (oldTotalProgress != newTotalProgress)
        {
            ProgressChanged?.Invoke(this, new ProgressTaskChangeArgs(oldTotalProgress, newTotalProgress));
        }

        CheckCompleted();
    }

    /// <summary>
    /// Update the inner progress value;
    /// </summary>
    protected void UpdateProgress(float progress)
    {
        float oldTotalProgress = Progress;
        _currentProgress = progress;
        float newTotalProgress = Progress;
        if (oldTotalProgress != newTotalProgress)
        {
            ProgressChanged?.Invoke(this, new ProgressTaskChangeArgs(oldTotalProgress, newTotalProgress));
        }

        CheckCompleted();
    }

    /// <summary>
    /// Check if progress has completed, and if so fire compelted event.
    /// </summary>
    private void CheckCompleted()
    {
        if (Mathf.Approximately(_currentProgress, _maxProgress) || _currentProgress >= _maxProgress)
        {
            float oldTotalProgress = Progress;
            _currentProgress = _maxProgress;
            Completed?.Invoke(this, new ProgressTaskChangeArgs(oldTotalProgress, Progress));
            _currentProgress = 0.0f;
            _maxProgress = 0.0f;
        }
    }
}

/// <summary>
/// A class that exposes IAsync operation progress along side an awaitable task.
/// </summary>
public class ModelProgressStatus : ProgressStatus
{
    public ModelProgressStatus()
    {
        UpdateMax(1.0f);
    }

    public void OnProgressUpdated(float progress)
    {
        UpdateProgress(progress);
    }
}

/// <summary>
/// A helper class used to track the progress of many progress tasks
/// </summary>
public class ProgressCollection : ProgressStatus
{
    private readonly List<ProgressStatus> _inners = new List<ProgressStatus>();
    private Timer _updateTimer = null;
    private static readonly TimeSpan _timerDueTime = TimeSpan.FromSeconds(1.0 / 30.0);
    private static readonly TimeSpan _timerPeriosDueTime = TimeSpan.FromSeconds(1.0 / 30.0);

    public ProgressCollection() 
    {
    }

    public void Add(ProgressStatus item)
    {
        RegisterInner(item);
        UpdateMax(_inners.Count);
        StartUpdates();
    }

    public void Clear()
    {
        foreach (var item in _inners)
        {
            UnregisterInner(item);
        }
        _inners.Clear();
        UpdateMax(0.0f);
        UpdateProgress(0.0f);
        StopUpdates();
    }

    private void StartUpdates()
    {
        if (_updateTimer == null)
        {
            _updateTimer = new Timer(new TimerCallback(UpdateTick), SynchronizationContext.Current, _timerDueTime, _timerPeriosDueTime);
        }
    }

    private void UpdateTick(object state)
    {
        SynchronizationContext context = state as SynchronizationContext;
        if (context != null)
        {
            context.Send(contextState => UpdateProgress(), null);
        }
        else
        {
            UpdateProgress();
        }
    }

    private void StopUpdates()
    {
        if (_updateTimer != null)
        {
            Debug.Assert(UnityEngine.WSA.Application.RunningOnAppThread(), "Not running on app thread.");
            _updateTimer.Dispose();
            _updateTimer = null;
        }
    }

    /// <summary>
    /// Register an additional task to monitor
    /// </summary>
    private void RegisterInner(ProgressStatus inner)
    {
        if (_inners.Contains(inner))
        {
            return;
        }

        _inners.Add(inner);
        inner.ProgressChanged += InnerProgress;
        inner.Completed += InnerCompleted;
    }


    /// <summary>
    /// Unregister an additional task to monitor
    /// </summary>
    private void UnregisterInner(ProgressStatus inner)
    {
        inner.Completed -= InnerCompleted;
        inner.ProgressChanged -= InnerProgress;
    }

    /// <summary>
    /// Update the total progress
    /// </summary>
    private void UpdateProgress()
    {
        float total = 0;
        foreach (var item in _inners)
        {
            total += item.Progress;
        }
        UpdateProgress(total);

        // If progress reaches 100%, clear inners
        if (total >= _inners.Count)
        {
            Clear();
        }
    }

    /// <summary>
    /// Update a task's progress
    /// </summary>
    private void InnerProgress(object sender, ProgressTaskChangeArgs args)
    {
        StartUpdates();
    }

    /// <summary>
    /// Clean up a inner task registration.
    /// </summary>
    private void InnerCompleted(object sender, ProgressTaskChangeArgs args)
    {
        UnregisterInner((ProgressStatus)sender);
        StartUpdates();
    }
}

public struct ProgressTaskChangeArgs
{
    public ProgressTaskChangeArgs(float oldValue, float newValue)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    public float OldValue { get; }

    public float NewValue { get; }
}
