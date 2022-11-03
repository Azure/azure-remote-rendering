// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.SpatialAnchors;
using Microsoft.Azure.SpatialAnchors.Unity;
using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// Provides all service address for a given session/room, and creates platform addresses for the a given session
    /// </summary>
    public interface ISharingServiceRoomAddresses 
    {
        /// <summary>
        /// Get the room name for these addresses
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Event firen when the participants changed at the given address
        /// </summary>
        event Action<ISharingServiceRoomAddresses, ParticipantsChangedArgs> ParticipantsChanged;

        /// <summary>
        /// Set the address for the current participant
        /// </summary>
        Task<IEnumerable<SharingServiceAddress>> GetAddresses();

        /// <summary>
        /// Get the address the player.
        /// </summary>
        SharingServiceAddress GetAddress(string playerId);

        /// <summary>
        /// Set the address for the current participant
        /// </summary>
        Task SetAddress(SharingServiceAddress platformAddress);  
    }

    public class ParticipantsChangedArgs
    {
        public ParticipantsChangedArgs(SharingServiceAddress address)
        {
            Address = address;
        }

        public SharingServiceAddress Address { get; private set; }
    }
}
