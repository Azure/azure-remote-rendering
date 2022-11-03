// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using DataType = Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.ProtocolMessageDataType;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    public class SharingServiceProtocol : ISharingServiceProtocol
    {
        private SharingServiceBasicSerializer _basicSerializer = new SharingServiceBasicSerializer();
        private readonly Dictionary<DataType, ISharingServiceSerializer> _serializers;
        private readonly Dictionary<Type, DataType> _dataTypes;
        private const char _encodedPropertySeparator = ':';
        private const string _encodedPropertyNameFormat = "{0}:{1}";
        private const string _encodedPropertyStringFormat = "{0}:{1}";
        private StringCache _decodePropertyNameCache = new StringCache();
        private StringCache _dataTypeCache = new StringCache();
        private Dictionary<EncodePropertyNameKey, string> _encodedPropertyNames = new Dictionary<EncodePropertyNameKey, string>();
        private BinaryFormatter _binaryFormatter = null;
        private BufferPool<byte> _bufferPool =  null;

        public SharingServiceProtocol()
        {
            _serializers = new Dictionary<DataType, ISharingServiceSerializer>()
            {
                { DataType.Boolean, new SharingServiceTypeSerializer<bool>(_basicSerializer) },
                { DataType.Float, new SharingServiceTypeSerializer<float>(_basicSerializer) },
                { DataType.Int, new SharingServiceTypeSerializer<int>(_basicSerializer) },
                { DataType.Long, new SharingServiceTypeSerializer<long>(_basicSerializer) },
                { DataType.Guid, new SharingServiceTypeSerializer<Guid>(_basicSerializer) },
                { DataType.DateTime, new SharingServiceTypeSerializer<DateTime>(_basicSerializer) },
                { DataType.TimeSpan, new SharingServiceTypeSerializer<TimeSpan>(_basicSerializer) },
                { DataType.String, new SharingServiceStringSerializer(_basicSerializer) },
                { DataType.Color, new SharingServiceTypeSerializer<Color>(_basicSerializer) },
                { DataType.SharingServiceTransform, new SharingServiceTransformSerializer(_basicSerializer) },
                { DataType.SharingServiceMessage, new SharingServiceMessageSerializer(_basicSerializer) },
                { DataType.SharingServiceAnchor, new SharingServiceAnchorSerializer(_basicSerializer) },
                { DataType.SharingServicePingRequest, new SharingServicePingRequestSerializer() },
                { DataType.SharingServicePingResponse, new SharingServicePingResponseSerializer() },
                { DataType.SharingServicePlayerPose, new AvatarPoseSerializer(_basicSerializer) },
            };

            _dataTypes = new Dictionary<Type, ProtocolMessageDataType>()
            {
                { typeof(bool), DataType.Boolean },
                { typeof(short), DataType.Short },
                { typeof(int), DataType.Int },
                { typeof(long), DataType.Long },
                { typeof(float), DataType.Float },
                { typeof(string), DataType.String },
                { typeof(Guid), DataType.Guid },
                { typeof(DateTime), DataType.DateTime },
                { typeof(TimeSpan), DataType.TimeSpan },
                { typeof(Color), DataType.Color },
                { typeof(SharingServiceMessage), DataType.SharingServiceMessage },
                { typeof(SharingServiceTransform), DataType.SharingServiceTransform },
                { typeof(SharingServiceAnchor), DataType.SharingServiceAnchor },
                { typeof(SharingServicePingRequest), DataType.SharingServicePingRequest },
                { typeof(SharingServicePingResponse), DataType.SharingServicePingResponse },
                { typeof(AvatarPose), DataType.SharingServicePlayerPose },
            };
        }        

        /// <summary>
        /// A pool of byte buffers.
        /// </summary>
        private BufferPool<byte> BufferPool
        {
            get
            {
                if (_bufferPool == null)
                {
                    _bufferPool = new BufferPool<byte>();
                }

                return _bufferPool;
            }
        }

        /// <summary>
        /// A binary formatter used to serialize generic data types.
        /// </summary>
        private BinaryFormatter BinaryFormatter
        {
            get
            {
                if (_binaryFormatter == null)
                {
                    _binaryFormatter = new BinaryFormatter();
                }

                return _binaryFormatter;
            }
        }

        /// <summary>
        /// Get the data type from the given object.
        /// </summary>
        public DataType GetDataType(object value)
        {
            if (value == null)
            {
                return DataType.Unknown;
            }

            var type = value.GetType();
            return _dataTypes.ContainsKey(type) ? _dataTypes[type] : DataType.Unknown;
        }

        /// <summary>
        /// Get the total size of the buffer required to serialize the object
        /// </summary>
        public int GetMessageSize(ref ProtocolMessage message)
        {
            int bytes = 1; // message type
            bytes += GetMessageDataSize(ref message.data);
            return bytes;
        }

        /// <summary>
        /// Serialize a message
        /// </summary>
        public void SerializeMessage(ref ProtocolMessage message, byte[] target)
        {
            int offset = 0;
            target[offset++] = (byte)message.type;
            SerializeMessageData(ref message.data, target, ref offset);
        }

        /// <summary>
        /// Get the number of bytes to serialize a custom type.
        /// </summary>
        public int GetMessageDataSize(ref ProtocolMessageData messageData)
        {
            return GetObjectSize(messageData.type, messageData.value);
        }

        /// <summary>
        /// Get the number of bytes to serialize a custom type.
        /// </summary>
        public int GetObjectSize(DataType type, object value)
        {
            int bytes = 1; // message data type;
            bytes += _serializers[type].GetByteSize(value);
            return bytes;
        }

        /// <summary>
        /// Serialize a message data
        /// </summary>
        public void SerializeMessageData(ref ProtocolMessageData messageData, byte[] target, ref int offset)
        {
            SerializeObject(messageData.type, messageData.value, target, ref offset);
        }

        /// <summary>
        /// Serialize an object.
        /// </summary>
        public void SerializeObject(DataType type, object value, byte[] target, ref int offset)
        {
            target[offset++] = (byte)type;
            _serializers[type].Serialize(value, target, ref offset);
        }

        /// <summary>
        /// Serialize a message to the given stream.
        /// </summary>
        public int SerializeMessage(ref ProtocolMessage message, Stream stream)
        {
            int wrote = Serialize((int)message.type, stream);
            wrote += SerializeMessageData(ref message.data, stream);
            return wrote;
        }

        /// <summary>
        /// Serialize a message data to the given stream.
        /// </summary>
        public int SerializeMessageData(ref ProtocolMessageData messageData, Stream stream)
        {
            return SerializeObject(messageData.type, messageData.value, stream);
        }

        /// <summary>
        /// Serialize an object to the given stream.
        /// </summary>
        public int SerializeObject(DataType type, object value, Stream stream)
        {
            int wrote = Serialize((int)type, stream);
            if (type == DataType.Unknown)
            {
                wrote += SerializeObjectForGenericDataType(ref value, stream);
            }
            else
            {
                wrote += SerializeObjectForKnownDataType(type, ref value, stream);
            }
            return wrote;
        }

        /// <summary>
        /// Serialize to string using custom serializers
        /// </summary>
        public string SerializeToString(object value)
        {
            return SerializeToString(GetDataType(value), value);
        }

        /// <summary>
        /// Serialize to string using custom serializers
        /// </summary>
        public string SerializeToString(DataType type, object value)
        {
            if (value == null)
            {
                return null;
            }
            else
            {
                return string.Format(_encodedPropertyStringFormat, (int)type, _serializers[type].ToString(value));
            }
        }       

        /// <summary>
        /// Deserialize a Message from byte array
        /// </summary>
        public void DeserializeMessage(out ProtocolMessage message, byte[] source)
        {
            int offset = 0;
            message.type = (ProtocolMessageType)source[offset++];
            DeserializeMessageData(out message.data, source, ref offset);
        }

        /// <summary>
        /// Deserialize the message data from the byte array
        /// </summary>
        public void DeserializeMessageData(out ProtocolMessageData data, byte[] source, ref int offset)
        {
            data = new ProtocolMessageData();
            data.value = DeserializeObject(out data.type, source, ref offset);
        }

        /// <summary>
        /// Deserialize the message data from the byte array
        /// </summary>
        public object DeserializeObject(byte[] source, ref int offset)
        {
            DataType type;
            return DeserializeObject(out type, source, ref offset);
        }

        /// <summary>
        /// Deserialize the message data from the byte array
        /// </summary>
        public object DeserializeObject(out DataType type, byte[] source, ref int offset)
        {
            object result;
            type = (DataType)source[offset++];
            _serializers[type].Deserialize(out result, source, ref offset);
            return result;
        }

        /// <summary>
        /// Deserialize a message from the given stream.
        /// </summary>
        public void DeserializeMessage(ref ProtocolMessage message, Stream stream)
        {
            int typeInt;
            Deserialize(out typeInt, stream);
            message.type = (ProtocolMessageType)typeInt;
            DeserializeMessageData(ref message.data, stream);
        }

        /// <summary>
        /// Deserialize a message data from the given stream.
        /// </summary>
        public void DeserializeMessageData(ref ProtocolMessageData messageData, Stream stream)
        {
            messageData.value = DeserializeObject(out messageData.type, stream);
        }

        /// <summary>
        /// Deserialize a object from the given stream.
        /// </summary>
        public object DeserializeObject(Stream stream)
        {
            DataType type;
            return DeserializeObject(out type, stream);
        }

        /// <summary>
        /// Deserialize a message data from the given stream.
        /// </summary>
        public object DeserializeObject(out DataType type, Stream stream)
        {
            int typeInt;
            Deserialize(out typeInt, stream);

            type = (DataType)typeInt;
            if (type == DataType.Unknown)
            {
                return DeserializeObjectForGenericDataType(stream);
            }
            else
            {
                return DeserializeObjectForKnownDataType(type, stream);
            }
        }

        /// <summary>
        /// Deserialize a string embedded for the SessionState
        /// </summary>
        public bool DeserializeFromString(string value, out object result)
        {
            result = null;
            string typePart = null;
            string valuePart = null;

            if (string.IsNullOrEmpty(value))
            {
                return true;
            }
            
            int startIndex = 0;
            int separatorIndex = value.IndexOf(_encodedPropertySeparator);
            if (separatorIndex < 0 || value.Length < 3)
            {
                return false;
            }

            if (startIndex < separatorIndex)
            {
                typePart = _dataTypeCache.Find(value, startIndex, separatorIndex - startIndex);
            }

            startIndex = separatorIndex + 1;
            separatorIndex = value.Length;
            if (startIndex < separatorIndex)
            {
                // value parts probably change often, so avoid using a cache
                valuePart = value.Substring(startIndex, separatorIndex - startIndex);
            }

            int typeInt;
            DataType dataType = DataType.Unknown;
            if (int.TryParse(typePart, out typeInt))
            {
                dataType = (DataType)typeInt;
            }

            if (_serializers.ContainsKey(dataType))
            {
                return _serializers[dataType].FromString(valuePart, out result);
            }
            else
            {
                result = null;
                Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "Trying to deserialize a data string of an unknown type. ({0}) ({1})", dataType, value);
                return false;
            }
        }

        /// <summary>
        /// Decode an encoded property name.
        /// </summary>
        public bool DecodePropertyName(string encoded, out string propertyName)
        {
            string participantId;
            return DecodePropertyName(encoded, out participantId, out propertyName);
        }

        /// <summary>
        /// Decode an encoded property name.
        /// </summary>
        public bool DecodePropertyName(string encoded, out string participantId, out string propertyName)
        {
            participantId = null;
            propertyName = null;

            int startIndex = 0;
            int separatorIndex = encoded.IndexOf(_encodedPropertySeparator);
            if (separatorIndex < 0 || encoded.Length < 3)
            {
                return false;
            }

            if (startIndex < separatorIndex)
            {
                participantId = _decodePropertyNameCache.Find(encoded, startIndex, separatorIndex - startIndex);
            }

            startIndex = separatorIndex + 1;
            separatorIndex = encoded.Length;
            if (startIndex < separatorIndex)
            {
                propertyName = _decodePropertyNameCache.Find(encoded, startIndex, separatorIndex - startIndex);
            }

            return propertyName != null;
        }

        /// <summary>
        /// Encode property name for this object.
        /// </summary>
        public string EncodePropertyName(string name)
        {
            return EncodePropertyName(participantId: string.Empty, name);
        }

        /// <summary>
        /// Encode property name for this object.
        /// </summary>
        public string EncodePropertyName(string participantId, string name)
        {
            if (participantId == null)
            {
                participantId = string.Empty;
            }

            string result;
            EncodePropertyNameKey key = new EncodePropertyNameKey(participantId, name);
            if (!_encodedPropertyNames.TryGetValue(key, out result))
            {
                result = string.Format(_encodedPropertyNameFormat, key.ParticipantId, key.PropertyName);
                _encodedPropertyNames[key] = result;
            }
            return result;
        }

        /// <summary>
        /// Wrap data in a protocol message
        /// </summary>
        public ProtocolMessage Wrap(ProtocolMessageType type, object data)
        {
            if (data is ProtocolMessage)
            {
                return (ProtocolMessage)data;
            }
            else
            {
                return new ProtocolMessage()
                {
                    type = type,
                    data = new ProtocolMessageData()
                    {
                        type = GetDataType(data),
                        value = data
                    }
                };
            }
        }

        /// <summary>
        /// Unwrap data from a possible protocol message
        /// </summary>
        public object Unwrap(object data)
        {
            if (data is ProtocolMessage)
            {
                return ((ProtocolMessage)data).data.value;
            }
            else
            {
                return data;
            }
        }

        /// <summary>
        /// Serialize a message data to the given stream, for a known data type.
        /// </summary>
        private int SerializeObjectForKnownDataType(DataType type, ref object value, Stream stream)
        {
            int byteSize = GetObjectSize(type, value);
            if (byteSize > short.MaxValue)
            {
                byteSize = 0;
            }
            else
            {
                using (BufferPool<byte>.Pinned pinned = BufferPool.LongTerm(byteSize))
                {
                    int offset = 0;
                    _serializers[type].Serialize(value, pinned.Buffer, ref offset);
                    stream.Write(pinned.Buffer, 0, byteSize);
                }
            }

            return (short)byteSize;
        }

        /// <summary>
        /// Serialize a message data to the given stream, for an unknown data type.
        /// </summary>
        private int SerializeObjectForGenericDataType(ref object value, Stream stream)
        {
            var binnaryFormatter = BinaryFormatter;
            var start = stream.Position;
            binnaryFormatter.Serialize(stream, value);
            var end = stream.Position;

            var length = end - start;
            if (length > Int32.MaxValue)
            {
                throw new ArgumentOutOfRangeException();
            }

            return (int)length;
        }

        /// <summary>
        /// Deserialize message data to the given stream, for a known data type.
        /// </summary>
        private object DeserializeObjectForKnownDataType(DataType type, Stream stream)
        {
            if (stream.Length > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException();
            }

            object result;
            int maxLength = (int)stream.Length;
            using (BufferPool<byte>.Pinned pinned = BufferPool.LongTerm(maxLength))
            {
                int offset = 0;
                stream.Read(pinned.Buffer, 0, maxLength);
                _serializers[type].Deserialize(out result, pinned.Buffer, ref offset);
            }
            return result;
        }

        /// <summary>
        /// Deserialize message data from the given stream, for an unknown data type.
        /// </summary>
        private object DeserializeObjectForGenericDataType(Stream stream)
        {
            return BinaryFormatter.Deserialize(stream);
        }

        /// <summary>
        /// Serialize a basic unmanaged type
        /// </summary>
        private int Serialize<T>(T value, Stream stream) where T : unmanaged
        {
            int size = _basicSerializer.GetByteSize<T>();
            BufferPool.CheckOut((args) =>
            {
                int offset = 0;
                _basicSerializer.Serialize(value, args.Buffer, ref offset);
                stream.Write(args.Buffer, 0, size);
            }, size);
            return size;
        }

        /// <summary>
        /// Serialize a basic unmanaged type
        /// </summary>
        private void Deserialize(out int value, Stream stream)
        {
            int size = _basicSerializer.GetByteSize<int>();

            using (BufferPool<byte>.Pinned pinned = BufferPool.LongTerm(size))
            {
                int offset = 0;
                stream.Read(pinned.Buffer, 0, size);
                _basicSerializer.Deserialize(out value, pinned.Buffer, ref offset);
            }
        }

        private struct EncodePropertyNameKey : IEquatable<EncodePropertyNameKey>
        {
            public string ParticipantId;
            public string PropertyName;

            public EncodePropertyNameKey(string participantId, string propertyName) => 
                (ParticipantId, PropertyName) = (participantId ?? string.Empty, propertyName ?? string.Empty);

            public override bool Equals(object obj) => 
                obj is EncodePropertyNameKey value && Equals(value);

            public bool Equals(EncodePropertyNameKey other) =>
                ParticipantId == other.ParticipantId &&
                PropertyName == other.PropertyName;

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 23 + ParticipantId.GetHashCode();
                    hash = hash * 23 + PropertyName.GetHashCode();
                    return hash;
                }
            }

        }

    }
}
