// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
	public interface IRemoteObjectFactoryService : IMixedRealityExtensionService
    {
        /// <summary>
        /// Get the total loading progress
        /// </summary>
        float Progress { get; }

        /// <summary>
        /// Load a model.
        /// </summary>
        ModelProgressStatus Load(RemoteModel model, Entity parent);
	}
}
