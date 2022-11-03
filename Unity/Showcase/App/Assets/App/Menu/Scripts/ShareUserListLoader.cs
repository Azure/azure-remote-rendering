// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication;

/// <summary>
/// A class for helping to load filterable users
/// </summary>
public class ShareUserListLoader : ListFilterableDataLoader
{
    #region MonoBehavior Functions
    /// <summary>
    /// Load the last filter request
    /// </summary>
    protected override void OnEnable()
    {
        base.OnEnable();

        AppServices.SharingService.CurrentRoomChanged += OnCurrentRoomChanged;
        OnCurrentRoomChanged(null, AppServices.SharingService.CurrentRoom);
    }

    /// <summary>
    /// Cancel the current filter request
    /// </summary>
    protected override void OnDisable()
    {
        base.OnDisable();

        AppServices.SharingService.CurrentRoomChanged -= OnCurrentRoomChanged;
    }
    #endregion MonoBehavior Functions

    #region Private Functions
    private void OnCurrentRoomChanged(ISharingService sender, ISharingServiceRoom room)
    {
        if (room == null)
        {
            FilterableDataSource = null;
        }
        else if (FilterableDataSource == null)
        {
            FilterableDataSource = new FindPlayersDataSource();
            Load();
        }
    }
    #endregion Private Functions
}
