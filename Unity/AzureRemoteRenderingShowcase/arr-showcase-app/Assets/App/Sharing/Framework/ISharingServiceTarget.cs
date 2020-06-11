// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication;
using System;
using System.Collections.Generic;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// This is a sharing target object. Targets are used by the sharing service to direct events and property changes 
    /// to a particular game object within the game's scene. 
    ///
    /// A sharing target can have "children" or "addressable parts" (see the 'Children' and 'Address' properties).
    /// If a sharing ID has an address, the event or property change for that ID will be redirected to the root's 
    /// child. If as sharing ID has no address, the event or property change will be handled by the root.
    ///
    /// Here is the sharing ID's format. Note that 'propertyName' is required by property changes, and is not used
    /// with other type of events:
    /// 
    /// parentType.parentLabel[.childAddress][.propertyName]
    ///
    /// Event ID Examples:
    ///
    /// This ID points to a root target of type 'SharingServiceTargetType.Dynamic':
    /// 1.cae4870f-27a2-4236-b0c1-1d0012f9458a
    ///    
    /// This ID points to a child (2.0.1) on a 'SharingServiceTargetType.Dynamic' target:
    /// 1.cae4870f-27a2-4236-b0c1-1d0012f9458a.2.0.1
    ///
    /// Property Change ID Examples:
    ///
    /// This ID points to the 'anchor' property on a root target of type 'SharingServiceTargetType.Dynamic':
    /// 1.cae4870f-27a2-4236-b0c1-1d0012f9458a.anchor
    ///
    /// This ID points to the 'transform' property of a child (0.3.1.2) on a 'SharingServiceTargetType.Dynamic' target:
    /// 1.cae4870f-27a2-4236-b0c1-1d0012f9458a.0.3.1.2.transform
    /// </summary>
    public interface ISharingServiceTarget : IDisposable
    {
        /// <summary>
        /// Get the type of the sharing target
        /// </summary>
        SharingServiceTargetType Type { get; }

        /// <summary>
        /// Get the label of the sharing target
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
        /// Get if this target is connected to the sharing service.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Is this the root target
        /// </summary>
        bool IsRoot { get; }

        /// <summary>
        /// The root target of this child target.
        /// </summary>
        ISharingServiceTarget Root { get; }

        /// <summary>
        /// Get the current properties.
        /// </summary>
        IReadOnlyList<ISharingServiceTarget> Children { get; }

        /// <summary>
        /// Get the current properties.
        /// </summary>
        IReadOnlyDictionary<string, object> Properties { get; }

        /// <summary>
        /// Event fired when a property changes.
        /// </summary>
        event Action<ISharingServiceTarget, string, object> PropertyChanged;

        /// <summary>
        /// Event raised when a child property has changed.
        /// </summary>
        event Action<ISharingServiceTarget, string, object> ChildPropertyChanged;

        /// <summary>
        /// Event fired when a message is received from a remote client
        /// </summary>
        event Action<ISharingServiceTarget, ISharingServiceMessage> MessageReceived;

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
        event Action<ISharingServiceTarget, SharingServiceTransform> ChildTransformMessageReceived;

        /// <summary>
        /// Get the target object for the given child address.
        /// </summary>
        ISharingServiceTarget AddChild(int[] address);

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
        void SendMessage(string command);

        /// <summary>
        /// Send a transform message to this target on other clients.
        /// </summary>
        void SendTransformMessage(SharingServiceTransform transform);
    }
}
