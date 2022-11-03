// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
#if UNITY_EDITOR
    public class PhotonResourceEditor : SharingServiceResourceEditor
    {
        public PhotonResourceEditor() : base(SharingServiceProfile.ProviderService.Photon)
        { }

        protected override void InitializeVariant(GameObject variant)
        {
#if PHOTON_INSTALLED
            var view = variant.EnsureComponent<PhotonViewExtended>();
            view.OwnershipTransfer = global::Photon.Pun.OwnershipOption.Takeover;
            view.observableSearch = global::Photon.Pun.PhotonView.ObservableSearch.Manual;
#endif
        }
    }
#endif
}
