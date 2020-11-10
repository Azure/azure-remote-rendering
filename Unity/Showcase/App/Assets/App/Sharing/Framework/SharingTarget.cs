// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// An abstract component that wraps an ISharingServiceTarget model. This allows a game object to lazily set its
    /// ISharingServiceTarget, or even change its ISharingServiceTarget. If no ISharingServiceTarget is set once 
    /// OnEnable() is called, a new ISharingServiceTarget will be created. 
    /// </summary>
    public abstract class SharingTarget : MonoBehaviour
    {
        private int[] _address;
        private SharingTarget _root;
        private ISharingServiceTarget _innerTarget;
        private event Action<string, object> _propertyChanged;
        private event Action<ISharingServiceTarget, string, object> _childPropertyChanged;

        #region Serialized Fields
        [SerializeField]
        [Tooltip("The type used when synchronizing properties to other clients.")]
        private SharingServiceTargetType type;

        /// <summary>
        /// The type used when synchronizing properties to other clients. If this is not a root object, the type must be child.
        /// </summary>
        public SharingServiceTargetType Type
        {
            get => type;
            set
            {
                if (Type != type)
                {
                    if (InnerTarget == null)
                    {
                        type = IsRoot ? value : SharingServiceTargetType.Child;
                    }
                    else
                    {
                        Debug.LogError("Unable to change SharingId's type, as target has already been created.");
                    }
                }
            }
        }

        [SerializeField]
        [Tooltip("The label used when synchronizing properties to other clients.")]
        private string label;

        /// <summary>
        /// The label used when synchronizing properties to other clients.
        /// </summary>
        public string Label
        {
            get => label;
            set
            {
                if (InnerTarget == null)
                {
                    label = value;
                }
                else
                {
                    Debug.LogError("Unable to change SharingId's label, as target has already been created.");
                }
            }
        }
        #endregion Serialized Fields

        #region Public Propertie
        /// <summary>
        /// Get if this target is connected to the sharing service.
        /// </summary>
        public bool IsConnected => _innerTarget == null ? false : _innerTarget.IsConnected;
        #endregion Public Properties

        #region Public Events
        /// <summary>
        /// Event fired when a property changes.
        /// </summary>
        public event Action<string, object> PropertyChanged
        {
            add
            {
                _propertyChanged += value;
                ReplayPropertyChanges(value);
            }

            remove
            {
                _propertyChanged -= value;
            }
        }

        /// <summary>
        /// Event raised when a child property has changed.
        /// </summary>
        public event Action<ISharingServiceTarget, string, object> ChildPropertyChanged
        {
            add
            {
                _childPropertyChanged += value;
                ReplayChildPropertyChanges(_childPropertyChanged);
            }

            remove
            {
                _childPropertyChanged -= value;
            }
        }

        /// <summary>
        /// A specialized message optimized for sending a transform to a target.
        /// </summary>
        public event Action<SharingServiceTransform> TransformMessageReceived;

        /// <summary>
        /// A specialized message optimized for sending an array of floats to a target.
        /// </summary>
        public event Action<ISharingServiceTarget, SharingServiceTransform> ChildTransformMessageReceived;

        /// <summary>
        /// Event fired when a message is received from a remote client
        /// </summary>
        public event Action<ISharingServiceMessage> MessageReceived;

        /// <summary>
        /// Event fired when connection changes
        /// </summary>
        public event Action<bool> ConnectionChanged;
        #endregion Public Events

        #region Public Properties
        /// <summary>
        /// Get the inner target. Note, this can change during initialization so be careful when using this.
        /// </summary>
        public ISharingServiceTarget InnerTarget
        {
            get => _innerTarget;

            set
            {
                if (_innerTarget == value)
                {
                    return;
                }

                if (_innerTarget != null)
                {
                    _innerTarget.PropertyChanged -= InnerPropertyChanged;
                    _innerTarget.ChildPropertyChanged -= InnerChildPropertyChanged;
                    _innerTarget.TransformMessageReceived -= InnerTransformMessageReceived;
                    _innerTarget.ChildTransformMessageReceived -= ChildInnerTransformMessageReceived;
                    _innerTarget.MessageReceived -= InnerMessageReceived;
                    _innerTarget.ConnectionChanged -= InnerConnectionChanged;
                }

                _innerTarget = value;

                if (_innerTarget != null)
                {
                    _innerTarget.PropertyChanged += InnerPropertyChanged;
                    _innerTarget.ChildPropertyChanged += InnerChildPropertyChanged;
                    _innerTarget.TransformMessageReceived += InnerTransformMessageReceived;
                    _innerTarget.ChildTransformMessageReceived += ChildInnerTransformMessageReceived;
                    _innerTarget.MessageReceived += InnerMessageReceived;
                    _innerTarget.ConnectionChanged += InnerConnectionChanged;
                }

                ReplayPropertyChanges(_propertyChanged);
                ReplayChildPropertyChanges(_childPropertyChanged);
            }
        }

        /// <summary>
        /// Get if this is a root
        /// </summary>
        public virtual bool IsRoot => false;

        /// <summary>
        /// Get the root sharing target
        /// </summary>
        public SharingTarget Root
        {
            get
            {
                if (_root == null)
                {
                    _root = FindRootParent();

                    // Prevent FindRootParent() from being called again, set to non-null
                    if (_root == null)
                    {
                        _root = this;
                    }
                }

                return _root;
            }
        }

        /// <summary>
        /// Get the address of the target. If this is null, we're at the root.
        /// </summary>
        public int[] Address
        {
            get
            {
                if (_address == null)
                {
                    _address = CreateAddress();

                    // Create "empty" arrays for null, to avoid multiple calls to CreateAddress().
                    if (_address == null)
                    {
                        _address = new int[0];
                    }
                }
                return _address;
            }
        }

        /// <summary>
        /// Get the id used to share information on this object.
        /// </summary>
        public string SharingId
        {
            get
            {
                return InnerTarget?.SharingId;
            }
        }
        #endregion Public Properties

        #region MonoBehaviour Function
        /// <summary>
        /// Ensure the type is "child" if this is not a root object.
        /// </summary>
        private void OnValidate()
        {
            if (!IsRoot)
            {
                Type = SharingServiceTargetType.Child;
            }
        }

        /// <summary>
        /// Ensure the type is "child" if this is not a root object.
        /// </summary>
        private void Start()
        {
            if (!IsRoot)
            {
                Type = SharingServiceTargetType.Child;
            }
        }

        /// <summary>
        /// Initialize the sharing service target, if not done already.
        /// </summary>
        private void OnEnable()
        {
            EnsureInnerTarget();
        }

        /// <summary>
        /// Prevent the sharing target from being used once this is destroyed.
        /// </summary>
        private void OnDestroy()
        {
            InnerTarget = null;
        }
        #endregion MonoBehaviour Functions

        #region Public Functions
        /// <summary>
        /// Set a property on the given target to a praticular value.
        /// </summary>
        /// <param name="property">The property to set</param>
        /// <param name="value">The value to set.</param>
        public void SetProperty(string property, object value)
        {
            InnerTarget?.SetProperty(property, value);
        }

        /// <summary>
        /// Set a properties on the given target to a praticular value. Setting a value
        /// to null indicates that the property will be removed from the server.
        /// </summary>
        public void SetProperties(params object[] propertyNamesAndValues)
        {
            InnerTarget?.SetProperties(propertyNamesAndValues);
        }

        /// <summary>
        /// Clear all properties under this target.
        /// </summary>
        public void ClearProperties()
        {
            InnerTarget?.ClearProperties();
        }

        /// <summary>
        /// Try and get a property value of a given target.
        /// </summary>
        /// <param name="property">The property to get</param>
        /// <param name="value">The value of the property.</param>
        public bool TryGetProperty<T>(string property, out T value)
        {
            value = default;
            return InnerTarget?.TryGetProperty(property, out value) == true;
        }

        /// <summary>
        /// Does this target have the given property
        /// </summary>
        public bool HasProperty(string property)
        {
            return InnerTarget?.HasProperty(property) == true;
        }

        /// <summary>
        /// Send a command message to this target on other clients.
        /// </summary>
        public void SendCommandMessage(string command)
        {
            InnerTarget?.SendMessage(command);
        }

        /// <summary>
        /// Send a sharing service transform to this target on other clients.
        /// </summary>
        public void SendTransformMessage(SharingServiceTransform transform)
        {
            InnerTarget?.SendTransformMessage(transform);
        }
        #endregion Public Functions

        #region Protected Functions
        /// <summary>
        /// Create an address used to find a child target that is underneath a root target. If null or empty is returned,
        /// it is assumed that is a root target.
        /// </summary>
        protected abstract int[] CreateAddress();
        #endregion Protected Functions

        #region Private Functions
        /// <summary>
        /// Initialize the sharing service target, if not done already.
        /// </summary>
        private void EnsureInnerTarget()
        {
            if (InnerTarget == null)
            {
                if (Root == this)
                {
                    InnerTarget = AppServices.SharingService.CreateTarget(Type, Label);
                }
                else
                {
                    Debug.Assert(Address.Length > 0, $"Sharing target child '{name}' does not have an address. Sharing of data accross clients will not work.");
                    Root.EnsureInnerTarget();
                    InnerTarget = Root.InnerTarget.AddChild(Address);
                }
            }
        }

        /// <summary>
        /// The root sharing target for this child target. A reference to 'self' mabe returned if this is already a root.
        /// </summary>
        private SharingTarget FindRootParent()
        {
            SharingTarget sharingTarget = GetComponentInParent<SharingTargetRoot>();
            Debug.Assert(sharingTarget != null && sharingTarget.IsRoot, $"Unable to find a root target for '{name}'. Sharing of data accross clients will likely not work.");
            return sharingTarget;
        }

        /// <summary>
        /// Redirect message received events
        /// </summary>
        private void InnerMessageReceived(ISharingServiceTarget target, ISharingServiceMessage message)
        {
            MessageReceived?.Invoke(message);
        }

        /// <summary>
        /// Redirect property changed events
        /// </summary>
        private void InnerChildPropertyChanged(ISharingServiceTarget child, string property, object value)
        {
            _childPropertyChanged?.Invoke(child, property, value);
        }

        /// <summary>
        /// Redirect property changed events
        /// </summary>
        private void InnerPropertyChanged(ISharingServiceTarget sender, string property, object value)
        {
            _propertyChanged?.Invoke(property, value);
        }

        /// <summary>
        /// Redirect numeric event
        /// </summary>
        private void InnerTransformMessageReceived(SharingServiceTransform transform)
        {
            TransformMessageReceived?.Invoke(transform);
        }

        /// <summary>
        /// Redirect numeric event
        /// </summary>
        private void ChildInnerTransformMessageReceived(ISharingServiceTarget child, SharingServiceTransform transform)
        {
            ChildTransformMessageReceived?.Invoke(child, transform);
        }

        /// <summary>
        /// Replay all child property changes on the given delegate.
        /// </summary>
        private void ReplayPropertyChanges(Action<string, object> propertyChangeHandler)
        {
            if (InnerTarget == null || propertyChangeHandler == null)
            {
                return;
            }

            // The properties can change during handler callbacks, so copy dictionary first.
            var toReplay = new List<(string, object)>(InnerTarget.Properties.Count);

            foreach (var property in InnerTarget.Properties)
            {
                if (property.Value != null)
                {
                    toReplay.Add((property.Key, property.Value));
                }
            }

            foreach (var property in toReplay)
            { 
                propertyChangeHandler(property.Item1, property.Item2);
            }
        }

        /// <summary>
        /// Replay all child property changes on the given delegate.
        /// </summary>
        private void ReplayChildPropertyChanges(Action<ISharingServiceTarget, string, object> propertyChangeHandler)
        {
            if (InnerTarget == null || propertyChangeHandler == null)
            {
                return;
            }

            // The properties can change during handler callbacks, so copy dictionary first.
            var toReplay = new List<(ISharingServiceTarget, string, object)>(InnerTarget.Children.Count);

            foreach (var child in InnerTarget.Children)
            {
                foreach (var property in child.Properties)
                {
                    if (property.Value != null)
                    {
                        toReplay.Add((child, property.Key, property.Value));
                    }
                }
            }

            foreach (var property in toReplay)
            {
                propertyChangeHandler(property.Item1, property.Item2, property.Item3);
            }
        }

        /// <summary>
        /// Handle inner target's connection changing.
        /// </summary>
        private void InnerConnectionChanged(bool isConnected)
        {
            ConnectionChanged?.Invoke(isConnected);
        }
        #endregion Private Functions
    }
}

