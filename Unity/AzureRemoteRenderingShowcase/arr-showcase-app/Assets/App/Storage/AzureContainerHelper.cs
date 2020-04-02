// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.Storage
{
    public static class ContainerHelper 
    {
        public static async Task<EnumerationResults> Query(
            string storageAccountName,
            string storageAccountKey,
            string storageContainer,
            string marker = null)
        {
            if (string.IsNullOrEmpty(storageAccountName))
            {
                throw new ArgumentNullException("storageAccountName");
            }

            if (string.IsNullOrEmpty(storageAccountKey))
            {
                throw new ArgumentNullException("storageAccountKey");
            }

            if (string.IsNullOrEmpty(storageContainer))
            {
                throw new ArgumentNullException("storageContainer");
            }

            string url = $"https://{storageAccountName}.blob.core.windows.net/{storageContainer}?restype=container&comp=list";

            if (!string.IsNullOrEmpty(marker))
            {
                url = $"{url}&marker={marker}";
            }

            EnumerationResults result = await AzureStorageHelper.Get<EnumerationResults>(url, storageAccountName, storageAccountKey);
            if (result != null)
            {
                result.Container = $"https://{storageAccountName}.blob.core.windows.net/{storageContainer}";
            }

            return result;
        }
    }
}
