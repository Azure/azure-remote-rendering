// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// A class for serializing strings.
    /// </summary>
    public class SharingServiceStringSerializer : ISharingServiceSerializer
    {
        ISharingServiceBasicSerializer _serializer;

        public SharingServiceStringSerializer(ISharingServiceBasicSerializer byteSerializer)
        {
            _serializer = byteSerializer;
        }

        /// <summary>
        /// Get the number of bytes needed to encode the given value.
        /// </summary>
        public int GetByteSize(object value)
        {
            return _serializer.GetByteSize((string)value);
        }

        /// <summary>
        /// Serialize the given value to the byte array
        /// </summary>
        public void Serialize(object value, byte[] target, ref int offset)
        {
            _serializer.Serialize((string)value, target, ref offset);
        }

        /// <summary>
        /// Deserialize the given value from a byte array
        /// </summary>
        public void Deserialize(out object value, byte[] source, ref int offset)
        {
            string result;
            _serializer.Deserialize(out result, source, ref offset);
            value = result;
        }

        /// <summary>
        /// Convert object to string
        /// </summary>
        public string ToString(object value)
        {
            return value as string;
        }

        /// <summary>
        /// Convert string to object
        /// </summary>
        public bool FromString(string value, out object result)
        {
            result = value;
            return true;
        }
    }
}
