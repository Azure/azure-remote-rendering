// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if PHOTON_INSTALLED
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    public class PhotonLocation : SharingServiceLocation, IDisposable
    {
        List<IDisposable> _owenedDisables = new List<IDisposable>();

        #region Constructor
        private PhotonLocation(
            ISharingServiceRoomAddresses ownedAddresses,
            ISharingServiceAddressFactory ownedFactory,
            ISharingServiceAddressSearchStrategy ownedSearch) : base(ownedAddresses, ownedFactory, ownedSearch)
        {
            if (ownedAddresses is IDisposable)
            {
                _owenedDisables.Add((IDisposable)ownedAddresses);
            }

            if (ownedFactory is IDisposable)
            {
                _owenedDisables.Add((IDisposable)ownedFactory);
            }

            if (ownedSearch is IDisposable)
            {
                _owenedDisables.Add((IDisposable)ownedSearch);
            }
        }
        #endregion Constructor

        #region Protected Methods
        protected override void OnDispose(bool disposing)
        {
            if (disposing && _owenedDisables != null)
            {
                foreach (var dispose in _owenedDisables)
                {
                    dispose.Dispose();
                }
                _owenedDisables = null;
            }
        }
        #endregion Protected Methods

        #region Public Methods
        public static async Task<PhotonLocation> CreateFromRoom(
            SharingServiceProfile settings, 
            PhotonSharingRoom room,
            PhotonProperties properties)
        {
            bool useAzureSpatialAnchors =
                await AppServices.AnchoringService.IsReady() &&
                AppServices.AnchoringService.IsCloudEnabled;

            ISharingServiceRoomAddresses rooms = new PhotonSharingRoomAddresses(room, properties);
            ISharingServiceAddressSearchStrategy search = new AzureSpatialAnchorSearchStrategy(settings, rooms);

            ISharingServiceAddressFactory factory;
            if (useAzureSpatialAnchors)
            {
                factory = new AzureSpatialAnchorFactory();
            }
            else
            {
                factory = new DeviceAddressFactory();
            }

            return new PhotonLocation(rooms, factory, search);
        }
        #endregion Public Methods
    }
}
#endif // PHOTON_INSTALLED
