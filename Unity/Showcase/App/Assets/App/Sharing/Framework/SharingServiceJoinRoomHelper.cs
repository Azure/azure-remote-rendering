// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// A behavior for the editor, to help join a room with a given name.
    /// </summary>
    public class SharingServiceJoinRoomHelper : MonoBehaviour
    {
        #region Serializable Fields
        [SerializeField]
        [Tooltip("The room to enter when 'Join()' is called. This can be called from the Inspector button.")]
        private string roomId = null;

        /// <summary>
        /// The room to enter when 'Join()' is called. This can be called from the Inspector button.
        /// </summary>
        public string RoomId
        {
            get => roomId;
            set => roomId = null;
        }
        #endregion Serializable Fields

        #region Public Functions
        /// <summary>
        /// Enter the room given by the current value of 'RoomId'.
        /// </summary>
        public void Join()
        {
            if (!string.IsNullOrEmpty(roomId))
            {
                AppServices.SharingService.JoinRoom(roomId);
            }
        }
        #endregion Public Functions
    }
}
