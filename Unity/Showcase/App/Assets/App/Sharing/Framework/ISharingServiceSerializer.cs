// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    public interface ISharingServiceSerializer
    {
        /// <summary>
        /// Get the number of bytes needed to encode the given value.
        /// </summary>
        int GetByteSize(object value);

        /// <summary>
        /// Serialize the given value to the byte array
        /// </summary>
        void Serialize(object value, byte[] target, ref int offset);

        /// <summary>
        /// Deserialize the given value from a byte array
        /// </summary>
        void Deserialize(out object value, byte[] source, ref int offset);

        /// <summary>
        /// Convert object to string
        /// </summary>
        string ToString(object value);

        /// <summary>
        /// Convert string to object
        /// </summary>
        bool FromString(string value, out object result);
    }
}