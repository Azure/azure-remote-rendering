// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// Message to be sent over the sharing service
    /// </summary>
    public class SharingServiceMessage: ISharingServiceMessage
    {
        /// <summary>
        /// The target object to send the message to. This maybe null, in which cases this is an global message.
        /// </summary>
        public string Target { get; set; }

        /// <summary>
        /// Command to send
        /// </summary>
        public string Command { get; set; }
    }
}
