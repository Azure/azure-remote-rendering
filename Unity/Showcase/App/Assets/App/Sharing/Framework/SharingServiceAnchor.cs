// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// A struct holding an Azure Spatial Anchor id and a fallback pose.
    /// </summary>
    [Serializable]
    public struct SharingServiceAnchor
    {
        /// <summary>
        /// Create a sharing anchor that can hold an anchor id, fallback position, and fallback rotation
        /// </summary>
        public static SharingServiceAnchor Create()
        {
            return new SharingServiceAnchor()
            {
                Fallback = new Pose(Vector3.zero, Quaternion.identity),
                AnchorId = string.Empty
            };
        }

        /// <summary>
        /// Create a sharing anchor that can hold an anchor id, with a given fallback position and fallback rotation
        /// </summary>
        public static SharingServiceAnchor Create(ref Vector3 position, ref Quaternion rotation)
        {
            return new SharingServiceAnchor()
            {
                Fallback = new Pose(position, rotation),
                AnchorId = string.Empty
            };
        }

        /// <summary>
        /// Create a sharing anchor with a given anchor id, fallback position and fallback rotation
        /// </summary>
        public static SharingServiceAnchor Create(string anchorId, ref Vector3 position, ref Quaternion rotation)
        {
            return new SharingServiceAnchor()
            {
                Fallback = new Pose(position, rotation),
                AnchorId = anchorId
            };
        }

        /// <summary>
        /// The Azure Anchor id to search for
        /// </summary>
        public string AnchorId;

        /// <summary>
        /// The fallback pose to use if Azure Anchor is not located.
        /// </summary>
        public Pose Fallback;

        /// <summary>
        /// Convert to a string.
        /// </summary>
        public override string ToString()
        {
            return $"(Anchor Id = {AnchorId}) (Position = [{Fallback.position.x}, {Fallback.position.y}, {Fallback.position.z}]) (Rotation = [{Fallback.rotation.x}, {Fallback.rotation.y}, {Fallback.rotation.z}, {Fallback.rotation.w}])";
        }

        /// <summary>
        /// Test if equals
        /// </summary>
        public override bool Equals(object obj)
        {
            if (!(obj is SharingServiceAnchor))
            {
                return false;
            }

            SharingServiceAnchor other = (SharingServiceAnchor)obj;
            return other.AnchorId == AnchorId &&
                other.Fallback.position.x == Fallback.position.x &&
                other.Fallback.position.y == Fallback.position.y &&
                other.Fallback.position.z == Fallback.position.z &&
                other.Fallback.rotation.x == Fallback.rotation.x &&
                other.Fallback.rotation.y == Fallback.rotation.y &&
                other.Fallback.rotation.z == Fallback.rotation.z &&
                other.Fallback.rotation.w == Fallback.rotation.w;
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
        public static bool operator ==(SharingServiceAnchor v1, SharingServiceAnchor v2)
        {
            return v1.Equals(v2);
        }

        /// <summary>
        /// Not Equals operator
        /// </summary>
        public static bool operator !=(SharingServiceAnchor v1, SharingServiceAnchor v2)
        {
            return !v1.Equals(v2);
        }

        /// <summary>
        /// Copy the raw fallback data to its corresponding structs.
        /// </summary>
        public bool CopyTo(ref Vector3 position, ref Quaternion rotation)
        {
            position.Set(Fallback.position.x, Fallback.position.y, Fallback.position.z);
            rotation.Set(Fallback.rotation.x, Fallback.rotation.y, Fallback.rotation.z, Fallback.rotation.w);
            return true;
        }

        /// <summary>
        /// Copy a transform object to the inner fallback data
        /// </summary>
        /// <returns>
        /// True if the inner data was modified.
        /// </returns>
        public bool Set(Transform source)
        {
            bool changed = false;

            if (source.localPosition.x != Fallback.position.x)
            {
                Fallback.position.x = source.localPosition.x;
                changed = true;
            }

            if (source.localPosition.y != Fallback.position.y)
            {
                Fallback.position.y = source.localPosition.y;
                changed = true;
            }

            if (source.localPosition.z != Fallback.position.z)
            {
                Fallback.position.z = source.localPosition.z;
                changed = true;
            }

            if (source.localRotation.x != Fallback.rotation.x)
            {
                Fallback.rotation.x = source.localRotation.x;
                changed = true;
            }

            if (source.localRotation.y != Fallback.rotation.y)
            {
                Fallback.rotation.y = source.localRotation.y;
                changed = true;
            }

            if (source.localRotation.z != Fallback.rotation.z)
            {
                Fallback.rotation.z = source.localRotation.z;
                changed = true;
            }

            if (source.localRotation.w != Fallback.rotation.w)
            {
                Fallback.rotation.w = source.localRotation.w;
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Copy the given position and rotation to this object.
        /// </summary>
        /// <returns>
        /// True if the inner data was modified.
        /// </returns>
        public bool Set(ref Vector3 position, ref Quaternion rotation)
        {
            bool changed = false;

            if (position.x != Fallback.position.x)
            {
                Fallback.position.x = position.x;
                changed = true;
            }

            if (position.y != Fallback.position.y)
            {
                Fallback.position.y = position.y;
                changed = true;
            }

            if (position.z != Fallback.position.z)
            {
                Fallback.position.z = position.z;
                changed = true;
            }

            if (rotation.x != Fallback.rotation.x)
            {
                Fallback.rotation.x = rotation.x;
                changed = true;
            }

            if (rotation.y != Fallback.rotation.y)
            {
                Fallback.rotation.y = rotation.y;
                changed = true;
            }

            if (rotation.z != Fallback.rotation.z)
            {
                Fallback.rotation.z = rotation.z;
                changed = true;
            }

            if (rotation.w != Fallback.rotation.w)
            {
                Fallback.rotation.w = rotation.w;
                changed = true;
            }

            return changed;
        }
    }

    public class SharingServiceAnchorSerializer : ISharingServiceSerializer
    {
        ISharingServiceBasicSerializer _serializer;

        public SharingServiceAnchorSerializer(ISharingServiceBasicSerializer byteSerializer)
        {
            _serializer = byteSerializer;
        }

        /// <summary>
        /// Get the number of bytes needed to encode the given value.
        /// </summary>
        public int GetByteSize(object value)
        {
            if (!(value is SharingServiceAnchor))
            {
                return 0;
            }

            SharingServiceAnchor sharingAnchor = (SharingServiceAnchor)value;

            int bytes = 0;
            bytes += _serializer.GetByteSize(sharingAnchor.AnchorId);
            bytes += _serializer.GetByteSize(ref sharingAnchor.Fallback);
            return bytes;
        }

        /// <summary>
        /// Serialize the given value to the byte array
        /// </summary>
        public void Serialize(object value, byte[] target, ref int offset)
        {
            if (!(value is SharingServiceAnchor))
            {
                return;
            }

            SharingServiceAnchor sharingAnchor = (SharingServiceAnchor)value;
            _serializer.Serialize(sharingAnchor.AnchorId, target, ref offset);
            _serializer.Serialize(ref sharingAnchor.Fallback, target, ref offset);
        }

        /// <summary>
        /// Deserialize the given value from a byte array
        /// </summary>
        public void Deserialize(out object value, byte[] source, ref int offset)
        {
            value = new SharingServiceAnchor();
            SharingServiceAnchor sharingAnchor = (SharingServiceAnchor)value;
            _serializer.Deserialize(out sharingAnchor.AnchorId, source, ref offset);
            _serializer.Deserialize(out sharingAnchor.Fallback, source, ref offset);
        }

        /// <summary>
        /// Convert object to string
        /// </summary>
        public string ToString(object value)
        {
            if (!(value is SharingServiceAnchor))
            {
                return null;
            }

            return JsonUtility.ToJson((SharingServiceAnchor)value);
        }

        /// <summary>
        /// Convert string to object
        /// </summary>
        public bool FromString(string value, out object result)
        {
            return SharingServiceJsonHelper.DeserializeFromJson<SharingServiceAnchor>(value, out result);
        }
    }
}
