// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if PHOTON_INSTALLED
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using System;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    public class PhotonMessages :
        IOnEventCallback,
        IDisposable
    {
        private PhotonParticipants _participants = null;
        private LogHelper<PhotonMessages> _logger = new LogHelper<PhotonMessages>();

        #region Constructors
        private PhotonMessages(SharingServiceProfile settings, PhotonParticipants participants)
        {
            _logger.Verbose = settings.VerboseLogging ? LogHelperState.Always : LogHelperState.Default;
            _participants = participants ?? throw new ArgumentNullException("Participants can't be null");
            PhotonNetwork.AddCallbackTarget(this);
        }
        #endregion Constructors

        #region Public Events
        /// <summary>
        /// Event raised when a data event has occurred.
        /// </summary>
        public event Action<PhotonMessages, PhotonMessage> MessageReceived;
        #endregion Public Events

        #region Public Functions
        /// <summary>
        /// Initialize data transport.
        /// </summary>
        public static PhotonMessages CreateFromParticipants(
            SharingServiceProfile settings, PhotonParticipants participants)
        {
            return new PhotonMessages(settings, participants);
        }

        /// <summary>
        /// Release resources.
        /// </summary>
        public void Dispose()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
        }

        /// <summary>
        /// Send a new message
        /// </summary>
        public bool SendMessage(ProtocolMessage message)
        {
            return SendMessage(targetId: -1, message);
        }

        /// <summary>
        /// Send a new message
        /// </summary>
        public bool SendMessage(string targetId, ProtocolMessage message)
        {
            return SendMessage(PhotonHelpers.UserIdFromString(targetId), message);
        }
        #endregion Public Functions

        #region IOnEventCallback
        /// <summary>
        /// Called for any incoming events.
        /// </summary>
        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code == (byte)PhotonEventTypes.ProtocolMessageEvent)
            {
                LogVerbose("OnEvent() (code = {0})", photonEvent.Code);
                ReceiveMessage(photonEvent.Sender, (ProtocolMessage)photonEvent.CustomData);
            }
        }
        #endregion

        #region Private Functions
        /// <summary>
        /// Send a new message
        /// </summary>
        private bool SendMessage(int targetId, ProtocolMessage message)
        {
            var options = RaiseEventOptions.Default;
            if (targetId >= 0)
            {
                options.TargetActors = new int[] { targetId };
            }    

            return PhotonNetwork.RaiseEvent(
                (byte)PhotonEventTypes.ProtocolMessageEvent,
                message, 
                options,
                SendOptions.SendReliable);
        }

        /// <summary>
        /// Receive a custom message
        /// </summary>
        private void ReceiveMessage(int senderId, ProtocolMessage message)
        {
            PhotonParticipant participant;
            _participants.TryFind(senderId, out participant);
            MessageReceived?.Invoke(this, new PhotonMessage()
            {
                sender = participant,
                inner = message
            });
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
    }
}
#endif // PHOTON_INSTALLED
