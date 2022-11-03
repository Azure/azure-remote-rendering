// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// A struct holding an array with raw transform data.
    /// </summary>
    [Serializable]
    public struct SharingServiceTransform
    {
        /// <summary>
        /// Create a transform that can hold position, rotation, and scale data.
        /// </summary>
        public static SharingServiceTransform Create()
        {
            return new SharingServiceTransform()
            {
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
                Scale = Vector3.one
            };
        }

        /// <summary>
        /// Create a transform that can hold position and rotation data.
        /// </summary>
        public static SharingServiceTransform Create(ref Vector3 position, ref Quaternion rotation)
        {
            return new SharingServiceTransform()
            {
                Position = position,
                Rotation = rotation,
                Scale = Vector3.one
            };
        }

        /// <summary>
        /// Transform the raw data into a position vector.
        /// </summary>
        public Vector3 Position;

        /// <summary>
        /// Transform the raw data into a rotation
        /// </summary>
        public Quaternion Rotation;

        /// <summary>
        /// Transform the raw data into a scale vector.
        /// </summary>
        public Vector3 Scale;

        /// <summary>
        /// The sharing id that this transform target. This can be null or empty depending on the scenario this structure is used.
        /// </summary>
        public string Target;

        /// <summary>
        /// Convert to a string.
        /// </summary>
        public override string ToString()
        {
            return $"(Position = [{Position.x}, {Position.y}, {Position.z}]) (Rotation = [{Rotation.x}, {Rotation.y}, {Rotation.z}, {Rotation.w}]) (Scale = [{Scale.x}, {Scale.y}, {Scale.z}]) (Target = {Target})";
        }

        /// <summary>
        /// Test if equals
        /// </summary>
        public override bool Equals(object obj)
        {
            if (!(obj is SharingServiceTransform))
            {
                return false;
            }

            SharingServiceTransform other = (SharingServiceTransform)obj;
            return other.Position.x == Position.x &&
                other.Position.y == Position.y &&
                other.Position.z == Position.z &&
                other.Rotation.x == Rotation.x &&
                other.Rotation.y == Rotation.y &&
                other.Rotation.z == Rotation.z &&
                other.Rotation.w == Rotation.w &&
                other.Scale.x == Scale.x &&
                other.Scale.y == Scale.y &&
                other.Scale.z == Scale.z &&
                other.Target == Target;
        }

        /// <summary>
        /// Use the base implementation for now.
        /// </summary>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>
        /// Equals operator
        /// </summary>
        public static bool operator==(SharingServiceTransform v1, SharingServiceTransform v2)
        {
            return v1.Equals(v2);
        }

        /// <summary>
        /// Not Equals operator
        /// </summary>
        public static bool operator !=(SharingServiceTransform v1, SharingServiceTransform v2)
        {
            return !v1.Equals(v2);
        }

        /// <summary>
        /// Copy the raw data to its corresponding structs.
        /// </summary>
        public bool CopyTo(ref Vector3 position, ref Quaternion rotation, ref Vector3 scale)
        {
            position.Set(Position.x, Position.y, Position.z);
            rotation.Set(Rotation.x, Rotation.y, Rotation.z, Rotation.w);
            scale.Set(Scale.x, Scale.y, Scale.z);
            return true;
        }

        /// <summary>
        /// Copy the raw data to its corresponding structs.
        /// </summary>
        public bool CopyTo(ref Vector3 position, ref Quaternion rotation)
        {
            position.Set(Position.x, Position.y, Position.z);
            rotation.Set(Rotation.x, Rotation.y, Rotation.z, Rotation.w);
            return true;
        }

        /// <summary>
        /// Copy a transform's local value to an array of floats.
        /// </summary>
        /// <returns>
        /// True is the raw array was modified.
        /// </returns>
        public bool SetLocal(ref Transform source)
        {
            bool changed = false;

            if (source.localPosition.x != Position.x)
            {
                Position.x = source.localPosition.x;
                changed = true;
            }

            if (source.localPosition.y != Position.y)
            {
                Position.y = source.localPosition.y;
                changed = true;
            }

            if (source.localPosition.z != Position.z)
            {
                Position.z = source.localPosition.z;
                changed = true;
            }

            if (source.localRotation.x != Rotation.x)
            {
                Rotation.x = source.localRotation.x;
                changed = true;
            }

            if (source.localRotation.y != Rotation.y)
            {
                Rotation.y = source.localRotation.y;
                changed = true;
            }

            if (source.localRotation.z != Rotation.z)
            {
                Rotation.z = source.localRotation.z;
                changed = true;
            }

            if (source.localRotation.w != Rotation.w)
            {
                Rotation.w = source.localRotation.w;
                changed = true;
            }

            if (source.localScale.x != Scale.x)
            {
                Scale.x = source.localScale.x;
                changed = true;
            }

            if (source.localScale.y != Scale.y)
            {
                Scale.y = source.localScale.y;
                changed = true;
            }

            if (source.localScale.z != Scale.z)
            {
                Scale.z = source.localScale.z;
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Copy a transform object's local scale to an array of floats.
        /// </summary>
        /// <returns>
        /// True is the raw array was modified.
        /// </returns>
        public bool SetScale(ref Transform source)
        {
            bool changed = false;

            if (source.localScale.x != Scale.x)
            {
                Scale.x = source.localScale.x;
                changed = true;
            }

            if (source.localScale.y != Scale.y)
            {
                Scale.y = source.localScale.y;
                changed = true;
            }

            if (source.localScale.z != Scale.z)
            {
                Scale.z = source.localScale.z;
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Copy the given position and rotation to this object.
        /// </summary>
        /// <returns>
        /// True is the raw array was modified.
        /// </returns>
        public bool Set(ref Vector3 position, ref Quaternion rotation)
        {
            bool changed = false;

            if (position.x != Position.x)
            {
                Position.x = position.x;
                changed = true;
            }

            if (position.y != Position.y)
            {
                Position.y = position.y;
                changed = true;
            }

            if (position.z != Position.z)
            {
                Position.z = position.z;
                changed = true;
            }

            if (rotation.x != Rotation.x)
            {
                Rotation.x = rotation.x;
                changed = true;
            }

            if (rotation.y != Rotation.y)
            {
                Rotation.y = rotation.y;
                changed = true;
            }

            if (rotation.z != Rotation.z)
            {
                Rotation.z = rotation.z;
                changed = true;
            }

            if (rotation.w != Rotation.w)
            {
                Rotation.w = rotation.w;
                changed = true;
            }

            return changed;
        }


        /// <summary>
        /// Copy a transform object to an array of floats.
        /// </summary>
        /// <returns>
        /// True is the raw array was modified.
        /// </returns>
        public bool Set(ref Vector3 position, ref Quaternion rotation, ref Vector3 scale)
        {
            bool changed = false;

            changed |= Set(ref position, ref rotation);

            if (scale.x != Scale.x)
            {
                Scale.x = scale.x;
                changed = true;
            }

            if (scale.y != Scale.y)
            {
                Scale.y = scale.y;
                changed = true;
            }

            if (scale.z != Scale.z)
            {
                Scale.z = scale.z;
                changed = true;
            }

            return changed;
        }
    }

    public class SharingServiceTransformSerializer : ISharingServiceSerializer
    {
        ISharingServiceBasicSerializer _serializer;

        public SharingServiceTransformSerializer(ISharingServiceBasicSerializer byteSerializer)
        {
            _serializer = byteSerializer;
        }

        public int GetByteSize(object value)
        {
            if (!(value is SharingServiceTransform))
            {
                return 0;
            }

            // string
            SharingServiceTransform transform = (SharingServiceTransform)value;

            // vector3 + quaternion + vector3
            int bytes = (3 + 4 + 3) * sizeof(float);

            // target is a string which is variable
            bytes += _serializer.GetByteSize(transform.Target);

            return bytes;
        }

        public void Serialize(object value, byte[] target, ref int offset)
        {
            if (!(value is SharingServiceTransform))
            {
                throw new InvalidCastException();
            }

            short sizeOfObject = (short)GetByteSize(value);

            // make sure there is enough room for the amount of data that is required for object
            if (target.Length < sizeOfObject)
            {
                throw new ArgumentOutOfRangeException();
            }

            SharingServiceTransform transform = (SharingServiceTransform)value;
            _serializer.Serialize(transform.Target, target, ref offset);
            _serializer.Serialize(transform.Position.x, target, ref offset);
            _serializer.Serialize(transform.Position.y, target, ref offset);
            _serializer.Serialize(transform.Position.z, target, ref offset);
            _serializer.Serialize(transform.Rotation.w, target, ref offset);
            _serializer.Serialize(transform.Rotation.x, target, ref offset);
            _serializer.Serialize(transform.Rotation.y, target, ref offset);
            _serializer.Serialize(transform.Rotation.z, target, ref offset);
            _serializer.Serialize(transform.Scale.x, target, ref offset);
            _serializer.Serialize(transform.Scale.y, target, ref offset);
            _serializer.Serialize(transform.Scale.z, target, ref offset);
        }

        public void Deserialize(out object value, byte[] source, ref int offset)
        {
            // grab the object name and determine if the size is correct
            SharingServiceTransform transform = SharingServiceTransform.Create();
            _serializer.Deserialize(out transform.Target, source, ref offset);
            _serializer.Deserialize(out transform.Position.x, source, ref offset);
            _serializer.Deserialize(out transform.Position.y, source, ref offset);
            _serializer.Deserialize(out transform.Position.z, source, ref offset);
            _serializer.Deserialize(out transform.Rotation.w, source, ref offset);
            _serializer.Deserialize(out transform.Rotation.x, source, ref offset);
            _serializer.Deserialize(out transform.Rotation.y, source, ref offset);
            _serializer.Deserialize(out transform.Rotation.z, source, ref offset);
            _serializer.Deserialize(out transform.Scale.x, source, ref offset);
            _serializer.Deserialize(out transform.Scale.y, source, ref offset);
            _serializer.Deserialize(out transform.Scale.z, source, ref offset);

            value = transform;
        }

        /// <summary>
        /// Convert object to string
        /// </summary>
        public string ToString(object value)
        {
            if (!(value is SharingServiceTransform))
            {
                return null;
            }

            return JsonUtility.ToJson((SharingServiceTransform)value);
        }

        /// <summary>
        /// Convert string to object
        /// </summary>
        public bool FromString(string value, out object result)
        {
            return SharingServiceJsonHelper.DeserializeFromJson<SharingServiceTransform>(value, out result);
        }
    }

}
