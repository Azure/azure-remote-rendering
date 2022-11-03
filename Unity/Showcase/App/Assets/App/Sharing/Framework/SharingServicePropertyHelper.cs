// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    public static class SharingServicePropertyHelper
    {
        /// <summary>
        /// Encode an object id and property name into a single string.
        /// </summary>
        public static string Encode(string objectId, string property)
        {
            if (string.IsNullOrEmpty(objectId) ||
                string.IsNullOrEmpty(property))
            {
                return null;
            }

            ValidateIdPart(ref property);
            return $"{objectId}.{property}";
        }

        public static void ValidateIdPart(ref string part)
        {
            part = EscapeIdPart(part, log: true);
        }

        public static string EscapeIdPart(string part)
        {
            return EscapeIdPart(part, log: false);
        }

        private static string EscapeIdPart(string part, bool log)
        {
            if (part.IndexOf('.') >= 0)
            {
                if (log)
                {
                    Debug.LogWarning($"ISharingServiceObject id part '{part}' contained an invalid character.");
                }
                part = part.Replace('.', '-');
            }
            return part;
        }
    }
}
