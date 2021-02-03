// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.Azure.RemoteRendering.Unity;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Wraps the Azure Remote Rendering RayCast queries to easily send requests using Unity data types
/// </summary>
public class RemoteRayCaster
{
    public static double maxDistance = 30.0;

    public static async Task<RayCastHit[]> RemoteRayCast(Vector3 origin, Vector3 dir, HitCollectionPolicy hitPolicy = HitCollectionPolicy.ClosestHit)
    {
        if (RemoteRenderingCoordinator.instance.CurrentCoordinatorState == RemoteRenderingCoordinator.RemoteRenderingState.RuntimeConnected)
        {
            var rayCast = new RayCast(origin.toRemotePos(), dir.toRemoteDir(), maxDistance, hitPolicy);
            var result = await RemoteRenderingCoordinator.CurrentSession.Connection.RayCastQueryAsync(rayCast);
            return result.Hits;
        }
        else
        {
            return new RayCastHit[0];
        }
    }

    public static async Task<Entity[]> RemoteRayCastEntities(Vector3 origin, Vector3 dir, HitCollectionPolicy hitPolicy = HitCollectionPolicy.ClosestHit)
    {
        var hits = await RemoteRayCast(origin, dir, hitPolicy);
        return hits.Select(hit => hit.HitEntity).Where(entity => entity != null).ToArray();
    }
}
