// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if PHOTON_INSTALLED
using Photon.Realtime;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    public class PhotonSharingRoomAddresses : ISharingServiceRoomAddresses, IDisposable
    {
        private PhotonSharingRoom _room = null;
        private PhotonProperties _properties = null;
        private SharingServiceAddress _primaryAddress = null;

        private const string _asaAddressType = "asa";
        private const string _deviceAddressType = "device";
        private const string _addressProperty = "_player_address_";

        #region Constructor
        public PhotonSharingRoomAddresses(PhotonSharingRoom room, PhotonProperties properties)
        {
            _room = room ?? throw new ArgumentNullException("Room can't be null");
            _properties = properties ?? throw new ArgumentNullException("Properties can't be null");
            _properties.RegisterPrivateProperty(_addressProperty);
            _properties.PrivatePlayerPropertyChanged += OnPrivatePlayerPropertyChanged;
        }
        #endregion Constructor

        #region IDisposable
        public void Dispose()
        {
            _properties.PrivatePlayerPropertyChanged -= OnPrivatePlayerPropertyChanged;
        }
        #endregion IDisposable

        #region ISharingServiceRoomAddresses
        /// <summary>
        /// Get the room name for these addresses
        /// </summary>
        public string Name => _room?.Name;

        /// <summary>
        /// Event invoked when the participants changed at the given address
        /// </summary>
        public event Action<ISharingServiceRoomAddresses, ParticipantsChangedArgs> ParticipantsChanged;

        /// <summary>
        /// Get all the known addresses
        /// </summary>
        public Task<IEnumerable<SharingServiceAddress>> GetAddresses()
        {
            var asaAddresses = new HashSet<string>();
            var players = _room.Inner?.Players;
            if (players != null)
            {
                foreach (var player in players)
                {
                    if (_properties.TryGetSessionParticipantProperty(player.Value, _addressProperty, out object addressObject))
                    {
                        string address = addressObject as string;
                        if (!string.IsNullOrEmpty(address))
                        {
                            asaAddresses.Add(address);
                        }
                    }
                }
            }

            var result = new List<SharingServiceAddress>(asaAddresses.Count);
            foreach (var asaAddress in asaAddresses)
            {
                result.Add(new SharingServiceAddress(SharingServiceAddressType.Anchor, asaAddress));
            }
            return Task.FromResult<IEnumerable<SharingServiceAddress>>(result);
        }

        /// <summary>
        /// Get the address the player.
        /// </summary>
        public SharingServiceAddress GetAddress(string playerId)
        {
            string address = null;
            if (_properties.TryGetSessionParticipantProperty(playerId, _addressProperty, out object addressObject))
            {
                address = addressObject as string;
            }

            if (!string.IsNullOrEmpty(address))
            {
                return new SharingServiceAddress(SharingServiceAddressType.Anchor, address);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Set the address for the current participant
        /// </summary>
        public Task SetAddress(SharingServiceAddress address)
        {
            _primaryAddress = address;
            _properties.SetSessionParticipantProperty(_addressProperty, address.Type == SharingServiceAddressType.Anchor ? address.Data : null);
            return Task.CompletedTask;
        }
        #endregion ISharingServiceRoomAddressesd

        #region Private Functions
        private static bool IsAnchorAddress(string type)
        {
            return type == _asaAddressType;
        }

        private static bool IsDeviceAddress(string type)
        {
            return type == _deviceAddressType;
        }

        private static SharingServiceAddressType AddressType(string type)
        {
            if (IsAnchorAddress(type))
            {
                return SharingServiceAddressType.Anchor;
            }
            else
            {
                // assume device
                return SharingServiceAddressType.Device;
            }
        }


        /// <summary>
        /// Handle private property changes.
        /// </summary>
        private void OnPrivatePlayerPropertyChanged(PhotonProperties sender, string playerId, string name, object newValue, object oldValue)
        {
            if (_properties == sender)
            {
                switch (name)
                {
                    case _addressProperty:
                        NotifyThatParticipantsChanged(oldValue as string);
                        NotifyThatParticipantsChanged(newValue as string);
                        break;
                }
            }
        }

        /// <summary>
        /// If this given address is the current primary address. Notify others that the participants have changed.
        /// </summary>
        /// <param name="address"></param>
        private void NotifyThatParticipantsChanged(string address)
        {
            if (address != null && 
                _primaryAddress != null && 
                _primaryAddress.Type == SharingServiceAddressType.Anchor && 
                _primaryAddress.Data == address)
            {
                ParticipantsChanged?.Invoke(this, new ParticipantsChangedArgs(_primaryAddress));
            }
        }
        #endregion Private Methods
    }
}
#endif // PHOTON_INSTALLED