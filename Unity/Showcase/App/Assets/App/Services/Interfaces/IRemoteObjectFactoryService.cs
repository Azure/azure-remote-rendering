// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using System;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
	public interface IRemoteObjectFactoryService : IMixedRealityExtensionService
    {
        /// <summary>
        /// Get the total loading progress
        /// </summary>
        float Progress { get; }

        /// <summary>
        /// Get if there are models currently being loaded.
        /// </summary>
        bool IsLoading { get; }

        /// <summary>
        /// Event raised when loading of models has started.
        /// </summary>
        event Action<IRemoteObjectFactoryService> LoadStarted;


        /// <summary>
        /// Event raised when loading of models has completed.
        /// </summary>
        event Action<IRemoteObjectFactoryService> LoadCompleted;

        /// <summary>
        /// Load a model.
        /// </summary>
        Task<LoadModelResult> Load(RemoteModel model, Entity parent);
	}
}
