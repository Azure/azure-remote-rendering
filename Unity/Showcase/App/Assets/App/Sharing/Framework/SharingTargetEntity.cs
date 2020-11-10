// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.Azure.RemoteRendering.Unity;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// A SharingTarget component that represents an Azure Remote Rendering Entity, and is capable of sharing state for
    /// this entity. For sharing to work properly, this component must be a child or grandchild of a game object containing
    /// the SharingTargetRoot component. That is, this component must be rooted somehow.
    /// </summary>
    public class SharingTargetEntity : SharingTarget
    {
        #region Public Properties
        /// <summary>
        /// Get if this is a root
        /// </summary>
        public override bool IsRoot => false;
        #endregion Public Properties

        #region Public Functions
        /// <summary>
        /// Resolve an ISharingServiceTarget to an Azure Remote Rendering Entity object. If a 'SharingTargetRoot' is
        /// provided, this will search the 'rootHint' for the remote Entity. If 'SharingTargetRoot' is not provided, 
        /// this will first search the 'target' for the nearest 'SharingTargetRoot', and then search for the Entity.
        /// </summary>
        public static Entity ResolveTarget(ISharingServiceTarget target, SharingTargetRoot rootHint = null)
        {
            if (target == null)
            {
                return null;
            }

            if ((rootHint == null) ||
                (rootHint.InnerTarget == null) ||
                (rootHint.InnerTarget != target.Root && rootHint.InnerTarget != target))
            {
                rootHint = FindRootTarget(target);
            }

            return FindEntity(rootHint, target);
        }

        /// <summary>
        /// Create a new ISharingServiceTarget from a given Azure Remote Rendering Entity object. 
        /// </summary>
        public static ISharingServiceTarget CreateTarget(Entity child)
        {
            SharingTargetRoot root = child?.GetExistingParentGameObject()?.GetComponentInParent<SharingTargetRoot>();
            return CreateTarget(root, child);
        }

        /// <summary>
        /// Create a new ISharingServiceTarget from a given Azure Remote Rendering Entity object that is child of the 
        /// given SharingTargetRoot. 
        /// </summary>
        public static ISharingServiceTarget CreateTarget(SharingTargetRoot root, Entity child)
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

            return root.InnerTarget.AddChild(CreateAddress(rootEntitySync.Entity, child));
        }
        #endregion Public Functions

        #region Protected Functions
        /// <summary>
        /// Create an address used to find a child target that is underneath a root target. If null or empty is returned,
        /// it is assumed that is a root target.
        /// </summary>
        protected override sealed int[] CreateAddress()
        {
            RemoteEntitySyncObject rootEntitySync = Root.GetComponentInChildren<RemoteEntitySyncObject>();
            RemoteEntitySyncObject thisEntitySync = this.GetComponent<RemoteEntitySyncObject>();
            return CreateAddress(rootEntitySync, thisEntitySync);
        }
        #endregion Protected Functions

        #region Private Functions
        /// <summary>
        /// Create an address used to find a child target that is underneath a root target. If null or empty is returned,
        /// it is assumed that is a root target.
        /// </summary>
        private static int[] CreateAddress(RemoteEntitySyncObject root, RemoteEntitySyncObject child)
        {
            if (root == null || !root.IsEntityValid)
            {
                Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, $"Can't create sharing address for child entity '{child?.Entity?.Name}'. Can't find a valid root RemoteEntitySyncObject.");
                return null;
            }
            else if (child == null || !child.IsEntityValid)
            {
                Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, $"Can't create sharing address for child entity '{child?.Entity?.Name}'. Can't find a valid RemoteEntitySyncObject for self.");
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
                if (currentEntity == null || !currentEntity.Valid)
                {
                    Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, $"Found a null or invalid entity when creating sharing address for entity '{childEntity?.Name}'.");
                    address.Clear();
                    break;
                }
                else if (currentEntity.Parent == null || !currentEntity.Parent.Valid)
                {
                    Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, $"Found a null or invalid entity parent when creating sharing address for entity '{childEntity?.Name}'.");
                    address.Clear();
                    break;
                }

                int index = IndexOfChild(currentEntity);
                if (index < 0)
                {
                    Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, $"Unable to find child index when creating sharing address for entity '{childEntity?.Name}'.");
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
        /// Given an ISharingServiceTarget, find the nearest SharingTargetRoot that has the target as a child.
        /// </summary>
        private static SharingTargetRoot FindRootTarget(ISharingServiceTarget target)
        {
            if (target == null)
            {
                return null;
            }

            SharingTargetRoot result = null;
            SharingTargetRoot[] roots = Component.FindObjectsOfType<SharingTargetRoot>();
            int rootsLength = roots.Length;
            for (int i = 0; i < rootsLength; i++)
            {
                SharingTargetRoot current = roots[i];
                if ((current.InnerTarget != null) &&
                    (current.InnerTarget == target || current.InnerTarget == target.Root))
                {
                    result = current;
                    break;
                }
            }
            return result;
        }

        /// <summary>
        /// Resolve an ISharingServiceTarget to an Azure Remote Rendering Entity object. This will search the 'root' for the remote Entity.
        /// </summary>
        private static Entity FindEntity(SharingTargetRoot root, ISharingServiceTarget target)
        {
            RemoteEntitySyncObject rootEntitySync = root?.GetComponentInChildren<RemoteEntitySyncObject>();
            if (rootEntitySync == null || !rootEntitySync.IsEntityValid)
            {
                Debug.LogError($"Can't find sharing target off of '{root?.name ?? "NULL"}'. Can't find a valid root RemoteEntitySyncObject.");
                return null;
            }

            Entity parentEntity = rootEntitySync.Entity;
            Entity resultEntity = parentEntity;
            int[] childIndices = target.Address;
            int childIndicesCount = childIndices?.Length ?? 0;

            for (int i = 0; i < childIndicesCount; i++)
            {
                if (parentEntity == null)
                {
                    Debug.LogError($"Can't find sharing target off of '{root.name}'. The hierarchy was too shallow.");
                    resultEntity = null;
                    break;
                }

                int index = childIndices[i];
                if (parentEntity.Children.Count <= index)
                {
                    Debug.LogError($"Can't find sharing target off of '{root.name}'. The a parent didn't have enough children. Was excepted a child at index '{index}'");
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

