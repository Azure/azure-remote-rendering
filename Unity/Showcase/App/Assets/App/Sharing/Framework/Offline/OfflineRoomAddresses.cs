// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    public class OfflineRoomAddresses : ISharingServiceRoomAddresses
    {
        #region Constructor
        public OfflineRoomAddresses()
        {
        }
        #endregion Constructor

        #region ISharingServiceRoomAddresses
        /// <summary>
        /// Get the room name for these addresses
        /// </summary>
        public string Name => "Offline";

#pragma warning disable 0067
        /// <summary>
        /// Event invoked when the participants changed at the given address
        /// </summary>
        public event Action<ISharingServiceRoomAddresses, ParticipantsChangedArgs> ParticipantsChanged;
#pragma warning restore 0067

        /// <summary>
        /// Set the address for the current participant
        /// </summary>
        public Task<IEnumerable<SharingServiceAddress>> GetAddresses()
        {
            var result = new List<SharingServiceAddress>();
            return Task.FromResult<IEnumerable<SharingServiceAddress>>(result);
        }

        /// <summary>
        /// Get the address the player.
        /// </summary>
        public SharingServiceAddress GetAddress(string playerId)
        {
            return null;
        }

        /// <summary>
        /// Set the address for the current participant
        /// </summary>
        public Task SetAddress(SharingServiceAddress address)
        {
            return Task.CompletedTask;
        }
        #endregion ISharingServiceRoomAddresses
    }
}
