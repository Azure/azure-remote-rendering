// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if PHOTON_INSTALLED
using Photon.Pun;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    public class PhotonViewExtended : PhotonView
    {
    }
}
#else
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    /// <summary>
    /// A PhotonView that is available is Photon is not installed.
    /// </summary>
    public class PhotonViewExtended : MonoBehaviour
    {
        [SerializeField]
        [HideInInspector]
        private int sceneViewId = 0;
    }
}
#endif // PHOTON_INSTALLED
