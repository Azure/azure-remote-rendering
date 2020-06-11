// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.SpatialAnchors;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// This finds, deletes, and creates Azure Spatial Anchors. The Azure Spatial Anchors found by this class are wrapped inside an IAppAnchor object.
    /// </summary>
	public interface IAnchoringService : IMixedRealityExtensionService
    {
        /// <summary>
        /// The number of active searches.
        /// </summary>
        int ActiveSearchesCount { get; }

        /// <summary>
        /// Get if the service is currently searching for cloud anchors in the real-world.
        /// </summary>
        bool IsSearching { get; }

        /// <summary>
        /// The number of active anchors creations
        /// </summary>
        int ActiveCreationsCount { get; }

        /// <summary>
        /// Get if the service is currently creating new cloud anchors.
        /// </summary>
        bool IsCreating { get; }

        /// <summary>
        /// Event raised when the SearchesCount value has changed.
        /// </summary>
        event Action<IAnchoringService, AnchoringServiceSearchingArgs> ActiveSearchesCountChanged;

        /// <summary>
        /// Event raised when the ActiveCreationsCount value has changed.
        /// </summary>
        event Action<IAnchoringService, AnchoringServiceCreatingArgs> ActiveCreationsCountChanged;

        /// <summary>
        /// Start finding cloud spatial anchor, once found returned task is compelted.
        /// </summary>
        Task<CloudSpatialAnchor> Find(string cloudSpatialAnchorId);

        /// <summary>
        /// Start finding cloud spatial anchor, once found returned task is compelted.
        /// </summary>
        /// <param name="cancellationToken">Cancel the search by setting this cancellation token.</param>
        Task<CloudSpatialAnchor> Find(string cloudSpatialAnchorId, CancellationToken cancellationToken);

        /// <summary>
        /// Save the given cloud spatial anchor
        /// </summary>
        Task<string> Save(CloudSpatialAnchor cloudSpatialAnchor);

        /// <summary>
        /// Save the given cloud spatial anchor
        /// </summary>
        void Delete(CloudSpatialAnchor cloudSpatialAnchor);
    }
}
