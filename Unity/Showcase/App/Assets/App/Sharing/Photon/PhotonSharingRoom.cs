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
        public PhotonSharingRoom(string name, RoomInfo info)
        {
            Name = name;
            RoomInfo = info;
        }

    #region Public Properties
        /// <summary>
        /// The name of the sharing service room.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The id of the Photon Room.
        /// </summary>
        public RoomInfo RoomInfo { get; set; }
    #endregion Public Properties
    }
}
#endif // PHOTON_INSTALLED
