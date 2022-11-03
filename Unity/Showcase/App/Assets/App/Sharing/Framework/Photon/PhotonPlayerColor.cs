// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if PHOTON_INSTALLED
using Photon.Voice.PUN;
using Photon.Voice.Unity;
using System;
using UnityEngine;
using UnityEngine.Events;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    /// <summary>
    /// A helper for assigning colors to the current photon player
    /// </summary>
    public class PhotonPlayerColor
    {
        private PhotonParticipant _participant;
        private PhotonProperties _properties;
        private Color[] _primaryColors = null;

        public PhotonPlayerColor(PhotonParticipant participant, PhotonProperties properties)
        {
            _participant = participant;
            _properties = properties ?? throw new ArgumentNullException("Properties can't be null");
        }

        public Color[] PrimaryColors
        {
            get => _primaryColors;
            set
            {
                if (_primaryColors != value)
                {
                    _primaryColors = value;
                    ApplyPrimaryColor();
                }
            }
        }

        /// <summary>
        /// Apply the player's color
        /// </summary>
        public void ApplyPrimaryColor()
        {
            if (_primaryColors == null ||
                _primaryColors.Length == 0)
            {
                return;
            }

            int index = _participant.ActorNumber;

            if (index < 0)
            {
                index = string.IsNullOrEmpty(_participant.DisplayName) ?
                    UnityEngine.Random.Range(0, _primaryColors.Length - 1) :
                    _participant.DisplayName.GetHashCode();
            }

            index = index % _primaryColors.Length;
            _properties.SetSessionParticipantProperty(SharableStrings.PlayerPrimaryColor, _primaryColors[index]);
        }
    }
}
#endif // PHOTON_INSTALLED
