// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if PHOTON_INSTALLED
using Photon.Pun;
using Photon.Realtime;
using System;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    /// <summary>
    /// Helps with transferring ownership of Photon Views
    /// </summary>
    public class PhotonOwnership : IPunOwnershipCallbacks, IDisposable
    {
        private LogHelper<PhotonOwnership> _logger = new LogHelper<PhotonOwnership>();

        #region Constructor
        private PhotonOwnership(SharingServiceProfile settings)
        {
            _logger.Verbose = settings.VerboseLogging ? LogHelperState.Always : LogHelperState.Default;
            PhotonNetwork.AddCallbackTarget(this);
        }
        #endregion Constructor

        #region Public Methods
        public static PhotonOwnership Create(SharingServiceProfile settings)
        {
            return new PhotonOwnership(settings);
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
        }
        #endregion Public Methods

        #region IPunOwnershipCallbacks
        public void OnOwnershipRequest(PhotonView targetView, Player requestingPlayer)
        {
            LogVerbose("OnOwnershipRequest() (view = {0}) (request = {1})", targetView?.ViewID, requestingPlayer?.ActorNumber);
            if (targetView?.Owner != null &&
                targetView.Owner.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
            {
                LogVerbose("OnOwnershipRequest() transferring ownership");
                targetView.TransferOwnership(requestingPlayer);
            }
        }

        public void OnOwnershipTransfered(PhotonView targetView, Player previousOwner)
        {
            LogVerbose("OnOwnershipTransferred() (view = {0}) (old owner = {1}) (new owner = {2})", targetView?.ViewID, previousOwner?.ActorNumber, targetView?.Owner?.ActorNumber);
        }

        public void OnOwnershipTransferFailed(PhotonView targetView, Player senderOfFailedRequest)
        {
            LogVerbose("OnOwnershipTransferFailed() (view = {0}) (sender = {1})", targetView?.ViewID, senderOfFailedRequest?.ActorNumber);
        }
        #endregion IPunOwnershipCallbacks

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

