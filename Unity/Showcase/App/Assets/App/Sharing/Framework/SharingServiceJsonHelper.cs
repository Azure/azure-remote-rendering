// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;


namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// Helpers for consuming json.
    /// </summary>
    public static class SharingServiceJsonHelper
    {
        /// <summary>
        /// Deserialize from JSON
        /// </summary>
        static public bool DeserializeFromJson<T>(string value, out object result)
        {
            bool success = false;
            if (string.IsNullOrEmpty(value))
            {
                result = null;
                return success;
            }

            try
            {
                result = JsonUtility.FromJson<T>(value);
                success = true;
            }
            catch
            {
                result = null;
                success = false;
            }
            return success;
        }
    }
}
