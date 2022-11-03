// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.SpatialAnchors;
using Microsoft.Azure.SpatialAnchors.Unity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// Use the Azure Spatial Anchor SDK to find and create user location addresses
    /// </summary>
    public sealed class AzureSpatialAnchorSearchStrategy : CloudAddressSearchStrategy
    {
        private LogHelper<AzureSpatialAnchorSearchStrategy> _logger = new LogHelper<AzureSpatialAnchorSearchStrategy>();

        #region Constructor
        public AzureSpatialAnchorSearchStrategy(
            SharingServiceProfile settings, ISharingServiceRoomAddresses platformAddressing) : base(platformAddressing)
        {
            _logger.Verbose = settings.VerboseLogging ? LogHelperState.Always : LogHelperState.Default;
        }
        #endregion Constructor

        #region Protected Functions
        /// <summary>
        /// Compute if the device is able to find cloud anchors.
        /// </summary>
        /// <returns></returns>
        protected override bool CanFindAddressAnchors()
        {
            return AnchorSupport.IsNativeEnabled;
        }

        protected override async Task<IList<SharingServiceAddress>> FindAddressAnchorsFromKnownAddresses(IEnumerable<SharingServiceAddress> anchors, CancellationToken ct)
        {
            _logger.LogVerbose("FindAddressAnchorFromKnownAddresses() Entered");

            // Kick-off search, with a timeout 
            SetAnchoringServiceFindOptions();
            CloudSpatialAnchor[] searchResults = null;
            if (anchors != null && anchors.Count() > 0)
            {
                searchResults = await FindAllAnchors(anchors, ct);
            }
            else
            {
                _logger.LogVerbose("The session has no anchors to search for.");
            }
            ct.ThrowIfCancellationRequested();

            // Create result
            IList<SharingServiceAddress> result;
            if (searchResults != null)
            {
                result = await CreateSharingServiceAddresses(searchResults, ct);
                ct.ThrowIfCancellationRequested();
            }
            else
            {
                _logger.LogVerbose("Failed to find any anchors.");
                result = new List<SharingServiceAddress>();
            }

            _logger.LogVerbose("FindAddressAnchor() Exitting ({0})", result);
            return result;
        }
        #endregion Protected Functions

        #region Private Functions
        private async Task<IList<SharingServiceAddress>> CreateSharingServiceAddresses(CloudSpatialAnchor[] cloudAnchors, CancellationToken ct)
        {
            _logger.LogVerbose("CreateSharingServiceAddresses() Entered");

            Array.Sort(cloudAnchors, CompareAnchorsByDateDescendingOrder);

            List<Task<SharingServiceAddress>> resultTasks = new List<Task<SharingServiceAddress>>();
            foreach (var cloudAnchor in cloudAnchors)
            {
                if (cloudAnchor?.Identifier != null)
                {
                    _logger.LogVerbose("Wrapping a cloud anchor object in a SharingServiceAddress, and searching for native anchor {0}", cloudAnchor.Identifier);
                    resultTasks.Add(SharingServiceAddress.LoadAddress(cloudAnchor.Identifier, cloudAnchor.GetPose(), ct));
                }
                else
                {
                    _logger.LogVerbose("Ignoring an invalid cloud anchor object {0}", cloudAnchor?.Identifier);
                }
            }
            var result = await Task.WhenAll(resultTasks);
            ct.ThrowIfCancellationRequested();

            _logger.LogVerbose("CreateSharingServiceAddresses() Exitting");
            return result;
        }

        /// <summary>
        /// Find the nearest anchors and filter to the given set of anchor ids
        /// </summary>
        private static Task<CloudSpatialAnchor[]> FindAllAnchors(IEnumerable<SharingServiceAddress> anchors, CancellationToken ct)
        {
            return AppServices.AnchoringService.FindAll(
                anchors.Select(a => a.Data).ToArray(),
                timeoutForFirstInSeconds: 30,
                timeoutForOthersInSeconds: 5,
                ct);
        }

        /// <summary>
        /// Set the find option when searching for 'address' anchors near users
        /// </summary>
        private static void SetAnchoringServiceFindOptions()
        {
            AppServices.AnchoringService.FindOptions = new AnchoringServiceFindOptions()
            {
                NearDevice = true,
                MaxNearResults = 50,
            };
        }

        /// <summary>
        /// Compare anchors by date time, and sort in descending order.
        /// </summary>
        private int CompareAnchorsByDateDescendingOrder(CloudSpatialAnchor anchor1, CloudSpatialAnchor anchor2)
        {
            if (anchor1 == null && anchor2 == null)
            {
                return 0;
            }
            else if (anchor2 == null)
            {
                return -1;
            }
            else if (anchor1 == null)
            {
                return 1;
            }
            else
            {
                DateTime anchor1Time = AppServices.AnchoringService.UpdateTime(anchor1);
                DateTime anchor2Time = AppServices.AnchoringService.UpdateTime(anchor2);
                if (anchor1Time == anchor2Time)
                {
                    return 0;
                }
                else if (anchor1Time > anchor2Time)
                {
                    return -1;
                }
                else
                {
                    return 1;
                }
            }
        }
        #endregion Private Functions
    }
}
