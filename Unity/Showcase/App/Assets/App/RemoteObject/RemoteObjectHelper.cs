// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// Helpers to create remote object game objects.
    /// </summary>
    public static class RemoteObjectHelper
    {
        public static async Task<RemoteObject> Spawn(RemoteItemBase remoteData)
        {
            var stage = await AppServices.RemoteObjectStageService.GetRemoteStage();
            return await Spawn(remoteData, new RemoteObjectSpawnData()
            {
                Staged = stage.IsStageVisible
            });
        }

        public static async Task<RemoteObject> Spawn(RemoteItemBase remoteData, RemoteObjectSpawnData spawnData)
        {
            var original = Resources.Load<GameObject>("RemoteObject");

            GameObject sharedObject = null;
            if (original != null)
            {
                sharedObject = await AppServices.SharingService.SpawnTarget(original, data: new object[] { spawnData });
            }

            RemoteObject result = null;
            RemoteObjectReparent mover = null;
            if (sharedObject != null)
            {
                result = sharedObject.GetComponent<RemoteObject>();
                mover = sharedObject.GetComponent<RemoteObjectReparent>();
            }

            if (result != null)
            {
                result.Data = remoteData;
            }

            if (mover != null )
            {
                mover.Reparent(reposition: true, spawnData.Staged ? 
                    RemoteObjectReparent.OperationType.Staged : RemoteObjectReparent.OperationType.Unstaged);
            }

            return result;
        }
    }

    [Serializable]
    public struct RemoteObjectSpawnData
    {
        public bool Staged { get; set; }
    }

}
