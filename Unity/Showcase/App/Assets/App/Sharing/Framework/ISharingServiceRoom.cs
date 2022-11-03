// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// Represents a room that can be joined via the sharing service.
    /// </summary>
    public interface ISharingServiceRoom
    {
        /// <summary>
        /// The name of the sharing service room.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Get if this is a private room.
        /// </summary>
        bool IsPrivate { get; }

        /// <summary>
        /// If this is an inviation, this will be filled with the invitation sender id.
        /// </summary>
        bool IsInvitation { get; }

        /// <summary>
        /// If this is an inviation, this will be filled with the invitation's sender display name.
        /// </summary>
        string InvitationSender { get; }
    }
}

