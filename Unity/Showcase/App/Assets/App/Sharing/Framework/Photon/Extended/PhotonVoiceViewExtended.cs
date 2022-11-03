// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if PHOTON_INSTALLED
using Photon.Voice.PUN;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    public class PhotonVoiceViewExtended : PhotonVoiceView
    {
        protected override void Awake()
        {
            if (!PhotonFeatureSupport.HasVoice)
            {
                DestroyImmediate(this);
            }
            else
            {
                base.Awake();
            }
        }
    }
}
#else
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    /// <summary>
    /// A PhotonVoiceView that is available is Photon is not installed.
    /// </summary>
    public class PhotonVoiceViewExtended : MonoBehaviour
    {
        [SerializeField]
        [HideInInspector]
        private bool UsePrimaryRecorder;
    }
}
#endif // PHOTON_INSTALLED