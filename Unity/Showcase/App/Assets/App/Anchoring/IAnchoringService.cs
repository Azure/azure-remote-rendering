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
        /// Can cloud anchors be created.
        /// </summary>
        bool IsCloudEnabled { get; }

        /// <summary>
        /// Can native anchors be created
        /// </summary>
        bool IsNativeEnabled { get; }

        /// <summary>
        /// The number of active anchors creations
        /// </summary>
        int ActiveCreationsCount { get; }

        /// <summary>
        /// Get if the service is currently creating new cloud anchors.
        /// </summary>
        bool IsCreating { get; }

        /// <summary>
        /// Get or set the current find options. After setting this, the following Find() operations will use these settings.
        /// </summary>
        AnchoringServiceFindOptions FindOptions { get; set; }

        /// <summary>
        /// Event raised when the SearchesCount value has changed.
        /// </summary>
        event Action<IAnchoringService, AnchoringServiceSearchingArgs> ActiveSearchesCountChanged;

        /// <summary>
        /// Event raised when the ActiveCreationsCount value has changed.
        /// </summary>
        event Action<IAnchoringService, AnchoringServiceCreatingArgs> ActiveCreationsCountChanged;

        /// <summary>
        /// Wait for the anchoring service to initialize
        /// </summary>
        Task<bool> IsReady();

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
        /// Start finding cloud spatial anchor, once found returned task is compelted.
        /// </summary>
        /// <param name="cancellationToken">Cancel the search by setting this cancellation token.</param>
        Task<CloudSpatialAnchor> FindNearest(CancellationToken cancellationToken);

        /// <summary>
        /// Start finding cloud spatial anchor, once found returned task is compelted.
        /// </summary>
        /// <param name="cancellationToken">Cancel the search by setting this cancellation token.</param>
        /// <param name="timeoutForFirstInSeconds">The timeout for finding at least one anchor.</param>
        /// <param name="timeoutForOthersInSeconds">After the first anchor is found, the timeout for finding all other anchors.</param>
        Task<CloudSpatialAnchor[]> FindAll(string[] cloudSpatialAnchorIds, float timeoutForFirstInSeconds, float timeoutForOthersInSeconds, CancellationToken cancellationToken);

        /// <summary>
        /// Save the given cloud spatial anchor
        /// </summary>
        Task<string> Save(CloudSpatialAnchor cloudSpatialAnchor);

        /// <summary>
        /// Save the given cloud spatial anchor
        /// </summary>
        void Delete(CloudSpatialAnchor cloudSpatialAnchor);

        /// <summary>
        /// Extract the time the app service updated this anchor.
        /// </summary>
        DateTime UpdateTime(CloudSpatialAnchor cloudSpatialAnchor);
    }
}
