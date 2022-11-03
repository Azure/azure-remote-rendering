// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// Helpers for basic serialization to byte arrays and strings.
    /// </summary>
    public class SharingServiceBasicSerializer : ISharingServiceBasicSerializer
    {
        private BufferPool<char> _charPool = new BufferPool<char>();
        private StringCache _stringCache = new StringCache(100);

        static SharingServiceBasicSerializer()
        {
            TypeDescriptor.AddAttributes(typeof(Color), new TypeConverterAttribute(typeof(ColorConverter)));
        }

        /// <summary>
        /// Get the number of bytes to serialize the given type
        /// </summary>
        public unsafe int GetByteSize<T>() where T : unmanaged
        {
            if (typeof(T) == typeof(Pose))
            {
                return (3 + 4) * sizeof(float);
            }
            else if (typeof(T) == typeof(Vector3))
            {
                return 3 * sizeof(float);
            }
            else if (typeof(T) == typeof(Quaternion))
            {
                return 4 * sizeof(float);
            }
            else if (typeof(T) == typeof(Guid))
            {
                // size of byte[] length + data
                return sizeof(int) + Guid.Empty.ToByteArray().Length;
            }
            else if (typeof(T) == typeof(DateTime))
            {
                // will send DateTime as ticks
                return sizeof(long);
            }
            else if (typeof(T) == typeof(TimeSpan))
            {
                // will send TimeSpan as ticks
                return sizeof(long);
            }
            else
            {
                return sizeof(T);
            }
        }

        /// <summary>
        /// Get the number of bytes to serialize the given type
        /// </summary>
        public int GetByteSize(string value)
        {
            return sizeof(int) + (value == null ? 0 : Encoding.UTF8.GetByteCount(value));
        }

        /// <summary>
        /// Get the number of bytes needed to encode a pose.
        /// </summary>
        public int GetByteSize(ref Pose pose)
        {
            return GetByteSize<Pose>();
        }

        /// <summary>
        /// Get the number of bytes needed to encode a vector.
        /// </summary>
        public int GetByteSize(ref Vector3 pose)
        {
            return GetByteSize<Vector3>();
        }

        /// <summary>
        /// Get the number of bytes needed to encode a Guid.
        /// </summary>
        public int GetByteSize(ref Guid guid)
        {
            return GetByteSize<Guid>();
        }

        /// <summary>
        /// Get the number of bytes needed to encode a DateTime.
        /// </summary>
        public int GetByteSize(ref DateTime dateTime)
        {
            return GetByteSize<DateTime>();
        }

        /// <summary>
        /// Get the number of bytes needed to encode a TimeSpan.
        /// </summary>
        public int GetByteSize(ref TimeSpan timeSpan)
        {
            return GetByteSize<TimeSpan>();
        }

        /// <summary>
        /// Serialize a value to a given byte array
        /// </summary>
        public unsafe void Serialize<T>(T value, byte[] target, ref int offset) where T : unmanaged
        {
            int sizeNeeded = sizeof(T);
            if (target.Length < offset + sizeNeeded)
            {
                throw new ArgumentOutOfRangeException();
            }

            fixed (byte* ptr = target)
            {
                byte* write = ptr + offset;
                *(T*)write = value;
            }

            offset += sizeNeeded;
        }

        /// <summary>
        /// Serialize a string to a given byte array
        /// </summary>
        public void Serialize(string value, byte[] target, ref int offset)
        {
            int sizeNeeded = GetByteSize(value) + sizeof(int);
            if (target.Length < offset + sizeNeeded)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (value == null)
            {
                Serialize(0, target, ref offset);
            }
            else
            {
                int byteCount = Encoding.UTF8.GetByteCount(value);
                Serialize(byteCount, target, ref offset);
                offset += Encoding.UTF8.GetBytes(value, charIndex: 0, value.Length, target, offset);
            }
        }

        /// <summary>
        /// Serialize a Pose into the target byte[].
        /// </summary>
        public void Serialize(ref Pose pose, byte[] target, ref int offset)
        {
            Serialize(pose.position.x, target, ref offset);
            Serialize(pose.position.y, target, ref offset);
            Serialize(pose.position.z, target, ref offset);
            Serialize(pose.rotation.w, target, ref offset);
            Serialize(pose.rotation.x, target, ref offset);
            Serialize(pose.rotation.y, target, ref offset);
            Serialize(pose.rotation.z, target, ref offset);
        }

        /// <summary>
        /// Serialize a Vector3 into the target byte[].
        /// </summary>
        public void Serialize(ref Vector3 value, byte[] target, ref int offset)
        {
            Serialize(value.x, target, ref offset);
            Serialize(value.y, target, ref offset);
            Serialize(value.z, target, ref offset);
        }

        /// <summary>
        /// Serialize a Quaternion into the target byte[].
        /// </summary>
        public void Serialize(ref Quaternion value, byte[] target, ref int offset)
        {
            Serialize(value.w, target, ref offset);
            Serialize(value.x, target, ref offset);
            Serialize(value.y, target, ref offset);
            Serialize(value.z, target, ref offset);
        }

        /// <summary>
        /// Serialize an unmanaged type to a string.
        /// </summary>
        public string SerializeToString<T>(T value) where T : unmanaged
        {
            var converter = TypeDescriptor.GetConverter(typeof(T));
            return converter.ConvertToInvariantString(value);
        }

        /// <summary>
        /// Deserialize a value from a source byte[].
        /// </summary>
        public unsafe T Deserialize<T>(byte[] source, ref int offset) where T : unmanaged
        {
            int bytesNeeded = GetByteSize<T>();

            if (source.Length < (offset + bytesNeeded))
            {
                throw new ArgumentOutOfRangeException();
            }

            T value = new T();
            fixed (byte* ptr = source)
            {
                byte* read = ptr + offset;
                value = *(T*)read;
            }

            offset += bytesNeeded;
            return value;
        }

        /// <summary>
        /// Deserialize a bool from a source byte[].
        /// </summary>
        public void Deserialize(out bool value, byte[] source, ref int offset)
        {
            value = BitConverter.ToBoolean(source, offset);
            offset += sizeof(bool);
        }

        /// <summary>
        /// Deserialize a short from a source byte[].
        /// </summary>
        public void Deserialize(out short value, byte[] source, ref int offset)
        {
            value = BitConverter.ToInt16(source, offset);
            offset += sizeof(short);
        }

        /// <summary>
        /// Deserialize a int value from a source byte[].
        /// </summary>
        public void Deserialize(out int value, byte[] source, ref int offset)
        {
            value = BitConverter.ToInt32(source, offset);
            offset += sizeof(int);
        }

        /// <summary>
        /// Deserialize a float value from a source byte[].
        /// </summary>
        public void Deserialize(out float value, byte[] source, ref int offset)
        {
            value = BitConverter.ToSingle(source, offset);
            offset += sizeof(float);
        }

        /// <summary>
        /// Deserialize a string value from a source byte[].
        /// </summary>
        public void Deserialize(out string value, byte[] source, ref int offset)
        {
            if (source.Length < offset + sizeof(int))
            {
                throw new ArgumentOutOfRangeException();
            }

            int byteCount;
            Deserialize(out byteCount, source, ref offset);
            if (source.Length < offset + byteCount)
            {
                throw new ArgumentOutOfRangeException();
            }

            int charCount = Encoding.UTF8.GetCharCount(source, offset, byteCount);
            int sourceOffset = offset;
            int consumedBytes = 0;
            string result = null;

            _charPool.CheckOut((char[] buffer) =>
            {
                var consumedChars = Encoding.UTF8.GetChars(source, sourceOffset, byteCount, buffer, charIndex: 0);
                consumedBytes = Encoding.UTF8.GetByteCount(buffer, index: 0, count: consumedChars);
                result = _stringCache.Find(buffer, index: 0, consumedChars);
            }, charCount);

            value = result;
            offset += consumedBytes;
        }

        /// <summary>
        /// Deserialize a Pose from a source byte[].
        /// </summary>
        public void Deserialize(out Pose pose, byte[] source, ref int offset)
        {
            Deserialize(out pose.position.x, source, ref offset);
            Deserialize(out pose.position.y, source, ref offset);
            Deserialize(out pose.position.z, source, ref offset);
            Deserialize(out pose.rotation.w, source, ref offset);
            Deserialize(out pose.rotation.x, source, ref offset);
            Deserialize(out pose.rotation.y, source, ref offset);
            Deserialize(out pose.rotation.z, source, ref offset);
        }

        /// <summary>
        /// Deserialize a Vector3 from a source byte[].
        /// </summary>
        public void Deserialize(out Vector3 value, byte[] source, ref int offset)
        {
            Deserialize(out value.x, source, ref offset);
            Deserialize(out value.y, source, ref offset);
            Deserialize(out value.z, source, ref offset);
        }

        /// <summary>
        /// Deserialize a Quaternion from a source byte[].
        /// </summary>
        public void Deserialize(out Quaternion value, byte[] source, ref int offset)
        {
            Deserialize(out value.w, source, ref offset);
            Deserialize(out value.x, source, ref offset);
            Deserialize(out value.y, source, ref offset);
            Deserialize(out value.z, source, ref offset);
        }

        /// <summary>
        /// Deserialize a string to a native type.
        /// </summary>
        public bool DeserializeFromString<T>(string value, out object result) where T : unmanaged
        {
            bool success = false;

            try
            {
                var converter = TypeDescriptor.GetConverter(typeof(T));
                result = (T)converter.ConvertFromInvariantString(value);
                success = true;
            }
            catch
            {
                result = default(T);
            }

            return success;
        }
    }

    class ColorConverter : TypeConverter
    {
        public override object ConvertTo(
            ITypeDescriptorContext context,
            CultureInfo culture,
            object value,
            Type destinationType)
        {
            if (value is Color && destinationType == typeof(string))
            {
                return JsonUtility.ToJson(value);
            }
            else
            {
                return base.ConvertTo(context, culture, value, destinationType);
            }
        }

        public override object ConvertFrom(
            ITypeDescriptorContext context,
            CultureInfo culture,
            object value)
        {
            if (value is string)
            {
                return JsonUtility.FromJson<Color>((string)value);
            }
            else
            {
                return base.ConvertFrom(context, culture, value);
            }
        }
    }
}
