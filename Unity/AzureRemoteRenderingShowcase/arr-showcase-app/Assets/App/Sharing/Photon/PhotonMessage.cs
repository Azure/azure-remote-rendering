// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if PHOTON_INSTALLED
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    /// <summary>
    /// Message sent or received over the sharing service
    /// </summary>
    public class PhotonMessage : SharingServiceMessage
    {
        public PhotonMessage(
            string target,
            string command)
        {
            Target = target;
            Command = command;
        }

        /// <summary>
        /// The player id that sent the message.
        /// </summary>
        public int Sender { get; private set; } = -1;

        /// <summary>
        /// Initialize the sender index.
        /// </summary>
        public void InitializeSender(int sender)
        {
            if (Sender >= 0)
            {
                Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}", "Attempting to set a sender that's already been set.");
                return;
            }

            Sender = sender;
        }
    }
}
#endif // PHOTON_INSTALLED
