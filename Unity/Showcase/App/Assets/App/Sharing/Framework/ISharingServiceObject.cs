// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// This is a sharing service object. These objects are used by the sharing service to direct events  
    /// and property changes to a particular game object within the game's scene. 
    ///
    /// A sharing object can have "children" or "addressable parts" (see the 'Children' and 'Address' properties).
    /// 
    /// A sharing ID is used to direct events and property changes to a sharing service objects.
    ///    
    /// If a sharing ID has an child address, the event or property change for that ID will be redirected to the root's 
    /// child. 
    /// 
    /// If as sharing ID has no child address, the event or property change will be handled by the root.
    ///
    /// Here is the sharing ID's format. Note that 'propertyName' is required by property changes, and is not used
    /// with other type of events:
    /// 
    /// parentType.parentLabel[.childAddress][.propertyName]
    ///
    /// Event ID Examples:
    ///
    /// This ID points to a root target of type 'SharingServiceObjectType.Dynamic':
    /// 1.cae4870f-27a2-4236-b0c1-1d0012f9458a
    ///    
    /// This ID points to a child (2.0.1) on a 'SharingServiceObjectType.Dynamic' target:
    /// 1.cae4870f-27a2-4236-b0c1-1d0012f9458a.2.0.1
    ///
    /// Property Change ID Examples:
    ///
    /// This ID points to the 'anchor' property on a root target of type 'SharingServiceObjectType.Dynamic':
    /// 1.cae4870f-27a2-4236-b0c1-1d0012f9458a.anchor
    ///
    /// This ID points to the 'transform' property of a child (0.3.1.2) on a 'SharingServiceObjectType.Dynamic' target:
    /// 1.cae4870f-27a2-4236-b0c1-1d0012f9458a.0.3.1.2.transform
    /// </summary>
    public interface ISharingServiceObject
    {
        /// <summary>
        /// Get the type of the sharing object
        /// </summary>
        SharingServiceObjectType Type { get; }

        /// <summary>
        /// Get the label (or ID) of the sharing object
        /// </summary>
        string Label { get; }

        /// <summary>
        /// A target can have multiple objects inside it. This address represents a child object in the target object.
        /// If this is null, then this is the root target.
        /// </summary>
        int[] Address { get; }

        /// <summary>
        /// Get the id used to share data about this object.
        /// </summary>
        string SharingId { get; }

        /// <summary>
        /// Is this the root target
        /// </summary>
        bool IsRoot { get; }

        /// <summary>
        /// Get the room that this object was created in.
        /// </summary>
        ISharingServiceRoom Room { get; }

        /// <summary>
        /// The root target of this child target.
        /// </summary>
        ISharingServiceObject Root { get; }

        /// <summary>
        /// Get the current properties.
        /// </summary>
        IReadOnlyList<ISharingServiceObject> Children { get; }

        /// <summary>
        /// Get the current properties.
        /// </summary>
        IReadOnlyDictionary<string, object> Properties { get; }

        /// <summary>
        /// Event fired when a property changes.
        /// </summary>
        event Action<ISharingServiceObject, string, object> PropertyChanged;

        /// <summary>
        /// Event raised when a child property has changed.
        /// </summary>
        event Action<ISharingServiceObject, string, object> ChildPropertyChanged;

        /// <summary>
        /// Event fired when a message is received from a remote client
        /// </summary>
        event Action<ISharingServiceObject, ISharingServiceMessage> MessageReceived;

        /// <summary>
        /// Event fired when connection changes
        /// </summary>
        event Action<bool> ConnectionChanged;

        /// <summary>
        /// A specialized message optimized for sending an array of floats to a target.
        /// </summary>
        event Action<SharingServiceTransform> TransformMessageReceived;

        /// <summary>
        /// A specialized message optimized for sending an array of floats to a target.
        /// </summary>
        event Action<ISharingServiceObject, SharingServiceTransform> ChildTransformMessageReceived;

        /// <summary>
        /// Event fired when a despawn of this object is requested.
        /// </summary>
        event Action<ISharingServiceObject> DespawnRequested;

        /// <summary>
        /// Get the target object for the given child address.
        /// </summary>
        ISharingServiceObject AddChild(int[] address);

        /// <summary>
        /// Escape property names so that property name can be shared.
        /// </summary>
        string EncodePropertyName(string property);

        /// <summary>
        /// Set a property on the given target to a praticular value.
        /// </summary>
        /// <param name="property">The property to set</param>
        /// <param name="value">The value to set.</param>
        void SetProperty(string property, object value);

        /// <summary>
        /// Set a properties on the given target to a praticular value. Setting a value
        /// to null indicates that the property will be removed from the server.
        /// </summary>
        void SetProperties(params object[] propertyNamesAndValues);

        /// <summary>
        /// Try and get a property value of a given target.
        /// </summary>
        /// <param name="property">The property to get</param>
        /// <param name="value">The value of the property.</param>
        bool TryGetProperty(string property, out object value);

        /// <summary>
        /// Try and get a property value of a given target.
        /// </summary>
        /// <param name="property">The property to get</param>
        /// <param name="value">The value of the property.</param>
        bool TryGetProperty<T>(string property, out T value);

        /// <summary>
        /// Does this target have the given property
        /// </summary>
        bool HasProperty(string property);

        /// <summary>
        /// Clear all properties under this target.
        /// </summary>
        void ClearProperties();

        /// <summary>
        /// Send a command message to this target on other clients.
        /// </summary>
        void SendCommandMessage(string command);

        /// <summary>
        /// Send a transform message to this target on other clients.
        /// </summary>
        void SendTransformMessage(SharingServiceTransform transform);

        /// <summary>
        /// Request a despawn of this object.
        /// </summary>
        void Despawn(GameObject gameObject);
    }
}
