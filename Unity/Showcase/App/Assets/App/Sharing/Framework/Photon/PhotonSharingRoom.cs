// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if PHOTON_INSTALLED
using Photon.Realtime;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// Represents a Photon room that can be joined via the sharing service.
    /// </summary>
    public class PhotonSharingRoom : ISharingServiceRoom
    {
        public PhotonSharingRoom(string name, Room joinedRoom)
        {
            Name = name;
            Inner = joinedRoom;
            InnerInfo = joinedRoom;
        }

        public PhotonSharingRoom(string name, RoomInfo info)
        {
            Name = name;
            Inner = null;
            InnerInfo = info;
        }

        #region Public Properties
        /// <summary>
        /// The joined Photon Room. Only non-null if current room
        /// </summary>
        public Room Inner { get; private set; }

        /// <summary>
        /// The info of the Photon Room.
        /// </summary>
        public RoomInfo InnerInfo { get; private set; }
        #endregion Public Properties

        #region ISharingServiceRoom Properties
        /// <summary>
        /// The name of the sharing service room.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Get if this is a private room.
        /// </summary>
        public bool IsPrivate { get; }

        /// <summary>
        /// Is this an invitation to join the room.
        /// </summary>
        public bool IsInvitation { get; }

        /// <summary>
        /// If this is an invitation to join the room, this value will be populated with the invitation sender's display name.
        /// </summary>
        public string InvitationSender { get; }
        #endregion ISharingServiceRoom Properties
    }
}
#endif // PHOTON_INSTALLED
