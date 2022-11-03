// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// This is a sharing service object. This is used by the sharing service to direct events and property changes 
    /// to a particular game object within the game's scene. 
    ///
    /// A sharing service object can have "children" or "addressable parts" (see the 'Children' and 'Address' properties).
    /// If a sharing ID has an child address, the event or property change for that ID will be redirected to the root's 
    /// child. If as sharing ID has no child address, the event or property change will be handled by the root.
    ///
    /// Here is the sharing ID's format. Note that 'propertyName' is required by property changes, and is not used
    /// with other type of events:
    /// 
    /// parentType.parentLabel[.childAddress][.propertyName]
    ///
    /// Event ID Examples:
    ///
    /// (Example 1) This ID points to a root target of type 'SharingServiceObjectType.Dynamic' (int value = 1).
    ///     1.cae4870f-27a2-4236-b0c1-1d0012f9458a
    ///
    /// (Example 2) This ID points to a child (2.0.1) on a 'SharingServiceObjectType.Dynamic' target (int value = 1):
    ///     1.cae4870f-27a2-4236-b0c1-1d0012f9458a.2.0.1
    ///
    /// Property Change ID Examples:
    ///
    /// (Example 3) This ID points to the 'anchor' property on a root target of type 'SharingServiceObjectType.Dynamic':
    ///     1.cae4870f-27a2-4236-b0c1-1d0012f9458a.anchor
    ///
    /// (Example 4) This ID points to the 'transform' property of a child (0.3.1.2) on a 'SharingServiceObjectType.Dynamic' target:
    ///     1.cae4870f-27a2-4236-b0c1-1d0012f9458a.0.3.1.2.transform
    /// </summary>
    public class SharingServiceObjectModel : ISharingServiceObject, IDisposable
    {
        private Identification _id;
        private List<SharingServiceObjectModel> _children = new List<SharingServiceObjectModel>();
        private Dictionary<string, object> _properties = new Dictionary<string, object>();
        private Dictionary<string, string> _propertyKeys = new Dictionary<string, string>();
        private ISharingService _service = null;
        private WeakReference<SharingServiceObjectModel> _root = null;
        private LogHelper<SharingServiceObjectModel> _log = new LogHelper<SharingServiceObjectModel>();

        private static SharingServiceObjectModelCache _cache = new SharingServiceObjectModelCache();

        private SharingServiceObjectModel(ISharingService service)
        {
            _service = service ?? throw new ArgumentNullException("Provider can't be null");
            _service.Connected += ProviderConnected;
            _service.Disconnected += ProviderDisconnected;
        }

        public void Dispose()
        {
            if (_service != null)
            {
                _service.Connected -= ProviderConnected;
                _service.Disconnected -= ProviderDisconnected;
                _service = null;
            }

            _cache.Remove(this);
        }

        #region ISharingServiceObject Properties
        /// <summary>
        /// Get the target identification
        /// </summary>
        public Identification Id
        {
            get => _id;

            private set
            {
                if (_id != value)
                {
                    _cache.Remove(this);
                    _id = value;
                    _propertyKeys.Clear();
                    _cache.TryAddValue(this);
                }
            }
        }

        /// <summary>
        /// Get the type of the sharing object.
        /// </summary>
        public SharingServiceObjectType Type => Id.Type;

        /// <summary>
        /// Get the label of the sharing object
        /// </summary>
        public string Label => Id.Label;

        /// <summary>
        /// A target can have multiple objects inside it. This address represents a child object in the target object.
        /// If this is null, then this is the root target.
        /// </summary>
        public int[] Address => Id.Address;

        /// <summary>
        /// Get the id used to share data about this object.
        /// </summary>
        public string SharingId => Id.SharingId;

        /// <summary>
        /// Get if this target is connected to the sharing service.
        /// </summary>
        public bool IsConnected => _service == null ? false : _service.IsConnected;

        /// <summary>
        /// Is this the root target
        /// </summary>
        public bool IsRoot => (Address == null || Address.Length == 0);

        /// <summary>
        /// Get the room that this object was created in.
        /// </summary>
        public ISharingServiceRoom Room { get; private set; }

        /// <summary>
        /// The root target of this child target.
        /// </summary>
        public ISharingServiceObject Root
        {
            get => InnerRoot;
        }

        /// <summary>
        /// The root target of this child target.
        /// </summary>
        public SharingServiceObjectModel InnerRoot
        {
            get
            {
                SharingServiceObjectModel root = null;
                _root?.TryGetTarget(out root);
                return root;
            }

            private set
            {
                lock (_children)
                {
                    SharingServiceObjectModel oldRoot = null;
                    _root?.TryGetTarget(out oldRoot);

                    if (oldRoot != value && this != value)
                    {
                        _root = null;

                        if (oldRoot != null)
                        {
                            lock (oldRoot._children)
                            {
                                oldRoot._children.Remove(this);
                            }
                        }

                        if (value != null && value.IsRoot)
                        {
                            lock (value._children)
                            {
                                if (!value._children.Contains(this))
                                {
                                    value._children.Add(this);
                                }
                                _root = new WeakReference<SharingServiceObjectModel>(value as SharingServiceObjectModel);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get the child belonging to this object.
        /// </summary>
        public IReadOnlyList<ISharingServiceObject> Children
        {
            get
            {
                return _children;
            }
        }

        /// <summary>
        /// Get the current properties.
        /// </summary>
        public IReadOnlyDictionary<string, object> Properties
        {
            get
            {
                return _properties;
            }
        }

        /// <summary>
        /// The spawning information.
        /// </summary>
        public object SpawnInformation
        {
            get => null;
            set
            {
                _log.LogError("A non-MonoBehavior ISharingServiceObject cannot be spawned.");
            }
        }
        #endregion ISharingServiceObject Properties

        #region ISharingServiceObject Events
        /// <summary>
        /// Event raised when a property has changed.
        /// </summary>
        public event Action<ISharingServiceObject, string, object> PropertyChanged;

        /// <summary>
        /// Event raised when a child property has changed.
        /// </summary>
        public event Action<ISharingServiceObject, string, object> ChildPropertyChanged;

        /// <summary>
        /// A specialized message optimized for sending an array of floats to a target.
        /// </summary>
        public event Action<SharingServiceTransform> TransformMessageReceived;

        /// <summary>
        /// A specialized message optimized for sending an array of floats to a target.
        /// </summary>
        public event Action<ISharingServiceObject, SharingServiceTransform> ChildTransformMessageReceived;

        /// <summary>
        /// Event fired when a message is received from a remote client
        /// </summary>
        public event Action<ISharingServiceObject, ISharingServiceMessage> MessageReceived;

        /// <summary>
        /// Event fired when connection changes
        /// </summary>
        public event Action<bool> ConnectionChanged;

        /// <summary>
        /// Event fired when a despawn of this object is requested.
        /// </summary>
        public event Action<ISharingServiceObject> DespawnRequested;
        #endregion ISharingServiceObject Events

        #region ISharingServiceObject Methods
        /// <summary>
        /// Escape property names so that property name can be shared.
        /// </summary>
        public string EncodePropertyName(string property)
        {
            return SharingServicePropertyHelper.EscapeIdPart(property);
        }

        /// <summary>
        /// Set a property on the given target to a praticular value. Setting to null
        /// indicates that the property will be removed from the server.
        /// </summary>
        /// <param name="property">The property to set</param>
        /// <param name="value">The value to set.</param>
        public void SetProperty(string property, object value)
        {
            if (value == null)
            {
                _properties.Remove(property);
            }
            else
            {
                _properties[property] = value;
            }
            SetProviderProperty(property, value);
        }

        /// <summary>
        /// Set a properties on the given target to a praticular value. Setting a value
        /// to null indicates that the property will be removed from the server.
        /// </summary>
        public void SetProperties(params object[] propertyNamesAndValues)
        {
            if (_service != null)
            {
                int count = propertyNamesAndValues.Length - 1;
                for (int i = 0; i < count; i += 2)
                {
                    string name = (string)propertyNamesAndValues[i];
                    object value = propertyNamesAndValues[i + 1];

                    if (value == null)
                    {
                        _properties.Remove(name);
                    }
                    else
                    {
                        _properties[name] = value;
                    }
                    propertyNamesAndValues[i] = _id.SharingIdWithProperty(name);
                }

                _service.SetProperties(propertyNamesAndValues);
            }
        }

        /// <summary>
        /// Try and get a property value of a given target.
        /// </summary>
        /// <param name="property">The property to get</param>
        /// <param name="value">The value of the property.</param>
        public bool TryGetProperty(string property, out object value)
        {
            return (_properties.TryGetValue(property, out value) && value != null) ||
                GetProviderProperty(property, out value);
        }

        /// <summary>
        /// Try and get a property value of a given target.
        /// </summary>
        /// <param name="property">The property to get</param>
        /// <param name="value">The value of the property.</param>
        public bool TryGetProperty<T>(string property, out T value)
        {
            value = default;
            object obj;
            if (TryGetProperty(property, out obj) && obj is T)
            {
                value = (T)obj;
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Does this target have the current property
        /// </summary>
        public bool HasProperty(string property)
        {
            object value;
            return (_properties.TryGetValue(property, out value) && value != null) ||
                (_service != null && _service.HasProperty(Id.SharingIdWithProperty(property)));
        }

        /// <summary>
        /// Clear all properties under this target.
        /// </summary>
        public void ClearProperties()
        {
            _log.LogVerbose($"Clearing Properties.  (id: {SharingId})");
            ClearChildProperties(this);
            _service?.ClearPropertiesStartingWith(Id.SharingId);
        }

        /// <summary>
        /// Send a command message to this target on other clients.
        /// </summary>
        public void SendCommandMessage(string command)
        {
            var target = Id.SharingId;
            if (!string.IsNullOrEmpty(target) && _service != null)
            {
                _service.SendMessage(new SharingServiceMessage()
                {
                    Target = target,
                    Command = command,
                    Sender = _service.LocalPlayer?.Data.PlayerId
                });
            }
        }

        /// <summary>
        /// Send a transform message to this target on other clients.
        /// </summary>
        public void SendTransformMessage(SharingServiceTransform transform)
        {
            var target = Id.SharingId;
            if (!string.IsNullOrEmpty(target) && _service != null)
            {
                _service.SendTransformMessage(target, transform);
            }
        }

        /// <summary>
        /// Get the target object for the given child address.
        /// </summary>
        public ISharingServiceObject AddChild(int[] address)
        {
            Identification childId = Id.ChildId(address);
            SharingServiceObjectModel child;
            if (!_cache.TryGetValue(childId.SharingId, out child))
            {
                if (_service != null)
                {
                    child = new SharingServiceObjectModel(_service)
                    {
                        Id = childId,
                        InnerRoot = this
                    };
                }
            }
            return child;
        }

        /// <summary>
        /// Request a despawn of this object.
        /// </summary>
        public void Despawn(GameObject gameObject)
        {
            DespawnRequested?.Invoke(this);
            _ = _service.DespawnTarget(gameObject);
        }
        #endregion ISharingServiceObject Methods

        #region Public Methods
        /// <summary>
        /// Notify listeners of the property change
        /// </summary>
        public void NotifyPropertyChanged(string property, object value)
        {
            if (value == null)
            {
                _properties.Remove(property);
            }
            else
            {
                _properties[property] = value;
            }
            PropertyChanged?.Invoke(this, property, value);
            InnerRoot?.ChildPropertyChanged?.Invoke(this, property, value);
        }

        public void NotifyMessageReceived(ISharingServiceMessage message)
        {
            MessageReceived?.Invoke(this, message);
        }

        public void NotifyTransformMessageReceived(SharingServiceTransform transform)
        {
            TransformMessageReceived?.Invoke(transform);
            InnerRoot?.ChildTransformMessageReceived?.Invoke(this, transform);
        }

        /// <summary>
        /// Create a new service target
        /// </summary>
        public static SharingServiceObjectModel Create(ISharingService service, ISharingServiceRoom room, Identification id)
        {
            SharingServiceObjectModel target;
            if (!_cache.TryGetValue(id.SharingId, out target))
            {
                target = new SharingServiceObjectModel(service)
                {
                    Id = id,
                    Room = room
                };

                if (!id.IsRootId)
                {
                    SharingServiceObjectModel root = Create(service, room, id.RootId);
                    target.InnerRoot = root;
                }
            }
            return target;
        }

        /// <summary>
        /// Create a new service target
        /// </summary>
        public static SharingServiceObjectModel Create(ISharingService service, ISharingServiceRoom room, string sharingId)
        {
            SharingServiceObjectModel target;
            TryDecodeSharingId(service, room, sharingId, out target);
            return target;
        }

        /// <summary>
        /// Handle property changes from the sharing provider.
        /// </summary>
        public static SharingServiceObjectModel HandleProviderPropertyChanged(ISharingService service, ISharingServiceRoom room, string targetWithProperty, object value)
        {
            SharingServiceObjectModel target;
            string property;

            if (TryDecodeSharingIdWithProperty(
                service,
                room,
                targetWithProperty,
                out target,
                out property))
            {
                target.NotifyPropertyChanged(property, value);
            }

            return target;
        }
        #endregion Public Methods

        #region Private Methods
        private static void ClearChildProperties(SharingServiceObjectModel current)
        {
            if (current == null)
            {
                return;
            }

            current._properties.Clear();
            if (current._children != null)
            {
                foreach (var child in current._children)
                {
                    ClearChildProperties(child);
                }
            }
        }

        /// <summary>
        /// Set a property on the given target to a praticular value.
        /// </summary>
        /// <param name="property">The property to set</param>
        /// <param name="value">The value to set.</param>
        private void SetProviderProperty(string property, object value)
        {
            var sharingId = _id.SharingIdWithProperty(property);
            if (!string.IsNullOrEmpty(sharingId) && _service != null)
            {
                _service.SetProperty(sharingId, value);
            }
        }

        /// <summary>
        /// Attempt to get the property value from the provider's cache.
        /// </summary>
        private bool GetProviderProperty(string property, out object value)
        {
            if (_service == null)
            {
                value = default;
                return false;
            }
            else
            {
                return _service.TryGetProperty(Id.SharingIdWithProperty(property), out value);
            }
        }

        /// <summary>
        /// Try to decode the given string into a target object.
        /// </summary>
        private static bool TryDecodeSharingIdWithProperty(
            ISharingService service,
            ISharingServiceRoom room,
            string value,
            out SharingServiceObjectModel target,
            out string propertyName)
        {
            target = null;
            propertyName = null;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            // Attempt to parse out the property name if there is one.
            int stringStart = value.LastIndexOf('.') + 1;
            if (stringStart <= 0)
            {
                return false;
            }

            propertyName = value.Substring(stringStart);
            value = value.Substring(0, stringStart - 1);
            return TryDecodeSharingId(service, room, value, out target);
        }

        /// <summary>
        /// Try to decode the given string into a target object.
        /// </summary>
        private static bool TryDecodeSharingId(
            ISharingService service,
            ISharingServiceRoom room,
            string value,
            out SharingServiceObjectModel target)
        {
            target = null;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            if (_cache.TryGetValue(value, out target))
            {
                return true;
            }

            // Create a new target value from the given string, and cache it
            int type = (int)SharingServiceObjectType.Unknown;
            string label = null;
            int currentPart = 0;
            int[] address = null;
            string[] parts = value.Split(new []{ '.' }, StringSplitOptions.RemoveEmptyEntries);

            // If 'part' strings have been found, this is an error.
            bool error = parts == null || parts.Length < 2;

            if (!error)
            {
                error = !int.TryParse(parts[currentPart++], out type);
            }

            if (!error)
            { 
                label = parts[currentPart++];

                int addressPart = 0;
                int addressCount = parts.Length - currentPart;
                address = new int[addressCount];

                while (!error &&
                    addressPart < address.Length &&
                    currentPart < parts.Length)
                {
                    int index;
                    if (int.TryParse(parts[currentPart++], out index))
                    {
                        address[addressPart++] = index;
                    }
                    else
                    {
                        error = true;
                    }
                }
            }

            if (!error)
            {
                target = Create(service, room, new Identification()
                {
                    Type = (SharingServiceObjectType)type,
                    Label = label,
                    Address = address
                });
            }

            return target != null;
        }

        /// <summary>
        /// Handle disconnects.
        /// </summary>
        private void ProviderDisconnected(ISharingService sender)
        {
            _log.LogVerbose($"ProviderDisconnected. (id: {SharingId})");
            ConnectionChanged?.Invoke(false);
        }

        /// <summary>
        /// Handle connections, and make sure a local object now has a valid client id
        /// </summary>
        private void ProviderConnected(ISharingService sender)
        {
            _log.LogVerbose($"ProviderConnected. (id: {SharingId})");
            ConnectionChanged?.Invoke(true);
        }
        #endregion Private Methods

        #region Public Struct
        /// <summary>
        /// Represents a sharing object, either a root target or a child target.
        /// </summary>
        /// <remarks> 
        /// This struct does not contain the 'propertyName' part of an ID. This is filtered out by the SharingServiceObjectModel class.
        ///
        /// Here is the sharing ID's format. Note that 'propertyName' is required by property changes, and is not used
        /// with other type of events.
        /// rootType.rootLabel[.childAddress][.propertyName]
        ///
        /// Event ID Examples:
        ///
        /// This ID points to a root target of type 'object':
        /// object.cae4870f-27a2-4236-b0c1-1d0012f9458a
        ///    
        /// This ID points to a child (2.0.1) on an 'object' target:
        /// object.cae4870f-27a2-4236-b0c1-1d0012f9458a.2.0.1
        ///
        /// Property Change ID Examples:
        ///
        /// This ID points to the 'anchor' property on a root target of type 'object':
        /// object.cae4870f-27a2-4236-b0c1-1d0012f9458a.anchor
        ///
        /// This ID points to the 'transform' property of a child (0.3.1.2) on an 'object' target:
        /// object.cae4870f-27a2-4236-b0c1-1d0012f9458a.0.3.1.2.transform
        /// </remarks>
        public struct Identification
        {
            private string _label;
            private string _sharingId;

            /// <summary>
            /// The type of sharing object.
            /// </summary>
            public SharingServiceObjectType Type { get; set; }

            /// <summary>
            /// Get the label of this item, if any.
            /// </summary>
            public string Label
            {
                get
                {
                    if (string.IsNullOrEmpty(_label))
                    {
                        _label = $"{SystemInfo.deviceUniqueIdentifier}:{Guid.NewGuid().ToString()}";
                        SharingServicePropertyHelper.ValidateIdPart(ref _label);
                    }
                    return _label;
                }

                set
                {
                    if (value != null)
                    {
                        _label = value;
                        SharingServicePropertyHelper.ValidateIdPart(ref _label);
                    }
                }
            }

            /// <summary>
            /// A group can have multiple objects inside it. This address represents a single object in the group.
            /// If this is null, then the property values target the group's default.
            /// </summary>
            public int[] Address { get; set; }

            /// <summary>
            /// Get if this is a root id.
            /// </summary>
            public bool IsRootId => Address == null || Address.Length == 0;

            /// <summary>
            /// Get the root id for this id.
            /// </summary>
            public Identification RootId
            {
                get
                {
                    if (IsRootId)
                    {
                        return this;
                    }
                    else
                    {
                        return new Identification()
                        {
                            Type = Type,
                            Label = Label
                        };

                    }
                }
            }


            /// <summary>
            /// The id used to share properties about this object
            /// </summary>
            public string SharingId
            {
                get
                {
                    if (string.IsNullOrEmpty(_sharingId))
                    {
                        _sharingId = CreateLabelWithAddress();
                    }
                    return _sharingId;
                }
            }

            public override bool Equals(object obj)
            {
                if (obj is Identification)
                {
                    return this == (Identification)obj;
                }
                else
                {
                    return base.Equals(obj);
                }
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }

            public static bool operator ==(Identification v1, Identification v2)
            {
                return v1.SharingId != null && v1.SharingId == v2.SharingId;
            }

            public static bool operator !=(Identification v1, Identification v2)
            {
                return v1.SharingId == null || v1.SharingId != v2.SharingId;
            }

            /// <summary>
            /// Get the sharing id with the property name
            /// </summary>
            /// <param name="property"></param>
            public string SharingIdWithProperty(string property)
            {
                return SharingServicePropertyHelper.Encode(SharingId, property);
            }

            public Identification ChildId(int[] address)
            {
                return new Identification()
                {
                    Label = Label,
                    Type = Type,
                    Address = address
                };
            }

            /// <summary>
            /// Encode the type, label and address into a string format, suitable for sharing services.
            /// </summary>
            private string CreateLabelWithAddress()
            {
                if (string.IsNullOrEmpty(Label))
                {
                    return null;
                }

                string result;
                if (Address != null && Address.Length > 0)
                {
                    result = $"{(int)Type}.{Label}.{string.Join(".", Address)}";
                }
                else
                {
                    result = $"{(int)Type}.{Label}";
                }
                return result;
            }
        }
        #endregion Public Struct

        #region Private Class
        private class SharingServiceObjectModelCache
        {
            private Dictionary<string, WeakReference<SharingServiceObjectModel>> _cache =
                new Dictionary<string, WeakReference<SharingServiceObjectModel>>();

            public bool TryGetValue(string sharingId, out SharingServiceObjectModel target)
            {
                if (string.IsNullOrEmpty(sharingId))
                {
                    target = null;
                    return false;
                }

                lock (_cache)
                {
                    WeakReference<SharingServiceObjectModel> weak;
                    if (_cache.TryGetValue(sharingId, out weak))
                    {
                        if (!weak.TryGetTarget(out target))
                        {
                            _cache.Remove(sharingId);
                        }
                    }
                    else
                    {
                        target = null;
                    }
                }

                return target != null;
            }

            public bool TryAddValue(SharingServiceObjectModel target)
            {
                bool added = false;
                string sharingId = target?.Id.SharingId;

                if (!string.IsNullOrEmpty(sharingId))
                {
                    lock (_cache)
                    {
                        SharingServiceObjectModel validityCheck = null;
                        if (!_cache.ContainsKey(sharingId) ||
                            !_cache[sharingId].TryGetTarget(out validityCheck))
                        {
                            _cache[sharingId] = new WeakReference<SharingServiceObjectModel>(target);
                            added = true;
                        }
                    }
                }

                return added;
            }

            public void Remove(SharingServiceObjectModel target)  
            {
                string sharingId = target?.Id.SharingId;
                Remove(sharingId);
            }

            public void Remove(string sharingId)
            {
                if (!string.IsNullOrEmpty(sharingId))
                {
                    lock (_cache)
                    {
                        _cache.Remove(sharingId);
                    }
                }
            }
        }
        #endregion Private Class
    }
}
