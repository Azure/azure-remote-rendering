// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// An abstract component that wraps an ISharingServiceObject. This allows a game object to lazily set its
    /// inner ISharingServiceObject.
    /// </summary>
    public abstract class SharingObjectBase : MonoBehaviour, ISharingServiceObject
    {
        private SharingObjectBase _root;
        private ISharingServiceObject _inner;
        private bool _despawing = false;
        private event Action<ISharingServiceObject, string, object> _propertyChanged;
        private event Action<ISharingServiceObject, string, object> _childPropertyChanged;

        #region Serialized Fields
        [Header("Settings")]

        [SerializeField]
        [Tooltip("The type used when synchronizing properties to other clients.")]
        private SharingServiceObjectType type;

        [SerializeField]
        [Tooltip("The label used when synchronizing properties to other clients.")]
        private string label;
        #endregion Serialized Fields

        #region ISharingServiceObject Properties
        /// <summary>
        /// Get the type of the sharing object
        /// </summary>
        public SharingServiceObjectType Type
        {
            get => _inner == null ? type : _inner.Type;

            set
            {
                if (_inner == null)
                {
                    type = value;
                }
            }
        }

        /// <summary>
        /// Get the label (or ID) of the sharing object
        /// </summary>
        public string Label
        {
            get => _inner == null ? label : _inner.Label;

            set
            {
                if (_inner == null)
                {
                    label = value;
                }
            }
        }

        /// <summary>
        /// A target can have multiple objects inside it. This address represents a child object in the target object.
        /// If this is null, then this is the root target.
        /// </summary>
        public int[] Address => _inner == null ? null : _inner.Address;

        /// <summary>
        /// Get the id used to share data about this object.
        /// </summary>
        public string SharingId => _inner == null ? null : _inner.SharingId;

        /// <summary>
        /// Is this the root target
        /// </summary>
        public virtual bool IsRoot => _inner == null ? false : _inner.IsRoot;

        /// <summary>
        /// Get the room that this object was created in.
        /// </summary>
        public ISharingServiceRoom Room => _inner == null ? null : _inner.Room;

        /// <summary>
        /// The root target of this child target.
        /// </summary>
        public ISharingServiceObject Root
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
        /// Get the current properties.
        /// </summary>
        public IReadOnlyList<ISharingServiceObject> Children => _inner?.Children;

        /// <summary>
        /// Get the current properties.
        /// </summary>
        public IReadOnlyDictionary<string, object> Properties => _inner?.Properties;
        #endregion ISharingServiceObject Properties

        #region ISharingServiceObject Events
        /// <summary>
        /// Event fired when a property changes.
        /// </summary>
        public event Action<ISharingServiceObject, string, object> PropertyChanged
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
        public event Action<ISharingServiceObject, string, object> ChildPropertyChanged
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
        /// Event fired when a message is received from a remote client
        /// </summary>
        public event Action<ISharingServiceObject, ISharingServiceMessage> MessageReceived;

        /// <summary>
        /// Event fired when connection changes
        /// </summary>
        public event Action<bool> ConnectionChanged;

        /// <summary>
        /// A specialized message optimized for sending an array of floats to a target.
        /// </summary>
        public event Action<SharingServiceTransform> TransformMessageReceived;

        /// <summary>
        /// A specialized message optimized for sending an array of floats to a target.
        /// </summary>
        public event Action<ISharingServiceObject, SharingServiceTransform> ChildTransformMessageReceived;

        /// <summary>
        /// Event fired when a despawn of this object is requested.
        /// </summary>
        public event Action<ISharingServiceObject> DespawnRequested;
        #endregion ISharingServiceObject Event

        #region ISharingServiceObject Functions
        /// <summary>
        /// Get the target object for the given child address.
        /// </summary>
        public ISharingServiceObject AddChild(int[] address)
        {
            return _inner?.AddChild(address);
        }

        /// <summary>
        /// Escape property names so that property name can be shared.
        /// </summary>
        public string EncodePropertyName(string property)
        {
            return _inner?.EncodePropertyName(property);
        }

        /// <summary>
        /// Set a property on the given target to a praticular value.
        /// </summary>
        /// <param name="property">The property to set</param>
        /// <param name="value">The value to set.</param>
        public void SetProperty(string property, object value)
        {
            _inner?.SetProperty(property, value);
        }

        /// <summary>
        /// Set a properties on the given target to a praticular value. Setting a value
        /// to null indicates that the property will be removed from the server.
        /// </summary>
        public void SetProperties(params object[] propertyNamesAndValues)
        {
            _inner?.SetProperties(propertyNamesAndValues);
        }

        /// <summary>
        /// Try and get a property value of a given target.
        /// </summary>
        /// <param name="property">The property to get</param>
        /// <param name="value">The value of the property.</param>
        public bool TryGetProperty(string property, out object value)
        {
            value = default;
            return _inner?.TryGetProperty(property, out value) == true;
        }

        /// <summary>
        /// Try and get a property value of a given target.
        /// </summary>
        /// <param name="property">The property to get</param>
        /// <param name="value">The value of the property.</param>
        public bool TryGetProperty<T>(string property, out T value)
        {
            value = default;
            return _inner?.TryGetProperty<T>(property, out value) == true;
        }

        /// <summary>
        /// Does this target have the given property
        /// </summary>
        public bool HasProperty(string property)
        {
            return _inner?.HasProperty(property) == true;
        }

        /// <summary>
        /// Clear all properties under this target.
        /// </summary>
        public void ClearProperties()
        {
            _inner?.ClearProperties();
        }

        /// <summary>
        /// Send a command message to this target on other clients.
        /// </summary>
        public void SendCommandMessage(string command)
        {
            _inner?.SendCommandMessage(command);
        }

        /// <summary>
        /// Send a transform message to this target on other clients.
        /// </summary>
        public void SendTransformMessage(SharingServiceTransform transform)
        {
            _inner?.SendTransformMessage(transform);
        }

        /// <summary>
        /// Request a despawn of this object.
        /// </summary>
        public void Despawn(GameObject target)
        {
            if (target == null || transform == null)
            {
                return;
            }

            if (target == gameObject || target.transform.IsChildOf(transform))
            {
                DespawnRequested?.Invoke(this);
                _inner?.Despawn(gameObject);
            }
        }
        #endregion ISharingServiceObject Functions

        #region Public Properties
        /// <summary>
        /// Get the inner target. Note, this can change during initialization so be careful when using this.
        /// </summary>
        public ISharingServiceObject Inner => _inner;
        #endregion Public Properties

        #region MonoBehaviour Function
        /// <summary>
        /// Prevent the sharing object from being used once this is destroyed.
        /// </summary>
        private void OnDestroy()
        {
            UnregisterInnerEvents();
            _inner = null;
        }
        #endregion MonoBehaviour Functions

        #region Public Functions
        /// <summary>
        /// Initialize the inner data. Any second attempt to initialize is ignored.
        /// </summary>
        public void Initialize(ISharingServiceObject inner)
        {
            if (_inner != null || inner == null)
            {
                return;
            }

            _inner = inner;
            RegisterInnerEvents();
            ReplayPropertyChanges(_propertyChanged);
            ReplayChildPropertyChanges(_childPropertyChanged);
        }

        /// <summary>
        /// Request a despawn of this object.
        /// </summary>
        public void Despawn()
        {
            Despawn(gameObject);
        }

        /// <summary>
        /// Start despawning after a brief delay.
        /// </summary>
        public void DelayDespawn()
        {
            if (!_despawing)
            {
                _despawing = true;
                StartCoroutine(DelayDespawnWorker());
            }
        }
        #endregion Public Functions

        #region Protected Functions
        /// <summary>
        /// Handle property changes.
        /// </summary>
        protected virtual void OnPropertyChanged(string property, object value)
        {
        }

        /// <summary>
        /// Handle property changes.
        /// </summary>
        protected virtual void OnChildPropertyChanged(ISharingServiceObject child, string property, object value)
        {
        }
        #endregion Protected Functions

        #region Private Functions
        /// <summary>
        /// Delay a despawn after a brief pause.
        /// </summary>
        private IEnumerator DelayDespawnWorker()
        {
            yield return new WaitForSeconds(0.5f);
            Despawn();
            _despawing = false;
        }

        /// <summary>
        /// Register for the inner target
        /// </summary>
        private void RegisterInnerEvents()
        {
            if (_inner != null)
            {
                _inner.PropertyChanged += InnerPropertyChanged;
                _inner.ChildPropertyChanged += InnerChildPropertyChanged;
                _inner.TransformMessageReceived += InnerTransformMessageReceived;
                _inner.ChildTransformMessageReceived += ChildInnerTransformMessageReceived;
                _inner.MessageReceived += InnerMessageReceived;
                _inner.ConnectionChanged += InnerConnectionChanged;
            }
        }

        /// <summary>
        /// Unregister for the inner target
        /// </summary>
        private void UnregisterInnerEvents()
        {
            if (_inner != null)
            {
                _inner.PropertyChanged -= InnerPropertyChanged;
                _inner.ChildPropertyChanged -= InnerChildPropertyChanged;
                _inner.TransformMessageReceived -= InnerTransformMessageReceived;
                _inner.ChildTransformMessageReceived -= ChildInnerTransformMessageReceived;
                _inner.MessageReceived -= InnerMessageReceived;
                _inner.ConnectionChanged -= InnerConnectionChanged;
            }
        }

        /// <summary>
        /// The root sharing object for this child target. A reference to 'self' mabe returned if this is already a root.
        /// </summary>
        private SharingObjectBase FindRootParent()
        {
            SharingObjectBase sharingObject = GetComponentInParent<SharingObject>();
            Debug.Assert(sharingObject != null && sharingObject.IsRoot, $"Unable to find a root target for '{name}'. Sharing of data accross clients will likely not work.");
            return sharingObject;
        }

        /// <summary>
        /// Redirect message received events
        /// </summary>
        private void InnerMessageReceived(ISharingServiceObject target, ISharingServiceMessage message)
        {
            MessageReceived?.Invoke(this, message);
        }

        /// <summary>
        /// Redirect property changed events
        /// </summary>
        private void InnerChildPropertyChanged(ISharingServiceObject child, string property, object value)
        {
            OnChildPropertyChanged(child, property, value);
            _childPropertyChanged?.Invoke(child, property, value);
        }

        /// <summary>
        /// Redirect property changed events
        /// </summary>
        private void InnerPropertyChanged(ISharingServiceObject sender, string property, object value)
        {
            OnPropertyChanged(property, value);
            _propertyChanged?.Invoke(this, property, value);
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
        private void ChildInnerTransformMessageReceived(ISharingServiceObject child, SharingServiceTransform transform)
        {
            ChildTransformMessageReceived?.Invoke(child, transform);
        }

        /// <summary>
        /// Replay all child property changes on the given delegate.
        /// </summary>
        private void ReplayPropertyChanges(Action<ISharingServiceObject, string, object> propertyChangeHandler)
        {
            if (Inner == null || propertyChangeHandler == null)
            {
                return;
            }

            // The properties can change during handler callbacks, so copy dictionary first.
            var toReplay = new List<(string, object)>(Inner.Properties.Count);

            foreach (var property in Inner.Properties)
            {
                if (property.Value != null)
                {
                    toReplay.Add((property.Key, property.Value));
                }
            }

            foreach (var property in toReplay)
            { 
                propertyChangeHandler(this, property.Item1, property.Item2);
            }
        }

        /// <summary>
        /// Replay all child property changes on the given delegate.
        /// </summary>
        private void ReplayChildPropertyChanges(Action<ISharingServiceObject, string, object> propertyChangeHandler)
        {
            if (Inner == null || propertyChangeHandler == null)
            {
                return;
            }

            // The properties can change during handler callbacks, so copy dictionary first.
            var toReplay = new List<(ISharingServiceObject, string, object)>(Inner.Children.Count);

            foreach (var child in Inner.Children)
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

