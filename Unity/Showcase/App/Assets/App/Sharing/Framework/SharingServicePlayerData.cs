// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    public struct SharingServicePlayerData
    {
        public SharingServicePlayerData(string displayName, SharingServicePlayerStatus status, string playerId, bool isLocal, string tenantId, string tenantUserId)
        {
            DisplayName = displayName;
            Status = status;
            PlayerId = playerId;
            TenantId = tenantId;
            TenantUserId = tenantUserId;
            IsLocal = isLocal;
        }

        /// <summary>
        /// Get the display name
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Get the status
        /// </summary>
        public SharingServicePlayerStatus Status { get; private set; }

        /// <summary>
        /// The id of this player, for the current sharing room/session.
        /// </summary>
        public string PlayerId { get; private set; }

        /// <summary>
        /// Get a tenant id that this player belongs to.
        /// </summary>
        public string TenantId { get; private set; }

        /// <summary>
        /// Get the id of the user in their given tenant.
        /// </summary>
        public string TenantUserId { get; private set; }

        /// <summary>
        /// Get if this player is local
        /// </summary>
        public bool IsLocal { get; private set; }
    }
}
