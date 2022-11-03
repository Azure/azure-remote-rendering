// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if PHOTON_INSTALLED
namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    public struct PhotonMessage
    {
        public PhotonParticipant sender;

        public ProtocolMessage inner;
    }
}
#endif // PHOTON_INSTALLED