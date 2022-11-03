// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.Azure.RemoteRendering.Unity;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// A ISharingServiceObject component that represents an Azure Remote Rendering Entity, and is capable of sharing state for
    /// this entity. For sharing to work properly, this component must be a child or grandchild of a game object containing
    /// the SharingObject component. That is, this component must be rooted somehow.
    /// </summary>
    public class SharingObjectRemoteEntity : SharingObjectBase
    {
        private static LogHelper<SharingObjectRemoteEntity> _log = new LogHelper<SharingObjectRemoteEntity>();

        #region Public Properties
        /// <summary>
        /// Get if this is a root
        /// </summary>
        public override bool IsRoot => false;
        #endregion Public Properties

        #region MonoBehavior Functions
        private void Awake()
        {
            Initialize();
        }
        #endregion MonoBehavior Function

        #region Public Functions
        /// <summary>
        /// Resolve an ISharingServiceObject to an Azure Remote Rendering Entity object. If a 'SharingObject' is
        /// provided, this will search the 'rootHint' for the remote Entity. If 'SharingObject' is not provided, 
        /// this will first search the 'target' for the nearest 'SharingObject', and then search for the Entity.
        /// </summary>
        public static Entity ResolveSharingObject(ISharingServiceObject target, SharingObject rootHint = null)
        {
            if (target == null)
            {
                return null;
            }

            if ((rootHint == null) ||
                (rootHint.Inner == null) ||
                (rootHint.Inner != target.Root && rootHint.Inner != target))
            {
                rootHint = FindRootSharingObject(target);
            }

            return FindEntity(rootHint, target);
        }

        /// <summary>
        /// Create a new ISharingServiceObject from a given Azure Remote Rendering Entity object. 
        /// </summary>
        public static ISharingServiceObject CreateTarget(Entity child)
        {
            SharingObject root = child?.GetExistingParentGameObject()?.GetComponentInParent<SharingObject>();
            return CreateSharingObject(root, child);
        }

        /// <summary>
        /// Create a new ISharingServiceObject from a given Azure Remote Rendering Entity object that is child of the 
        /// given SharingObject. 
        /// </summary>
        public static ISharingServiceObject CreateSharingObject(SharingObject root, Entity child)
        {
            if (root == null || child == null || !child.Valid)
            {
                return null;
            }

            RemoteEntitySyncObject rootEntitySync = root.GetComponentInChildren<RemoteEntitySyncObject>();
            if (rootEntitySync == null || !rootEntitySync.IsEntityValid)
            {
                return null;
            }

            return root.Inner.AddChild(CreateAddress(rootEntitySync.Entity, child));
        }
        #endregion Public Functions

        #region Private Functions
        /// <summary>
        /// Initialize the sharing service target, if this is not a root.
        /// If this is a Root, the SharingService must initialize this object
        /// </summary>
        private void Initialize()
        {
            if (Inner == null)
            {
                // This must always be a child
                Type = SharingServiceObjectType.Child;

                var address = CreateAddress();
                _log.LogAssert(address != null && address.Length > 0, $"Sharing target child '{name}' does not have an address. Sharing of data accross clients will not work.");
                Initialize(Root.AddChild(address));
            }
        }

        /// <summary>
        /// Create an address used to find a child target that is underneath a root target. If null or empty is returned,
        /// it is assumed that is a root target.
        /// </summary>
        private int[] CreateAddress()
        {
            var rootBehavior = Root as MonoBehaviour;
            if (rootBehavior == null)
            {
                return null;
            }
            else
            {
                RemoteEntitySyncObject rootEntitySync = rootBehavior.GetComponentInChildren<RemoteEntitySyncObject>();
                RemoteEntitySyncObject thisEntitySync = this.GetComponent<RemoteEntitySyncObject>();
                return CreateAddress(rootEntitySync, thisEntitySync);
            }
        }

        /// <summary>
        /// Create an address used to find a child target that is underneath a root target. If null or empty is returned,
        /// it is assumed that is a root target.
        /// </summary>
        private static int[] CreateAddress(RemoteEntitySyncObject root, RemoteEntitySyncObject child)
        {
            if (root == null || !root.IsEntityValid)
            {
                _log.LogError("Can't create sharing address for child entity '{0}'. Can't find a valid root RemoteEntitySyncObject.", child?.Entity?.Name);
                return null;
            }
            else if (child == null || !child.IsEntityValid)
            {
                _log.LogError("Can't create sharing address for child entity '{0}'. Can't find a valid RemoteEntitySyncObject for self.", child?.Entity?.Name);
                return null;
            }
            else
            {
                return CreateAddress(root.Entity, child.Entity);
            }
        }

        /// <summary>
        /// Create an address used to find a child target that is underneath a root target. If null or empty is returned,
        /// it is assumed that is a root target.
        /// </summary>
        private static int[] CreateAddress(Entity rootEntity, Entity childEntity)
        {
            List<int> address = new List<int>();
            Entity currentEntity = childEntity;
            while (currentEntity != rootEntity)
            {
                if (currentEntity == null)
                {
                    _log.LogError("Found a null entity when creating sharing address for entity '{0}'.", childEntity?.Name);
                    address.Clear();
                    break;
                }
                else if (!currentEntity.Valid)
                {
                    _log.LogError("Found an invalid entity when creating sharing address for entity '{0}'.", childEntity?.Name);
                    address.Clear();
                    break;
                }
                else if (currentEntity.Parent == null)
                {
                    _log.LogError("Found a null entity parent when creating sharing address for entity '{0}'.", childEntity?.Name);
                    address.Clear();
                    break;
                }
                else if (currentEntity.Parent == null || !currentEntity.Parent.Valid)
                {
                    _log.LogError("Found an invalid entity parent when creating sharing address for entity '{0}'.", childEntity?.Name);
                    address.Clear();
                    break;
                }

                int index = IndexOfChild(currentEntity);
                if (index < 0)
                {
                    _log.LogError("Unable to find child index when creating sharing address for entity '{0}'.", childEntity?.Name);
                    address.Clear();
                    break;
                }

                address.Insert(0, index);
                currentEntity = currentEntity.Parent;
            }

            return address.ToArray();
        }

        /// <summary>
        /// Get the child index of the given child.
        /// </summary>
        private static int IndexOfChild(Entity child)
        {
            int index = -1;
            foreach (var current in child.Parent.Children)
            {
                index++;
                if (current == child)
                {
                    break;
                }
            }
            return index;
        }

        /// <summary>
        /// Given an ISharingServiceObject, find the nearest SharingObject that has the target as a child.
        /// </summary>
        private static SharingObject FindRootSharingObject(ISharingServiceObject target)
        {
            if (target == null)
            {
                return null;
            }

            SharingObject result = null;
            SharingObject[] roots = Component.FindObjectsOfType<SharingObject>();
            int rootsLength = roots.Length;
            for (int i = 0; i < rootsLength; i++)
            {
                SharingObject current = roots[i];
                if ((current.Inner != null) &&
                    (current.Inner == target || current.Inner == target.Root))
                {
                    result = current;
                    break;
                }
            }
            return result;
        }

        /// <summary>
        /// Resolve an ISharingServiceObject to an Azure Remote Rendering Entity object. This will search the 'root' for the remote Entity.
        /// </summary>
        private static Entity FindEntity(SharingObject root, ISharingServiceObject sharingObject)
        {
            RemoteEntitySyncObject rootEntitySync = root?.GetComponentInChildren<RemoteEntitySyncObject>();
            if (rootEntitySync == null)
            {
                _log.LogError("Can't find remote enity on game object '{0}'. The root RemoteEntitySyncObject is null ({1}).", root?.name, sharingObject?.SharingId);
                return null;
            }
            else if (!rootEntitySync.IsEntityValid)
            {
                _log.LogError("Can't find remote enity on game object '{0}'. The root RemoteEntitySyncObject is invalid ({1}).", root?.name, sharingObject?.SharingId);
                return null;
            }

            Entity parentEntity = rootEntitySync.Entity;
            Entity resultEntity = parentEntity;
            int[] childIndices = sharingObject.Address;
            int childIndicesCount = childIndices?.Length ?? 0;

            for (int i = 0; i < childIndicesCount; i++)
            {
                if (parentEntity == null)
                {
                    _log.LogError("Can't find remote enity on game object '{0}'. The hierarchy was too shallow ({1}).", root?.name, sharingObject?.SharingId);
                    resultEntity = null;
                    break;
                }

                int index = childIndices[i];
                if (parentEntity.Children.Count <= index)
                {
                    _log.LogError("Can't find remote enity on game object '{0}'. The a parent didn't have enough children. Was excepting a child at index '{1}' ({2})", root?.name, index, sharingObject?.SharingId);
                    resultEntity = null;
                    break;
                }

                resultEntity = parentEntity.Children[index];
                parentEntity = resultEntity;
            }           

            return resultEntity;

        }
        #endregion Private Functions
    }
}

