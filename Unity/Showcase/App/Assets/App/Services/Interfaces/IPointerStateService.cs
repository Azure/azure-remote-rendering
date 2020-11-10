// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// The interface helps manage the state of the pointers. It manages safely hiding/showing pointers as well as putting
    /// a pointer into a particular mode. Pointer modes are used when interacting with objects. For example, you can put 
    /// the pointer into a "manipulate" mode or a "delete" mode.
    /// </summary>
	public interface IPointerStateService : IMixedRealityExtensionService
    {
        /// <summary>
        /// Event raised when mode changes
        /// </summary>
        event EventHandler<IPointerModeChangedEventData> ModeChanged;

        /// <summary>
        /// Get or set the pointer mode that all pointers will be placed in
        /// </summary>
        PointerMode Mode { get; set; }

        /// <summary>
        /// Get or set mode data for the current mode.
        /// </summary>
        Object ModeData { get; }

        /// <summary>
        /// Set the pointer mode along with some associated data.
        /// </summary>
        void SetModeWithData(PointerMode mode, object modeData);

        /// <summary>
        /// Prevent the given pointer type from being shown/used. If ShowPointer() is called after this, the pointer
        /// will be shown until the caller of ShowPointer() disposes its IPointerStateVisibilityOverride object.
        /// </summary>
        /// <returns>A IPointerStateVisibilityOverride object. Dispose this object to undo the hide request.</returns>
        IPointerStateVisibilityOverride HidePointer(PointerType pointerType);

        /// <summary>
        /// Force the given pointer type to be shown/used. If HidePointer() is called after this, the pointer
        /// will be hidden until the caller of ShowPointer() disposes its IPointerStateVisibilityOverride object.
        /// </summary>
        /// <returns>A IPointerStateVisibilityOverride object. Dispose this object to undo the show request.</returns>
        IPointerStateVisibilityOverride ShowPointer(PointerType pointerType);
    }

    /// <summary>
    /// Represents a current pointer visibility override. To undo the override request, invoke the Dispose() method
    /// </summary>
    public interface IPointerStateVisibilityOverride : IDisposable
    {
    } 
}
