// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// Message to be sent over the sharing service
    /// </summary>
    public struct SharingServiceMessage : ISharingServiceMessage
    {
        /// <summary>
        /// The target object to send the message to. This maybe null, in which cases this is an global message.
        /// </summary>
        public string Target { get; set; }

        /// <summary>
        /// Command to send
        /// </summary>
        public string Command { get; set; }

        /// <summary>
        /// The message sender
        /// </summary>
        public string Sender { get; set; }
    }

    public class SharingServiceMessageSerializer : ISharingServiceSerializer
    {
        ISharingServiceBasicSerializer _serializer;

        public SharingServiceMessageSerializer(ISharingServiceBasicSerializer byteSerializer)
        {
            _serializer = byteSerializer;
        }

        /// <summary>
        /// Get the number of bytes needed to encode the given value.
        /// </summary>
        public int GetByteSize(object value)
        {
            if (!(value is SharingServiceMessage))
            {
                return 0;
            }

            SharingServiceMessage sharingServiceMessage = (SharingServiceMessage)value;

            int bytes = 0;
            bytes += _serializer.GetByteSize(sharingServiceMessage.Command);
            bytes += _serializer.GetByteSize(sharingServiceMessage.Target);
            bytes += _serializer.GetByteSize(sharingServiceMessage.Sender);
            return bytes;
        }

        /// <summary>
        /// Serialize the given value to the byte array
        /// </summary>
        public void Serialize(object value, byte[] target, ref int offset)
        {
            if (!(value is SharingServiceMessage))
            {
                throw new InvalidCastException();
            }

            SharingServiceMessage sharingServiceMessage = (SharingServiceMessage)value;
            _serializer.Serialize(sharingServiceMessage.Command, target, ref offset);
            _serializer.Serialize(sharingServiceMessage.Target, target, ref offset);
            _serializer.Serialize(sharingServiceMessage.Sender, target, ref offset);
        }

        /// <summary>
        /// Deserialize the given value from a byte array
        /// </summary>
        public void Deserialize(out object value, byte[] source, ref int offset)
        {
            string command;
            string target;
            string sender;
            _serializer.Deserialize(out command, source, ref offset);
            _serializer.Deserialize(out target, source, ref offset);
            _serializer.Deserialize(out sender, source, ref offset);
            value = new SharingServiceMessage()
            {
                Command = command,
                Target = target,
                Sender = sender
            };
        }

        /// <summary>
        /// Convert object to string
        /// </summary>
        public string ToString(object value)
        {
            // should never be serializing these messages to strings.
            throw new NotImplementedException();
        }

        /// <summary>
        /// Convert string to object
        /// </summary>
        public bool FromString(string value, out object result)
        {
            // should never be serializing these messages to strings.
            throw new NotImplementedException();
        }
    }

}
