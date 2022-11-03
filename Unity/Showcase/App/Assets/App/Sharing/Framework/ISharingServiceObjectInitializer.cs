// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    public interface ISharingServiceObjectInitializer
    {
        /// <summary>
        /// Initialize the sharing service object.
        /// </summary>
        void InitializeSharingObject(ISharingServiceObject sharingObject, object[] data);
    }
}
