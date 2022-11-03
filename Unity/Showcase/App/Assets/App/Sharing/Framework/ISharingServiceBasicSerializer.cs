// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// Helpers for basic serialization to byte arrays and strings.
    /// </summary>
    public interface ISharingServiceBasicSerializer
    {
        /// <summary>
        /// Get teh byte size of a given type
        /// </summary>
        int GetByteSize<T>() where T : unmanaged;

        /// <summary>
        /// Get the number of bytes to serialize the given type
        /// </summary>
        int GetByteSize(string value);

        /// <summary>
        /// Get the number of bytes needed to encode a pose.
        /// </summary>
        int GetByteSize(ref Pose pose);

        /// <summary>
        /// Get the number of bytes needed to encode a vector.
        /// </summary>
        int GetByteSize(ref Vector3 pose);

        /// <summary>
        /// Get the number of bytes needed to encode a Guid.
        /// </summary>
        int GetByteSize(ref Guid guid);

        /// <summary>
        /// Get the number of bytes needed to encode a DateTime.
        /// </summary>
        int GetByteSize(ref DateTime dateTime);

        /// <summary>
        /// Get the number of bytes needed to encode a TimeSpan.
        /// </summary>
        int GetByteSize(ref TimeSpan timeSpan);

        /// <summary>
        /// Serialize a value to a given byte array
        /// </summary>
        unsafe void Serialize<T>(T value, byte[] target, ref int offset) where T : unmanaged;

        /// <summary>
        /// Serialize a string to a given byte array
        /// </summary>
        void Serialize(string value, byte[] target, ref int offset);

        /// <summary>
        /// Serialize a Pose into the target byte[].
        /// </summary>
        void Serialize(ref Pose pose, byte[] target, ref int offset);

        /// <summary>
        /// Serialize a Vector3 into the target byte[].
        /// </summary>
        void Serialize(ref Vector3 value, byte[] target, ref int offset);

        /// <summary>
        /// Serialize a Quaternion into the target byte[].
        /// </summary>
        void Serialize(ref Quaternion value, byte[] target, ref int offset);

        /// <summary>
        /// Serialize an unmanaged type to a string.
        /// </summary>
        string SerializeToString<T>(T value) where T : unmanaged;

        /// <summary>
        /// Deserialize a value from a source byte[].
        /// </summary>
        unsafe T Deserialize<T>(byte[] source, ref int offset) where T : unmanaged;

        /// <summary>
        /// Deserialize a bool value from a source byte[].
        /// </summary>
        void Deserialize(out bool value, byte[] source, ref int offset);

        /// <summary>
        /// Deserialize a short value from a source byte[].
        /// </summary>
        void Deserialize(out short value, byte[] source, ref int offset);

        /// <summary>
        /// Deserialize a int value from a source byte[].
        /// </summary>
        void Deserialize(out int value, byte[] source, ref int offset);

        /// <summary>
        /// Deserialize a float from a source byte[].
        /// </summary>
        void Deserialize(out float value, byte[] source, ref int offset);

        /// <summary>
        /// Deserialize a string from a source byte[].
        /// </summary>
        void Deserialize(out string value, byte[] source, ref int offset);

        /// <summary>
        /// Deserialize a Pose from a source byte[].
        /// </summary>
        void Deserialize(out Pose pose, byte[] source, ref int offset);

        /// <summary>
        /// Serialize a Vector3 from a source byte[].
        /// </summary>
        void Deserialize(out Vector3 value, byte[] source, ref int offset);

        /// <summary>
        /// Serialize a Quaternion from a source byte[].
        /// </summary>
        void Deserialize(out Quaternion value, byte[] source, ref int offset);

        /// <summary>
        /// Deserialize a string to a native type.
        /// </summary>
        bool DeserializeFromString<T>(string value, out object result) where T : unmanaged;
    }
}
