// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class ShareRoomListLoader : ListDataLoaderBase
{
    #region MonoBehaviour Functions
    private void Start()
    {
        AppServices.SharingService.RoomsChanged += OnRoomsChanged;
    }

    private void OnDestroy()
    {
        AppServices.SharingService.RoomsChanged -= OnRoomsChanged;
    }
    #endregion MonoBehaviour Functions
    
    #region Protected Functions
    /// <summary>
    /// Get the data that will be put into the target list.
    /// </summary>
    protected override async Task<IList<object>> GetData(CancellationToken cancellation)
    {
        // only load data if share menu is selected
        var menu = AppServices.AppSettingsService.GetMainMenu<HandMenuHooks>();
        if (menu == null || menu.State != HandMenuHooks.MenuState.Share)
        {
            return null;
        }

        var rooms = await AppServices.SharingService.UpdateRooms();
        int count = rooms?.Count ?? 0;
        IList<object> objectData = new List<object>(count + 1);

        if (count > 0)
        {
            foreach (var room in rooms)
            {
                if (room != null)
                {
                    objectData.Add(room);
                }
            }
        }

        return objectData;
    }
    #endregion Protected Functions

    #region Private Functions
    private void OnRoomsChanged(ISharingService sender, IReadOnlyCollection<ISharingServiceRoom> rooms)
    {
        if (rooms == null)
        {
            SetData(null);
        }
        else
        {
            var copy = new List<object>(rooms.Count);
            foreach (var room in rooms)
            {
                copy.Add(room);
            }
            SetData(copy);
        }
    }
    #endregion Private Functions
}

