// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// A struct to hold the sharing service message type and data
    /// </summary>
    public struct ProtocolMessage
    {
        /// <summary>
        /// The type of the message
        /// </summary>
        public ProtocolMessageType type;

        /// <summary>
        /// The message data
        /// </summary>
        public ProtocolMessageData data;
    }

    /// <summary>
    /// A struct to hold the sharing service protocol message data and data type.
    /// </summary>
    public struct ProtocolMessageData
    {
        /// <summary>
        /// The type of the message data
        /// </summary>
        public ProtocolMessageDataType type;

        /// <summary>
        /// The data itself
        /// </summary>
        public object value;
    }

    /// <summary>
    /// An enumeration describing the type of message
    /// </summary>
    public enum ProtocolMessageType : byte
    {
        Unknown = 0,

        PropertyChanged = 1,
        Command = 2,

        SharingServiceTransform = 4,
        SharingServiceMessage = 5,
        SharingServiceAnchor = 6,
        SharingServicePingRequest = 7,
        SharingServicePingResponse = 8,
        SharingServiceSpawnParameter = 9,
    }

    /// <summary>
    /// An enumeration describing the type of the message data
    /// </summary>
    public enum ProtocolMessageDataType : byte
    {
        Unknown = 0,

        // Built in data types
        Boolean = 1,
        Short = 2,
        Int = 3,
        Float = 4,
        String = 6,
        Long = 7,

        // object types
        Guid = 10,
        DateTime = 11,
        TimeSpan = 12,
        Color = 13,

        // Special case data
        SharingServiceTransform = 20,

        // Custom data
        SharingServicePlayerPose = 251,
        SharingServicePingRequest = 252,
        SharingServicePingResponse = 253,
        SharingServiceMessage = 254,
        SharingServiceAnchor = 255,
    }
}
