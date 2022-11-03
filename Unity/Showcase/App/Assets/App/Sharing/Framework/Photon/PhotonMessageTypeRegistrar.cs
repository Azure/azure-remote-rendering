// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if PHOTON_INSTALLED
using ExitGames.Client.Photon;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    public class PhotonMessageTypeRegistrar
    {
        private static byte _protocolPlayerPoseDataType = 198;
        private static byte _protocolMessageDataType = 199;
        private LogHelper<PhotonMessages> _logger = new LogHelper<PhotonMessages>();

        #region Constructor
        public PhotonMessageTypeRegistrar(SharingServiceProfile settings, ISharingServiceProtocol protocol)
        {
            _logger.Verbose = settings.VerboseLogging ? LogHelperState.Always : LogHelperState.Default;
            Protocol = protocol ?? throw new ArgumentNullException("Protocol can't be null");
            RegisterAppMessage();
        }
        #endregion Constructor

        #region Public Properties
        /// <summary>
        /// Get the protocol used by the registrar
        /// </summary>
        public ISharingServiceProtocol Protocol { get; }

        /// <summary>
        /// Get or set if in a room. Some types can only be safely parsed while in a room.
        /// </summary>
        public bool InRoom { get; set; }
        #endregion Public Properties

        #region Private Functions
        /// <summary>
        /// Register app message data type
        /// </summary>
        private void RegisterAppMessage()
        {
            RegisterCustomMessageType<ProtocolMessage>(
                _protocolMessageDataType,
                SerializeAppMessage,
                DeserializeAppMessage);

            RegisterCustomMessageType<AvatarPose>(
                _protocolPlayerPoseDataType,
                SerializePlayerPose,
                DeserializePlayerPose);
        }

        /// <summary>
        /// Register a new custom event message.
        /// </summary>
        private void RegisterCustomMessageType<T>(
            byte typeCode,
            SerializeStreamMethod serializeMethod,
            DeserializeStreamMethod deserializeMethod)
        {
            Type type = typeof(T);
            var success = PhotonPeer.RegisterType(
                type,
                typeCode,
                serializeMethod,
                deserializeMethod);

            if (!success)
            {
                LogError("Unable to register type code '{0}' for type '{1}'", typeCode, type);
            }
        }

        /// <summary>
        /// Serialize app message data.
        /// </summary>
        private short SerializeAppMessage(StreamBuffer outStream, object customType)
        {
            short written = 0;
            if (customType is ProtocolMessage)
            {
                written = SerializeAppMessage(outStream, (ProtocolMessage)customType);
            }
            return written;
        }

        /// <summary>
        /// Serialize app message data.
        /// </summary>
        private short SerializeAppMessage(StreamBuffer outStream, ProtocolMessage message)
        {
            int written = Protocol.SerializeMessage(ref message, PhotonStream.CreateWriter(outStream));
            if (written > short.MaxValue)
            {
                throw new ArgumentOutOfRangeException();
            }
            return (short)written;
        }

        /// <summary>
        /// Deserialize app message data.
        /// </summary>
        private object DeserializeAppMessage(StreamBuffer inStream, short maxLength)
        {
            ProtocolMessage message = default;
            Protocol.DeserializeMessage(ref message, PhotonStream.CreateReader(inStream));
            return message;
        }

        /// <summary>
        /// Serialize a player pose
        /// </summary>
        private short SerializePlayerPose(StreamBuffer outStream, object customType)
        {
            return SerializeObject(outStream, ProtocolMessageDataType.SharingServicePlayerPose, customType);
        }

        /// <summary>
        /// Deserialize a player pose
        /// </summary>
        private object DeserializePlayerPose(StreamBuffer inStream, short maxLength)
        {
            if (InRoom)
            {
                return DeserializeObject(inStream, maxLength);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Serialize generic object.
        /// </summary>
        private short SerializeObject(StreamBuffer outStream, object customType)
        {
            return SerializeObject(outStream, Protocol.GetDataType(customType), customType);
        }

        /// <summary>
        /// Serialize generic object.
        /// </summary>
        private short SerializeObject(StreamBuffer outStream, ProtocolMessageDataType type, object customType)
        {
            int written = Protocol.SerializeObject(type, customType, PhotonStream.CreateWriter(outStream));
            if (written > short.MaxValue)
            {
                throw new ArgumentOutOfRangeException();
            }
            return (short)written;
        }

        /// <summary>
        /// DeserializeObject generic object.
        /// </summary>
        private object DeserializeObject(StreamBuffer inStream, short maxLength)
        {
            return Protocol.DeserializeObject(PhotonStream.CreateReader(inStream));
        }
        #endregion Private Functions

        #region Logging Methods
        /// <summary>
        /// Log a message if verbose logging is enabled.
        /// </summary>
        private void LogVerbose(string message)
        {
            _logger.LogVerbose(message);
        }

        /// <summary>
        /// Log a message if verbose logging is enabled. 
        /// </summary>
        private void LogVerbose(string messageFormat, params object[] args)
        {
            _logger.LogVerbose(messageFormat, args);
        }

        /// <summary>
        /// Log a message if information logging is enabled.
        /// </summary>
        private void LogInformation(string message)
        {
            _logger.LogInformation(message);
        }

        /// <summary>
        /// Log a message if information logging is enabled. 
        /// </summary>
        private void LogInformation(string messageFormat, params object[] args)
        {
            _logger.LogInformation(messageFormat, args);
        }

        /// <summary>
        /// Log a message if warning logging is enabled.
        /// </summary>
        private void LogWarning(string message)
        {
            _logger.LogWarning(message);
        }

        /// <summary>
        /// Log a message if warning logging is enabled. 
        /// </summary>
        private void LogWarning(string messageFormat, params object[] args)
        {
            _logger.LogWarning(messageFormat, args);
        }


        /// <summary>
        /// Log a message if error logging is enabled.
        /// </summary>
        private void LogError(string message)
        {
            _logger.LogError(message);
        }

        /// <summary>
        /// Log a message if error logging is enabled. 
        /// </summary>
        private void LogError(string messageFormat, params object[] args)
        {
            _logger.LogError(messageFormat, args);
        }
        #endregion Logging Methods

        #region Private Class 
        #endregion Private Class
    }
}
#endif //PHOTON_INSTALLED