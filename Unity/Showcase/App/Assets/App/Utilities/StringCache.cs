// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// This stores strings that are used often. These strings can be looked up by a subarray char[] or a substring. 
/// This is typically used along side a BufferPool<char> and a larger string
/// </summary>
public class StringCache
{
    int _capacity;
    int _removeAmount;
    float _removeFrequency = 0.2f;
    List<LinkedListNode<string>> _cache;
    LinkedList<string> _lruList;
    CacheLookupComparer _cacheComparer = new CacheLookupComparer();
    CacheTrimComparer _trimComparer = new CacheTrimComparer();

    public StringCache(int capacity = 100)
    {
        _removeAmount = (int)(capacity * _removeFrequency);
        _capacity = capacity + _removeAmount;
        _cache = new List<LinkedListNode<string>>(_capacity);
        _lruList = new LinkedList<string>();
    }

    /// <summary>
    /// Find a cached string using a subarray. If there is a cache miss, a new string is created and cached.
    /// </summary>
    /// <param name="chars">The array to use for search</param>
    /// <param name="index">The start index of the subarray</param>
    /// <param name="count">The length of the subarray</param>
    public string Find(char[] chars, int index, int count)
    {
        string result;
        lock (_cacheComparer)
        {
            _cacheComparer.chars = chars;
            _cacheComparer.str = null;
            _cacheComparer.index = index;
            _cacheComparer.count = count;
            result = Find(() => new string(chars, index, count));
        }
        return result;
    }

    /// <summary>
    /// Find a cached string using a substring. If there is a cache miss, a new string is created and cached.
    /// </summary>
    /// <param name="chars">The string to use for search</param>
    /// <param name="index">The start index of the substring</param>
    /// <param name="count">The length of the substring</param>
    public string Find(string str, int index, int count)
    {
        string result;
        lock (_cacheComparer)
        {
            _cacheComparer.chars = null;
            _cacheComparer.str = str;
            _cacheComparer.index = index;
            _cacheComparer.count = count;
            result = Find(() => str.Substring(index, count));
        }
        return result;
    }

    private string Find(Func<string> alloc)
    {
        LinkedListNode<string> node;

        int hit = _cache.BinarySearch(null, _cacheComparer);

        if (hit < 0)
        {
            node = new LinkedListNode<string>(alloc());
            _cache.Insert(~hit, node);
            _lruList.AddLast(node);
        }
        else
        {
            node = _cache[hit];
            _lruList.Remove(node);
            _lruList.AddLast(node);
        }

        Trim();
        return node.Value;
    }

    private void Trim()
    {
        if (_cache.Count < _capacity)
        {
            return;
        }

        for (int i = 0; i < _removeAmount; i++)
        {
            var trim = _lruList.First;
            _lruList.RemoveFirst();
            int hit = _cache.BinarySearch(trim, _trimComparer);

            if (hit >= 0)
            {
                _cache.RemoveAt(hit);
            }
            else
            {
                Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "[StringCache] Binary search failed to find trim cache value '{0}'.", trim.Value);
                _lruList.AddLast(trim);
            }
        }
    }

    private class CacheLookupComparer : IComparer<LinkedListNode<string>>
    {
        /// <summary>
        /// The chars to use for search
        /// </summary>
        public char[] chars = null;

        /// <summary>
        /// The string to use for search
        /// </summary>
        public string str = null;

        /// <summary>
        /// The index to start search
        /// </summary>
        public int index = 0;

        /// <summary>
        /// The number of chars to compare
        /// </summary>
        public int count = 0;

        public int Compare(LinkedListNode<string> xNode, LinkedListNode<string> yNode)
        {
            string cachedString = xNode != null ? xNode.Value : yNode.Value;
            int cachedStringLength = cachedString.Length;
            int inputMax = index + count;

            if (chars != null)
            {
                for (int i = 0, j = index; i < cachedStringLength && j < inputMax; i++, j++)
                {
                    if (cachedString[i] < chars[j])
                    {
                        return -1;
                    }
                    else if (cachedString[i] > chars[j])
                    {
                        return 1;
                    }
                }
            }
            else
            {
                for (int i = 0, j = index; i < cachedStringLength && j < inputMax; i++, j++)
                {
                    if (cachedString[i] < str[j])
                    {
                        return -1;
                    }
                    else if (cachedString[i] > str[j])
                    {
                        return 1;
                    }
                }
            }

            if (cachedStringLength < count)
            {
                return -1;
            }
            else if (cachedStringLength > count)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }
    }

    private class CacheTrimComparer : IComparer<LinkedListNode<string>>
    {
        public int Compare(LinkedListNode<string> xNode, LinkedListNode<string> yNode)
        {
            return StringComparer.Ordinal.Compare(xNode.Value, yNode.Value);
        }
    }
}
