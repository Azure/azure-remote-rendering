// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if PHOTON_INSTALLED
using Photon.Realtime;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    /// <summary>
    /// A structure to store participant id amd participant object. This is used so the id is
    /// persisted even after the underlaying participant information is destroyed.
    /// </summary>
    public struct PhotonParticipant
    {
        public readonly string Identifier;
        public readonly Player Inner;
        public readonly int ActorNumber;
        public readonly bool IsLocal;

        /// <summary>
        /// Get the display name of the user.
        /// </summary>
        /// <remarks>
        /// The inner nick name can change during the session.
        /// </remarks>
        public string DisplayName => Inner?.NickName;

        public PhotonParticipant(Player participant)
        {
            Identifier = PhotonHelpers.UserIdToString(participant);
            ActorNumber = participant.ActorNumber;
            Inner = participant;
            IsLocal = participant.IsLocal;
        }

        public SharingServicePlayerData ToPlayerData()
        {
            return new SharingServicePlayerData(
                Inner.NickName,
                SharingServicePlayerStatus.Unknown,
                Identifier,
                IsLocal,
                tenantId: string.Empty,
                tenantUserId: string.Empty);
        }
    }
}
#endif // PHOTON_INSTALLED