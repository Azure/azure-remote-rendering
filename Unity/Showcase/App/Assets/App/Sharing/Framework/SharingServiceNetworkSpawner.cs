// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// Spawn the given objects when connected to a sharing session, and destroy when disconnected.
    /// </summary>
    public class SharingServiceNetworkSpawner : MonoBehaviour
    {
        private List<GameObject> _spawned = null;
        private bool _spawnPending = false;

        #region Serialized Fields
        [SerializeField]
        [Tooltip("Spawn these objects when a sharing session is joined.")]
        private GameObject[] spawn = new GameObject[0];

        /// <summary>
        /// Spawn these objects when a sharing session is joined.
        /// </summary>
        public GameObject[] Spawn
        {
            get => spawn;
            set => spawn = value;
        }

        [SerializeField]
        [Tooltip("Should game objects be auto spawned on connection.")]
        private bool autoSpawn = false;

        /// <summary>
        /// Should game objects be auto spawned on connection.
        /// </summary>
        public bool AutoSpawn
        {
            get => autoSpawn;
            set => autoSpawn = value;
        }
        #endregion Serialized Fields

        #region MonoBehavior Functions
        private void OnEnable()
        {
            AppServices.SharingService.Connected += OnSharingServiceConnected;

            if (autoSpawn)
            {
                TrySpawning();
            }
        }

        private void OnDisable()
        {
            AppServices.SharingService.Connected -= OnSharingServiceConnected;
        }
        #endregion MonoBehavior Functions

        #region Public Functions
        public async void TrySpawning()
        {
            if (_spawned == null && spawn != null)
            {
                if (AppServices.SharingService.IsConnected)
                {
                    _spawnPending = false;

                    var spawned = _spawned = new List<GameObject>(spawn.Length);
                    List<Task<GameObject>> spawning = new List<Task<GameObject>>(spawn.Length);

                    foreach (var entry in spawn)
                    {
                        spawning.Add(AppServices.SharingService.SpawnTarget(entry));
                    }

                    var spawningResult = await Task.WhenAll(spawning);
                    if (_spawned == spawned)
                    {
                        _spawned.AddRange(spawningResult);
                    }
                }
                else
                {
                    _spawnPending = true;
                }
            }
        }

        public void DestroySpawned()
        {
            var oldSpawned = _spawned;

            _spawnPending = false;
            _spawned = null;

            DestroySpawned(oldSpawned);
        }
        #endregion Public Functions

        #region Private Functions
        private void OnSharingServiceConnected(ISharingService obj)
        {
            if (_spawnPending)
            {
                TrySpawning();
            }
        }

        private static void DestroySpawned(List<GameObject> spawned)
        {
            if (spawned != null)
            {
                foreach (var entry in spawned)
                {
                    if (entry != null)
                    {
                        Destroy(entry);
                    }
                }
            }
        }    
        #endregion Private Functions
    }
}
