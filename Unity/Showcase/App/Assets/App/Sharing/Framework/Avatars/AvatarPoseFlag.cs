// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    [Flags]
    public enum AvatarPoseFlag
    {
        None = 0x00000000,

        Hand = 0x00000001,

        ThumbTip = 0x00000004,
        IndexTip = 0x00000010,
        MiddleTip = 0x00000020,
        RingTip = 0x00000040,
        PinkyTip = 0x00000080,

        ThumbDistal = 0x00000100,
        IndexDistal = 0x00000200,
        MiddleDistal = 0x00000400,
        RingDistal = 0x00000800,
        PinkyDistal = 0x00001000,

        ThumbProximal = 0x00002000,
        IndexMiddle = 0x00004000,
        MiddleMiddle = 0x00008000,
        RingMiddle = 0x00010000,
        PinkyMiddle = 0x00020000,

        IndexKnuckle = 0x00040000,
        MiddleKnuckle = 0x00080000,
        RingKnuckle = 0x00100000,
        PinkyKnuckle = 0x00200000,

        // Max
        Max = PinkyKnuckle,
    }

    public static class AvatarPoseFlagHelper
    {
        public static IEnumerable<AvatarPoseFlag> GetEnumerable()
        {
            int value = 0x1;
            while (value <= (int)AvatarPoseFlag.Max)
            {
                yield return (AvatarPoseFlag)value;
                value <<= 1;
            }
        }
    }
}
