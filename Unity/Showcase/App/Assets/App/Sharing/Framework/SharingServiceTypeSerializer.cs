// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// A class for serializing unmanaged types.
    /// </summary>
    public class SharingServiceTypeSerializer<T> : ISharingServiceSerializer where T : unmanaged
    {
        ISharingServiceBasicSerializer _serializer;

        public SharingServiceTypeSerializer(ISharingServiceBasicSerializer byteSerializer)
        {
            _serializer = byteSerializer;
        }

        /// <summary>
        /// Get the number of bytes needed to encode the given value.
        /// </summary>
        public int GetByteSize(object value)
        {
            if (!(value is T))
            {
                return 0;
            }

            return _serializer.GetByteSize<T>();
        }

        /// <summary>
        /// Serialize the given value to the byte array
        /// </summary>
        public void Serialize(object value, byte[] target, ref int offset)
        {
            if (!(value is T))
            {
                return;
            }

            _serializer.Serialize<T>((T)value, target, ref offset);
        }

        /// <summary>
        /// Deserialize the given value from a byte array
        /// </summary>
        public void Deserialize(out object value, byte[] source, ref int offset)
        {
            value = _serializer.Deserialize<T>(source, ref offset);
        }

        /// <summary>
        /// Convert object to string
        /// </summary>
        public string ToString(object value)
        {
            if (!(value is T))
            {
                return null;
            }

            return _serializer.SerializeToString<T>((T)value);
        }

        /// <summary>
        /// Convert string to object
        /// </summary>
        public bool FromString(string value, out object result)
        {
            return _serializer.DeserializeFromString<T>(value, out result);
        }
    }
}
