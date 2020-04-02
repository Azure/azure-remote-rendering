// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// Helpers to create remote object game objects.
    /// </summary>
    public static class RemoteObjectHelper
    {
        public static RemoteObject Load(
            RemoteItemBase remoteData, 
            GameObject containerPrefab, 
            Transform parent = null, 
            Action<RemoteObject> initailizeAction = null)
        {
            // The RemoteObject will manage getting the "primary machine"
            return Load(null, remoteData, containerPrefab, parent, initailizeAction);
        }

        public static RemoteObject Load(
            IRemoteRenderingMachine machine,
            RemoteItemBase remoteData,
            GameObject containerPrefab, 
            Transform parent = null, 
            Action<RemoteObject> initailizeAction = null)
        {
            if (remoteData == null)
            {
                return null;
            }

            var newObject = containerPrefab == null ? new GameObject() : GameObject.Instantiate(containerPrefab);
            newObject.SetActive(false);
            newObject.transform.SetParent(parent, false);

            var remoteObject = newObject.EnsureComponent<RemoteObject>();
            remoteObject.PrimaryMachine = machine;
            remoteObject.Data = remoteData;

            initailizeAction?.Invoke(remoteObject);
            newObject.SetActive(true);

            return remoteObject;
        }
    }
}
