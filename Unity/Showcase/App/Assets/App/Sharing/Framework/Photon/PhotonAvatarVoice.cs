// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if PHOTON_INSTALLED
using Photon.Voice.PUN;
using Photon.Voice.Unity;
#endif // PHOTON_INSTALLED

using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    public class PhotonAvatarVoice : MonoBehaviour
    {
        #region MonoBehaviour Functions
        private void Start()
        {
            Initialize();
        }
        #endregion MonoBehavior Functions

        #region Private Functions
        private void Initialize()
        {
#if PHOTON_INSTALLED
            gameObject.EnsureComponent<PhotonVoiceView>();
            var audioSource = GetComponentInChildren<AudioSource>(includeInactive: true);
            if (audioSource != null)
            {
                audioSource.EnsureComponent<SpeakerExtended>();
            }
            else
            {
                gameObject.EnsureComponent<SpeakerExtended>();
            }
#endif // PHOTON_INSTALLED
        }
        #endregion MonoBehavior Functions
    }
}
