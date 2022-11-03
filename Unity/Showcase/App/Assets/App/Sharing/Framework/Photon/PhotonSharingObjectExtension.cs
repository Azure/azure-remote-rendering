// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if PHOTON_INSTALLED
using Photon.Pun;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    public static class PhotonSharingObjectExtension
    { 
        /// <summary>
        /// Register the app's SharingObjects with Photon's networking framework.
        /// </summary>
        /// <remarks>
        /// This helper functions removes the requirement of adding PhotonViews to the scene at compile time. 
        /// Making it possible to dynamically switch between networking platforms.
        /// </remarks>
        public static void RegisterWithPhoton(this SharingObject[] sharingObjects)
        {
            if (sharingObjects != null && Application.isPlaying)
            {
                foreach (var sharingObject in sharingObjects)
                {
                    sharingObject.RegisterWithPhoton();
                }
            }
        }

        /// <summary>
        /// Register the app's SharingObject with the Photon's networking framework.
        /// </summary>
        /// <remarks>
        /// This helper functions removes the requirement of adding NetObjects to the scene at compile time. 
        /// Making it possible to dynamically switch between networking platforms.
        /// </remarks>
        public static void RegisterWithPhoton(this SharingObject sharingObject)
        {
            if (sharingObject != null && Application.isPlaying)
            {
                var photonView = sharingObject.EnsureComponent<PhotonView>();
                if (photonView.ViewID != 0 &&
                    int.TryParse(sharingObject.Label, out int netId))
                {
                    photonView.ViewID = netId;
                    photonView.OwnershipTransfer = OwnershipOption.Takeover;
                }
            }
        }
    }
}
#endif // PHOTON_INSTALLED
