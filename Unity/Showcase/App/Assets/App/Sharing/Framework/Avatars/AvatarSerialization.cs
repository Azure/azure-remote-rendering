// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// Describes the type of joint serialization to use for this avatar
    /// </summary>
    public class AvatarSerialization : MonoBehaviour
    {
        private AvatarHandDescription _handDescription = null;

        [SerializeField]
        [Tooltip("Get or set the type of serialization to use for avatar fingers.")]
        private FingerSerializationType fingerSerializationType = FingerSerializationType.Unknown;

        /// <summary>
        /// Get or set if finger tips should be serialized
        /// </summary>
        public FingerSerializationType FingerSerializationType
        {
            get => fingerSerializationType;
            set
            {
                fingerSerializationType = value;
                HandDescription.FingerSerializationType = value;
            }
        }

        /// <summary>
        /// Get the hand description associated with this serialization.
        /// </summary>
        public AvatarHandDescription HandDescription
        { 
            get
            {
                if (_handDescription == null)
                {
                    _handDescription = new AvatarHandDescription();
                    _handDescription.FingerSerializationType = fingerSerializationType;
                }
                return _handDescription;
            }
        }

        /// <summary>
        /// Initialize hand description.
        /// </summary>
        private void Start()
        {
            HandDescription.FingerSerializationType = fingerSerializationType;
        }

        /// <summary>
        /// Get the AvatarHandDescription off of the given target game object.
        /// </summary>
        public static AvatarJointDescription[] ExtractSerializableJoints(GameObject target)
        {
            AvatarJointDescription[] result;
            if (target != null &&
                target.TryGetComponent(out AvatarSerialization avatarSerialization))
            {
                result = avatarSerialization.HandDescription.SerializableJoints;
            }
            else
            {
                result = new AvatarJointDescription[0];
            }
            return result;
        }
    }
}
