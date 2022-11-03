// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// A helper to spawn SharingObject prefabs while offline
    /// </summary>
    public class OfflineSpawner
    {
        private object _lock = new object();
        private int _objectId = -1;

        #region Public Functions
        /// <summary>
        /// Spawn a network object that is only available on this client.
        /// </summary>
        public Task<GameObject> SpawnTarget(GameObject original, object[] data)
        {
            var result = UnityEngine.Object.Instantiate(original);
            var sharingObjects = result.GetComponentsInChildren<SharingObject>(includeInactive: true);
            if (sharingObjects != null)
            {
                foreach (var sharingObject in sharingObjects)
                {
                    sharingObject.Initialize(AppServices.SharingService.CreateTarget(sharingObject.Type, NextObjectId().ToString()));
                    var initializer = AppServices.SharingService as ISharingServiceObjectInitializer;
                    initializer?.InitializeSharingObject(sharingObject, data);
                }
            }

            return Task.FromResult(result);
        }

        /// <summary>
        /// Despawn a network object that is shared across all clients
        /// </summary>
        public Task DespawnTarget(GameObject gameObject)
        {
            if (gameObject != null && gameObject.GetComponent<SharingObject>() != null)
            {
                UnityEngine.Object.Destroy(gameObject);
            }
            return Task.CompletedTask;
        }
        #endregion Public Functions

        #region Private Functions
        /// <summary>
        /// Get the next valid object id
        /// </summary>
        private int NextObjectId()
        {
            bool initializeObjectId = false;
            lock (_lock)
            {
                initializeObjectId = _objectId < 1;
            }

            if (initializeObjectId)
            {
                InitializeObjectId();
            }

            int id = 0;
            lock (_lock)
            {
                id = _objectId++;
            }
            return id;
        }

        /// <summary>
        /// Initialize the object id to the next valid index.
        /// </summary>
        private void InitializeObjectId()
        {
            int maxExistingId = 1;
            var sharingObjects = UnityEngine.Object.FindObjectsOfType<SharingObject>(includeInactive: true);
            foreach (var sharingObject in sharingObjects)
            {
                int existingId;
                if (int.TryParse(sharingObject.Label, out existingId))
                {
                    maxExistingId = Math.Max(maxExistingId, existingId + 1);
                }
            }

            lock (_lock)
            {
                _objectId = Math.Max(maxExistingId, maxExistingId);
            }
        }
        #endregion Private Functions
    }
}
