// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// A pool of T objects that can be used to serialize data.
/// </summary>
public class ObjectPool<T> where T : class, new()
{
    private AutoResetEvent _waitForBuffer = new AutoResetEvent(false);
    private T[] _pool;
    private int _size;

    public ObjectPool(int size)
    {
        _size = size;
        _pool = new T[size];

        for (int i = 0; i < size; i++)
        {
            _pool[i] = new T();
        }
    }

    /// <summary>
    /// Check out an object. This will block until an object becomes available.
    /// Once checked out, action will be invoked. After action completed, object
    /// is automatically checked in.
    /// </summary>
    public void CheckOut(Action<T> action)
    {
        T value = CheckOutWorker();
        try
        {
            action(value);
        }
        finally
        {
            CheckInWorker(value);
        }
    }

    /// <summary>
    /// Check out an object. This will block until an object becomes available.
    /// Once checked out, action will be invoked. After action completed, object
    /// is automatically checked in.
    /// </summary>
    public U CheckOut<U>(Func<T, U> action, int bufferLength = 0)
    {
        T buffer = CheckOutWorker();
        try
        {
            return action(buffer);
        }
        finally
        {
            CheckInWorker(buffer);
        }
    }

    /// <summary>
    /// Checkout object with a long term ownership
    /// </summary>
    public void CheckOut(Action<CheckoutArgs> action)
    {
        CheckoutArgs args = new CheckoutArgs(CheckOutWorker(), CheckInWorker);
        try
        {
            action(args);
        }
        finally
        {
            args.Dispose();
        }
    }

    /// <summary>
    /// Checkout buffer with a long term ownership
    /// </summary>
    public U CheckOut<U>(Func<CheckoutArgs, U> action)
    {
        CheckoutArgs args = new CheckoutArgs(CheckOutWorker(), CheckInWorker);
        try
        {
            return action(args);
        }
        finally
        {
            args.Dispose();
        }
    }
    /// <summary>
    /// Check out an object of type T. This will block until a T entry becomes available.
    /// This will fail if T doesn't implement IObjectPoolEntry<T>
    /// </summary>
    public T CheckOut()
    {
        T result = CheckOutWorker();

        if (!(result is IObjectPoolEntry<T>))
        {
            CheckInWorker(result);
            throw new InvalidOperationException("To use CheckOut() the entry must implement IObjectPoolEntry<T>");
        }

        return result;
    }

    /// <summary>
    /// Check out an object of type T. This will block until a T entry becomes available.
    /// </summary>
    private T CheckOutWorker()
    {
        T result = null;
        while (true)
        {
            lock (_pool)
            {
                for (int i = 0; i < _size; i++)
                {
                    if (_pool[i] != null)
                    {
                        result = _pool[i];
                        _pool[i] = null;
                        break;
                    }
                }
            }

            if (result != null)
            {
                if (result is IObjectPoolEntry<T>)
                {
                    var entry = (IObjectPoolEntry<T>)result;
                    entry.OnDisposed += OnEntryDisposed;
                    entry.OnCheckOut();
                }
                    
                break;
            }

            _waitForBuffer.WaitOne();
        }

        return result;
    }

    /// <summary>
    /// Check in a class object.
    /// </summary>
    private void CheckInWorker(T value)
    {
        if (value == null)
        {
            Debug.LogError("Trying to check-in a null buffer.");
            return;
        }

        if (value is IObjectPoolEntry<T>)
        {
            var entry = (IObjectPoolEntry<T>)value;
            entry.OnDisposed -= OnEntryDisposed;
            entry.OnCheckIn();
        }

        bool added = false;
        lock (_pool)
        {
            if (Array.IndexOf(_pool, value) < 0)
            {
                for (int i = 0; i < _size; i++)
                {
                    if (_pool[i] == null)
                    {
                        _pool[i] = value;
                        added = true;
                        break;
                    }
                }
            }
        }

        if (added)
        {
            _waitForBuffer.Set();
        }
    }

    private void OnEntryDisposed(T entry)
    {
        CheckInWorker(entry);
    }

    #region Public Struct
    public struct CheckoutArgs
    {
        private Action<T> _checkIn;

        public T Value { get; private set; }

        public CheckoutArgs(T value, Action<T> checkIn)
        {
            _checkIn = checkIn;
            Value = value;
        }

        internal void Dispose()
        {
            if (Value != null)
            {
                _checkIn?.Invoke(Value);
                Value = null;
            }
        }

        /// <summary>
        /// Move the object in this struct to pinned class which holds the object until freed
        /// </summary>
        public Pinned Move()
        {
            var result = new Pinned(Value, _checkIn);
            Value = null;
            return result;
        }
    }

    public class Pinned : IDisposable
    {
        private Action<T> _checkIn; 

        public T Value { get; private set; }

        public Pinned(T value, Action<T> checkIn)
        {
            _checkIn = checkIn;
            Value = value;
        }

        public void Dispose()
        {
            if (Value != null)
            {
                _checkIn?.Invoke(Value);
                Value = null;
            }
        }
    }
    #endregion Public Classes
}


/// <summary>
/// A pool entry, used for an entry to know when its checked out.
/// </summary>
public interface IObjectPoolEntry<T> : IDisposable
    where T : class, new()
{
    /// <summary>
    /// Event raised when item is disposed
    /// </summary>
    event Action<T> OnDisposed;

    /// <summary>
    /// Invoke on check out
    /// </summary>
    void OnCheckOut();

    /// <summary>
    /// Invoked on check in.
    /// </summary>
    void OnCheckIn();
}
