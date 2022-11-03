// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// Message sent or received over the sharing service
    /// </summary>
    public interface ISharingServiceMessage
    {
        /// <summary>
        /// The target object to send the message to. This maybe null, in which cases this is an app-wide message.
        /// </summary>
        string Target { get; }

        /// <summary>
        /// Command to send
        /// </summary>
        string Command { get; }

        /// <summary>
        /// The message sender
        /// </summary>
        string Sender { get; }
    }
}
