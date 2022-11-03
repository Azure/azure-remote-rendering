// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading;

public class CircularObjectBuffer<T> : IDisposable where T : class, new() 
{
    private T[] _entries = null;
    private int _read = 0;
    private int _write = 0;
    private object _lock = new object();
    private ManualResetEvent _canRead = new ManualResetEvent(false);

    public CircularObjectBuffer(uint size)
    {
        _entries = new T[size];
        for (int i = 0; i < size; i++)
        {
            _entries[i] = new T();
        }
    }       

    /// <summary>
    /// Release resources
    /// </summary>
    public void Dispose()
    {
        ManualResetEvent disposeThis;
        lock (_lock)
        {
            disposeThis = _canRead;
            _canRead = null;
        }

        if (disposeThis != null)
        {
            disposeThis.Set();
            disposeThis.Dispose();
        }
    }

    /// <summary>
    /// Is the queue empty
    /// </summary>
    public bool IsEmpty
    {
        get
        {
            lock (_lock)
            {
                return _read == _write;
            }
        }
    }

    /// <summary>
    /// Try to get a pointer to the next item to write to, but don't advance the write pointer.
    /// </summary>
    public bool TryStartWrite(out T entry)
    {
        lock (_lock)
        {
            int next = (_write + 1) % _entries.Length;
            if (next == _read)
            {
                entry = null;
                return false;
            }
            else
            {
                entry = _entries[_write];
                return true;
            }
        }
    }

    /// <summary>
    /// Try to advance the write pointer.
    /// </summary>
    public bool TryCommitWrite()
    {
        bool result;
        ManualResetEvent canRead;
        lock (_lock)
        {
            int next = (_write + 1) % _entries.Length;
            if (next == _read)
            {
                result = false;
            }
            else
            {
                _write = next;
                result = true;
            }

            canRead = _canRead;
        }

        if (result)
        {
            canRead?.Set();
        }

        return result;
    }

    /// <summary>
    /// Try to get a pointer to the next object to read from, but don't advance the read pointer.
    /// </summary>
    public bool TryStartRead(out T entry)
    {
        lock (_lock)
        {
            if (_read == _write)
            {
                entry = null;
                return false;
            }
            else
            {
                entry = _entries[_read];
                return true;
            }
        }
    }

    /// <summary>
    /// Wait for a pointer to the next object to read from, but don't advance the read pointer.
    /// </summary>
    public bool WaitStartRead(out T entry)
    {
        ManualResetEvent canRead;
        lock (_lock)
        {
            canRead = _canRead;
            if (canRead == null)
            {
                entry = null;
                return false;
            }
        }

        canRead.WaitOne();
        return TryStartRead(out entry);
    }

    /// <summary>
    /// Try to advance the read pointer.
    /// </summary>
    public bool TryCommitRead()
    {
        bool result;
        ManualResetEvent canRead;
        lock (_lock)
        {
            if (_read == _write)
            {
                result = false;
            }
            else
            {
                _read = (_read + 1) % _entries.Length;
                result = true;
            }

            canRead = _canRead;
        }

        if (_read == _write)
        {
            canRead?.Reset();
        }

        return result;
    }
}
