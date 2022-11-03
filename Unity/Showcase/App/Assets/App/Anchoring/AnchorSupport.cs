// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// Support functions for anchoring
    /// </summary>
    public static class AnchorSupport
    {
        private static IsNativeEnabledState _isNativeEnabled = IsNativeEnabledState.Unknown;

        /// <summary>
        /// An id representing an anchor that hasn't been saved to the cloud yet.
        /// </summary>
        public static string EmptyAnchorId => Guid.Empty.ToString();

        /// <summary>
        /// Get if the platform support native anchors.
        /// </summary>
        public static bool IsNativeEnabled
        {
            get
            {
                if (_isNativeEnabled == IsNativeEnabledState.Unknown)
                {
                    _isNativeEnabled = IsNativeEnabledState.False;
                    if (!Application.isEditor && 
                        !CoreServices.CameraSystem.IsOpaque &&
                        UnityEngine.Object.FindObjectOfType<ARAnchorManager>() != null)
                    {
                        _isNativeEnabled = IsNativeEnabledState.True;
                    }
                }

                return _isNativeEnabled == IsNativeEnabledState.True;
            }
        }

        /// <summary>
        /// Is the service anchor id empty.
        /// </summary>
        public static bool IsEmptyAnchor(string anchorId)
        {
            return string.IsNullOrEmpty(anchorId) || EmptyAnchorId == anchorId;
        }

        /// <summary>
        /// A helper enum to track if IsNativeEnabled has been initialized.
        /// </summary>
        private enum IsNativeEnabledState
        {
            Unknown,
            True,
            False
        }
    }
}
