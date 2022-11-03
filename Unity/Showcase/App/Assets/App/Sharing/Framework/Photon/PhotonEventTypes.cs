// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if PHOTON_INSTALLED
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using System;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    /// <summary>
    /// The Photon custom event types
    /// </summary>
    public enum PhotonEventTypes
    {
        PlayerPoseEvent = 198,
        ProtocolMessageEvent = 199,

        Max = 199
    }

}
#endif // PHOTON_INSTALLED
