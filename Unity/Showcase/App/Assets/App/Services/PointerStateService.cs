// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
	[MixedRealityExtensionService(SupportedPlatforms.WindowsStandalone|SupportedPlatforms.MacStandalone|SupportedPlatforms.LinuxStandalone|SupportedPlatforms.WindowsUniversal)]
	public class PointerStateService : BaseExtensionService, IPointerStateService, IMixedRealityExtensionService
	{
		private PointerStateServiceProfile _pointerStateServiceProfile;
        private PointerMode _mode = PointerMode.None;
        private readonly Dictionary<PointerType, List<PointerStateVisibilityOverride>> _visibilityOverrideRequests =
            new Dictionary<PointerType, List<PointerStateVisibilityOverride>>();

        public PointerStateService(string name, uint priority, BaseMixedRealityProfile profile) : base(name, priority, profile) 
		{
			_pointerStateServiceProfile = (PointerStateServiceProfile)profile;
		}

        #region IPointerStateService Properties
        /// <summary>
        /// Get or set the pointer mode that all pointers will be placed in
        /// </summary>
        public PointerMode Mode
        {
            get => _mode;
            set
            {
                SetModeWithData(value, null);
            }
        }

        /// <summary>
        /// Get or set mode data for the current mode.
        /// </summary>
        public System.Object ModeData { get; private set; }
        #endregion IPointerStateService Properties

        #region IPointerStateService Event
        /// <summary>
        /// Event raised when mode changes
        /// </summary>
        public event EventHandler<IPointerModeChangedEventData> ModeChanged;
        #endregion IPointerStateService Event

        #region IPointerStateService Methods
       /// <summary>
       /// Set the pointer mode along with some associated data.
       /// </summary>
        public void SetModeWithData(PointerMode mode, object modeData)
        {
            var oldData = ModeData;
            ModeData = modeData;

            var oldMode = _mode;
            _mode = mode;
            // Always fire changed event, even if _mode hasn't really changed. Resetting the mode to the same value can
            // have meaning. For example, resetting "clip" mode can reset the clipping plane position.
            ModeChanged?.Invoke(this, new PointerModeChangedEventData(oldMode, oldData, _mode, ModeData));
        }

        /// <summary>
        /// Prevent the given pointer type from being shown. If ShowPointer() is called before or after this, the pointer
        /// will be shown until all callers of ShowPointer() disposes its IPointerStateVisibilityOverride object.
        /// </summary>
        /// <returns>A IPointerStateVisibilityOverride object. Dispose this object to undo the hide request.</returns>
        public IPointerStateVisibilityOverride HidePointer(PointerType pointerType)
        {
            return CreateOverride(pointerType, PointerBehavior.AlwaysOff);
        }

        /// <summary>
        /// Force the given pointer type to always be shown, even if HidePointer() is called.
        /// </summary>
        /// <returns>A IPointerStateVisibilityOverride object. Dispose this object to undo the show request.</returns>
        public IPointerStateVisibilityOverride ShowPointer(PointerType pointerType)
        {
            return CreateOverride(pointerType, PointerBehavior.AlwaysOn);
        }
        #endregion IPointerStateService Methods

        #region BaseExtensionService Methods
        #endregion BaseExtensionService Methods

        #region Private Methods
        /// <summary>
        /// Create a new pointer visibility override for the given pointed type. The given visibility behavior will 
        /// get added to the pointer's override list, and applied.
        /// </summary>
        private PointerStateVisibilityOverride CreateOverride(PointerType type, PointerBehavior behavior)
        {
            PointerStateVisibilityOverride visibilityOverride = new PointerStateVisibilityOverride(
                type, behavior, HandleOverrideDispose);

            List<PointerStateVisibilityOverride> currentOverrides;
            if (!_visibilityOverrideRequests.TryGetValue(type, out currentOverrides) || currentOverrides == null)
            {
                _visibilityOverrideRequests[type] = currentOverrides = new List<PointerStateVisibilityOverride>();
            }

            currentOverrides.Add(visibilityOverride);
            behavior = GetHighestPriorityOverride(currentOverrides);
            SetPointerVisibility(type, behavior);
            return visibilityOverride;
        }

        /// <summary>
        /// Get the highest priority behavior in the list of overrides. ShowAlways is a high priority than HideAlways.
        /// </summary>
        private PointerBehavior GetHighestPriorityOverride(List<PointerStateVisibilityOverride> overrides)
        {
            var result = PointerBehavior.Default;
            if (overrides == null || overrides.Count == 0)
            {
                return PointerBehavior.Default;
            }

            foreach (var entry in overrides)
            {
                if (entry.Behavior != PointerBehavior.Default)
                {
                    result = entry.Behavior;
                }

                if (result == PointerBehavior.AlwaysOn)
                {
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// Handle the disposal of a visibility override. This will remove the visibility override from the pointer's 
        /// override list, and then (re)apply the highest priority visibility override.
        /// </summary>
        private void HandleOverrideDispose(PointerStateVisibilityOverride visibilityOverride)
        {
            if (visibilityOverride == null)
            {
                return;
            }

            PointerType type = visibilityOverride.Type;

            List<PointerStateVisibilityOverride> allOverrides;
            if (_visibilityOverrideRequests.TryGetValue(type, out allOverrides) &&
                allOverrides.Remove(visibilityOverride))
            {
                PointerBehavior behavior = GetHighestPriorityOverride(allOverrides);
                if (allOverrides.Count == 0)
                {
                    _visibilityOverrideRequests.Remove(type);
                }
                SetPointerVisibility(type, behavior);
            }
        }

        /// <summary>
        /// Set the pointer visibility
        /// </summary>
        private bool SetPointerVisibility(PointerType pointerType, PointerBehavior pointerBehavior)
        {
            bool success = true;
            try
            {
                switch (pointerType)
                {
                    case PointerType.Gaze:
                        PointerUtils.SetGazePointerBehavior(pointerBehavior);
                        break;

                    case PointerType.HandGrab:
                        PointerUtils.SetHandGrabPointerBehavior(pointerBehavior);
                        break;

                    case PointerType.HandPoke:
                        PointerUtils.SetHandPokePointerBehavior(pointerBehavior);
                        break;

                    case PointerType.HandRay:
                        PointerUtils.SetHandRayPointerBehavior(pointerBehavior);
                        break;

                    default:
                        Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}", $"Unsupported point type '{pointerType}'");
                        success = false;
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "Failed to reset pointer '{0}' to {1}. Exception {2}", pointerType, pointerBehavior, ex);
            }
            return success;
        }
        #endregion Private Methods

        #region Private Classes
        private class PointerModeChangedEventData : IPointerModeChangedEventData
        {
            public PointerModeChangedEventData(
                PointerMode oldValue,
                object oldData,
                PointerMode newValue,
                object newData)
            {
                OldValue = oldValue;
                OldData = oldData;
                NewValue = newValue;
                NewData = newData;
            }

            #region IPointerModeChangedEventData Properties
            /// <summary>
            /// The old pointer mode.
            /// </summary>
            public PointerMode OldValue { get; }

            /// <summary>
            /// The old data associated with the old mode.
            /// </summary>
            public object OldData { get; }

            /// <summary>
            /// The new pointer mode.
            /// </summary>
            public PointerMode NewValue { get; }

            /// <summary>
            /// The new data associated with the new mode.
            /// </summary>
            public object NewData { get; }
            #endregion IPointerModeChangedEventData Properties
        }

        /// <summary>
        /// Represents a current pointer visibility override. To undo the override request, invoke the Dispose() method
        /// </summary>
        private class PointerStateVisibilityOverride : IPointerStateVisibilityOverride
        {
            private Action<PointerStateVisibilityOverride> _disposeHandler;
            private bool _isDisposed;

            public PointerStateVisibilityOverride(PointerType type, PointerBehavior behavior, Action<PointerStateVisibilityOverride> disposeHandler)
            {
                Type = type;
                Behavior = behavior;
                _disposeHandler = disposeHandler;
            }

            /// <summary>
            /// Get the pointer type that's being changed by this override
            /// </summary>
            public PointerType Type { get; }

            /// <summary>
            /// Get the current behavior of this override.
            /// </summary>
            public PointerBehavior Behavior { get; private set; }

            /// <summary>
            /// Undo this override request.
            /// </summary>
            public void Dispose()
            {
                if (!_isDisposed)
                {
                    _isDisposed = true;
                    _disposeHandler?.Invoke(this);
                    Behavior = PointerBehavior.Default;
                }
            }
        }
        #endregion Private Classes
    }
}
