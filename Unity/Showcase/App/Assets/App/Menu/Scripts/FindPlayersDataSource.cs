// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A filtered data source for finding sharing service users.
/// </summary>
public class FindPlayersDataSource : IFilterableDataSource
{
    public async Task<IList<object>> Filter(string value, CancellationToken cancellationToken)
    {
        if (AppServices.SharingService == null)
        {
            return new List<object>();
        }
        else
        {
            var players = await AppServices.SharingService.FindPlayers(value, cancellationToken);
            return new List<object>(players.Select(inner => inner as object));
        }
    }    
}
