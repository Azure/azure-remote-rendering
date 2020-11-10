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
            GameObject newObject;
            if (containerPrefab == null)
            {
                newObject = new GameObject();
                // avoid scripts from running when adding to object
                newObject.SetActive(false);
            }
            else
            {
                // avoid scripts from running right when initantiated and adding to object
                containerPrefab.SetActive(false);
                newObject = GameObject.Instantiate(containerPrefab);
            }
            newObject.transform.SetParent(parent, true);

            var remoteObject = newObject.EnsureComponent<RemoteObject>();
            remoteObject.Data = remoteData;

            initailizeAction?.Invoke(remoteObject);
            newObject.SetActive(true);

            return remoteObject;
        }
    }
}
