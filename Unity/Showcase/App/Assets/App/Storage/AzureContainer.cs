// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Xml.Serialization;

namespace Microsoft.Azure.Storage
{
    [Serializable]
    [XmlRoot]
    public class EnumerationResults
    {
        /// <summary>
        /// The container URL.
        /// </summary>
        public string Container = string.Empty;

        /// <summary>
        /// The blobs within the container
        /// </summary>
        public Blob[] Blobs = new Blob[0];

        /// <summary>
        /// The next set of blobs.
        /// </summary>
        public string NextMarker = string.Empty;

        public bool ShouldSerializeBlobs()
        {
            return Blobs != null && Blobs.Length > 0;
        }

        public bool ShouldSerializeNextMarker()
        {
            return !string.IsNullOrEmpty(NextMarker);
        }
    }
}
