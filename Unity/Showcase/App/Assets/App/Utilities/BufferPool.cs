// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;


/// <summary>
/// A pool of T arrays that can be used to serialize data.
/// </summary>
public class BufferPool<T> where T : unmanaged
{
    private AutoResetEvent _waitForBuffer = new AutoResetEvent(false);
    private T[][][] _pools;
    private int[] _poolSizes;


    private static (int size, int poolCount)[] _poolVariations =
    {
        (sizeof(int) * 8, 5),
        (sizeof(int) * 16, 5),
        (sizeof(int) * 32, 5),
        (sizeof(int) * 64, 5),
        (sizeof(int) * 128, 2),
        (sizeof(int) * 256, 2),
        (sizeof(int) * 512, 2),
    };

    private static PoolVariantionComparer _poolVariationsComparer =
        new PoolVariantionComparer();

    private int MaxBufferSize => _poolVariations[_poolVariations.Length - 1].size;

    public BufferPool()
    {
        int variations = _poolVariations.Length;
        _pools = new T[variations][][];
        _poolSizes = new int[variations];

        for (int i = 0; i < variations; i++)
        {
            var poolVariation = _poolVariations[i];
            _pools[i] = new T[poolVariation.poolCount][];
            _poolSizes[i] = poolVariation.size;
            for (int j = 0; j < poolVariation.poolCount; j++)
            {
                _pools[i][j] = new T[poolVariation.size];
            }
        }
    }

    /// <summary>
    /// Check out a byte array. This will block until a buffer becomes available.
    /// Once checked out, action will be invoked. After action completed, buffer
    /// is automatically checked in.
    /// </summary>
    public void CheckOut(Action<T[]> action, int bufferLength = 0)
    {
        if (bufferLength < 0)
        {
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "Requested a negative buffer size. A possible numeric overflow.");
            return;
        }

        if (bufferLength > MaxBufferSize)
        {
            Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "Buffer request is too large to use the pool.");
            action(new T[bufferLength]);
            return;
        }

        T[] buffer = CheckOut(bufferLength);
        try
        {
            action(buffer);
        }
        finally
        {
            CheckIn(buffer);
        }
    }

    /// <summary>
    /// Check out a byte array. This will block until a buffer becomes available.
    /// Once checked out, action will be invoked. After action completed, buffer
    /// is automatically checked in.
    /// </summary>
    public U CheckOut<U>(Func<T[], U> action, int bufferLength = 0)
    {
        if (bufferLength < 0)
        {
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "Requested a negative buffer size. A possible numeric overflow.");
            return default;
        }

        if (bufferLength > MaxBufferSize)
        {
            Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "Buffer request is too large to use the pool.");
            return action(new T[bufferLength]);
        }

        T[] buffer = CheckOut(bufferLength);
        try
        {
            return action(buffer);
        }
        finally
        {
            CheckIn(buffer);
        }
    }

    /// <summary>
    /// Perform a long term checkout. Caller is responsible for clean-up
    /// </summary>
    public Pinned LongTerm(int bufferLength = 0)
    {
        if (bufferLength < 0)
        {
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "Requested a negative buffer size. A possible numeric overflow.");
            return default;
        }

        if (bufferLength > MaxBufferSize)
        {
            Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "Buffer request is too large to use the pool.");
            return new Pinned(CheckOut(bufferLength), (b) => { });
        }

        return new Pinned(CheckOut(bufferLength), CheckIn);
    }

    /// <summary>
    /// Checkout buffer with a long term ownership
    /// </summary>
    public void CheckOut(Action<CheckoutArgs> action, int bufferLength = 0)
    {
        if (bufferLength < 0)
        {
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "Requested a negative buffer size. A possible numeric overflow.");
            return;
        }

        if (bufferLength > MaxBufferSize)
        {
            Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "Buffer request is too large to use the pool.");
            action(new CheckoutArgs(CheckOut(bufferLength), (b) => { }));
            return;
        }

        CheckoutArgs args = new CheckoutArgs(CheckOut(bufferLength), CheckIn);
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
    public U CheckOut<U>(Func<CheckoutArgs, U> action, int bufferLength = 0)
    {
        if (bufferLength < 0)
        {
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "Requested a negative buffer size. A possible numeric overflow.");
            return default;
        }

        if (bufferLength > MaxBufferSize)
        {
            Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "Buffer request is too large to use the pool.");
            return action(new CheckoutArgs(CheckOut(bufferLength), (b) => { }));
        }

        CheckoutArgs args = new CheckoutArgs(CheckOut(bufferLength), CheckIn);
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
    /// Check out a byte array. This will block until a buffer becomes available.
    /// </summary>
    private T[] CheckOut(int bufferLength = -1)
    {
        if (bufferLength > MaxBufferSize)
        {
            throw new IndexOutOfRangeException("The given buffer size is too big.");
        }

        int variantionEntry = Array.BinarySearch(_poolVariations, (bufferLength, 0), _poolVariationsComparer);
        if (variantionEntry < 0)
        {
            variantionEntry = ~variantionEntry;
        }

        T[] result = null;
        while (true)
        {
            lock (_pools)
            {
                T[][] pools = _pools[variantionEntry];
                for (int i = 0; i < pools.Length; i++)
                {
                    if (pools[i] != null)
                    {
                        result = pools[i];
                        pools[i] = null;
                        break;
                    }
                }
            }

            if (result != null)
            {
                break;
            }

            _waitForBuffer.WaitOne();
        }

        for (int i = 0; i < result.Length; i++)
        {
            result[i] = default;
        }

        return result;
    }

    /// <summary>
    /// Check in a byte array.
    /// </summary>
    private void CheckIn(T[] buffer)
    {
        if (buffer == null)
        {
            Debug.LogError("Trying to check-in a null buffer.");
            return;
        }

        int variantionEntry = Array.BinarySearch(_poolVariations, (buffer.Length, 0), _poolVariationsComparer);
        if (variantionEntry < 0)
        {
            Debug.LogWarning("Trying to check-in a buffer that doesn't match a pool variation size.");
            variantionEntry = ~variantionEntry;
            buffer = new T[_poolVariations[variantionEntry].size];
        }

        bool added = false;
        lock (_pools)
        {
            T[][] pools = _pools[variantionEntry];
            for (int i = 0; i < pools.Length; i++)
            {
                if (pools[i] == null)
                {
                    pools[i] = buffer;
                    added = true;
                    break;
                }
            }
        }

        if (added)
        {
            _waitForBuffer.Set();
        }
    }

    #region Public Struct
    public struct CheckoutArgs
    {
        private Action<T[]> _checkIn;

        public T[] Buffer { get; private set; }

        public CheckoutArgs(T[] buffer, Action<T[]> checkIn)
        {
            _checkIn = checkIn;
            Buffer = buffer;
        }

        internal void Dispose()
        {
            if (Buffer != null)
            {
                _checkIn?.Invoke(Buffer);
                Buffer = null;
            }
        }

        /// <summary>
        /// Move the buffer in this struct to pinned class which holds the buffer until freed
        /// </summary>
        public Pinned Move()
        {
            var result = new Pinned(Buffer, _checkIn);
            Buffer = null;
            return result;
        }
    }

    public struct Pinned : IDisposable
    {
        private Action<T[]> _checkIn; 

        public T[] Buffer { get; private set; }

        public Pinned(T[] buffer, Action<T[]> checkIn)
        {
            _checkIn = checkIn;
            Buffer = buffer;
        }

        public void Dispose()
        {
            if (Buffer != null)
            {
                _checkIn?.Invoke(Buffer);
                Buffer = null;
            }
        }
    }
    #endregion Public Classes

    #region Private Classes
    private class PoolVariantionComparer : IComparer<(int size, int poolCount)>
    {
        public int Compare((int size, int poolCount) x, (int size, int poolCount) y)
        {
            if (x.size < y.size)
            {
                return -1;
            }
            else if (x.size > y.size)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }
    }
    #endregion Private Classes
}
