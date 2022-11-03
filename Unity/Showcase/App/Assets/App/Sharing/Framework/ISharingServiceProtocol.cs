// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.IO;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    public interface ISharingServiceProtocol
    {
        /// <summary>
        /// Get the data type from the given object.
        /// </summary>
        ProtocolMessageDataType GetDataType(object value);

        /// <summary>
        /// Get the total size of the buffer required to serialize the object
        /// </summary>
        int GetMessageSize(ref ProtocolMessage message);

        /// <summary>
        /// Serialize a message
        /// </summary>
        void SerializeMessage(ref ProtocolMessage message, byte[] target);

        /// <summary>
        /// Serialize a message to the given stream.
        /// </summary>
        int SerializeMessage(ref ProtocolMessage message, Stream stream);

        /// <summary>
        /// Get the number of bytes to serialize a custom type.
        /// </summary>
        int GetMessageDataSize(ref ProtocolMessageData messageData);

        /// <summary>
        /// Serialize a message data
        /// </summary>
        void SerializeMessageData(ref ProtocolMessageData messageData, byte[] target, ref int offset);

        /// <summary>
        /// Serialize a message data to the given stream.
        /// </summary>
        int SerializeMessageData(ref ProtocolMessageData messageData, Stream stream);

        /// <summary>
        /// Serialize an object to the given stream.
        /// </summary>
        int SerializeObject(ProtocolMessageDataType type, object value, Stream stream);

        /// <summary>
        /// Serialize to string using custom serializers
        /// </summary>
        string SerializeToString(object value);

        /// <summary>
        /// Serialize to string using custom serializers
        /// </summary>
        string SerializeToString(ProtocolMessageDataType type, object value);

        /// <summary>
        /// Deserialize a Message from byte array
        /// </summary>
        void DeserializeMessage(out ProtocolMessage message, byte[] source);

        /// <summary>
        /// Deserialize a message from the given stream.
        /// </summary>
        void DeserializeMessage(ref ProtocolMessage message, Stream stream);

        /// <summary>
        /// Deserialize the message data from the byte array
        /// </summary>
        void DeserializeMessageData(out ProtocolMessageData data, byte[] source, ref int offset);

        /// <summary>
        /// Deserialize a message data from the given stream.
        /// </summary>
        void DeserializeMessageData(ref ProtocolMessageData messageData, Stream stream);

        /// <summary>
        /// Deserialize a object from the given stream.
        /// </summary>
        object DeserializeObject(Stream stream);

        /// <summary>
        /// Deserialize a string embedded for the SessionState
        /// </summary>
        bool DeserializeFromString(string value, out object result);

        /// <summary>
        /// Decode an encoded property name.
        /// </summary>
        bool DecodePropertyName(string encoded, out string propertyName);

        /// <summary>
        /// Decode an encoded property name.
        /// </summary>
        bool DecodePropertyName(string encoded, out string participantId, out string propertyName);

        /// <summary>
        /// Encode property name for this object.
        /// </summary>
        string EncodePropertyName(string name);

        /// <summary>
        /// Encode property name for this object.
        /// </summary>
        string EncodePropertyName(string participantId, string name);

        /// <summary>
        /// Wrap data in a protocol message
        /// </summary>
        ProtocolMessage Wrap(ProtocolMessageType type, object data);

        /// <summary>
        /// Unwrap data from a possible protocol message
        /// </summary>
        public object Unwrap(object data);
    }
}