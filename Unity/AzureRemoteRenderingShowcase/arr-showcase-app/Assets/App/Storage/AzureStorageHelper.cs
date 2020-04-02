// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Microsoft.Azure.Storage
{
    public static class AzureStorageHelper
    {
        /// <summary>
        /// Perform an unauthenticated Http GET request.
        /// </summary>
        /// <param name="url">The URL to query</param>
        /// <returns></returns>
        public static Task<TResult> Get<TResult>(
            string url) where TResult : class
        {
            return Get<TResult>(url, null, null);
        }

        /// <summary>
        /// Perform an authenticated Http GET request.
        /// </summary>
        /// <param name="url">The URL to query</param>
        /// <param name="storageAccountName">The Azure Storage Account Name</param>
        /// <param name="storageAccountKey">The Azure Storage Account Key</param>
        /// <returns></returns>
        public static async Task<TResult> Get<TResult>(
            string url,
            string storageAccountName,
            string storageAccountKey) where TResult : class
        {
            TResult result = null;
            if (!string.IsNullOrEmpty(url))
            {
                MemoryStream memoryStream = null;
                Stream stream = null;
                HttpRequestMessage webRequestMessage = null;
                HttpClient webClient = null;
                HttpResponseMessage webResponse = null;
                try
                {
                    webClient = new HttpClient();
                    webRequestMessage = new HttpRequestMessage(HttpMethod.Get, url);

                    if (!string.IsNullOrEmpty(storageAccountName) ||
                        !string.IsNullOrEmpty(storageAccountKey))
                    {
                        // Add the request headers for x-ms-date and x-ms-version.
                        AuthenticationHelper.AddAuthorizationHeader(storageAccountName, storageAccountKey, webRequestMessage);
                    }

                    // Make request and get response message as a stream
                    webResponse = await webClient.SendAsync(webRequestMessage);
                    if (webResponse.StatusCode == HttpStatusCode.OK)
                    {
                        stream = await webResponse.Content.ReadAsStreamAsync();
                    }

                    // Deserialize XML
                    if (stream != null)
                    {
                        memoryStream = new MemoryStream();
                        await stream.CopyToAsync(memoryStream);
                        await memoryStream.FlushAsync();
                        memoryStream.Position = 0;

                        result = await Task.Run(() =>
                        {
                            XmlSerializer xml = new XmlSerializer(typeof(TResult));
                            return xml.Deserialize(memoryStream) as TResult;
                        });
                    }
                }
                finally
                {
                    memoryStream?.Close();
                    stream?.Close();
                    webResponse?.Dispose();
                    webRequestMessage?.Dispose();
                    webClient?.Dispose();
                }
            }

            return result;
        }
    }
}
