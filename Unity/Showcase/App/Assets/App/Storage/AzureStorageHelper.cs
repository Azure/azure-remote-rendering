// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
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
            return GetWithAccountKey<TResult>(url, null, null);
        }

        /// <summary>
        /// Perform an authenticated Http GET request.
        /// </summary>
        /// <param name="url">The URL to query</param>
        /// <param name="storageAccountName">The Azure Storage Account Name</param>
        /// <param name="storageAccountKey">The Azure Storage Account Key</param>
        /// <returns></returns>
        public static async Task<TResult> GetWithAccountKey<TResult>(
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

        /// <summary>
        /// Perform an authenticated Http GET request.
        /// </summary>
        /// <param name="url">The URL to query</param>
        /// <param name="storageAccountName">The Azure Storage Account Name</param>
        /// <param name="storageAccessToken">The Azure Access Token</param>
        /// <returns></returns>
        public static async Task<TResult> GetWithAccessToken<TResult>(
            string url,
            string storageAccountName,
            string storageAccessToken) where TResult : class
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
                        !string.IsNullOrEmpty(storageAccessToken))
                    {
                        // Add the request headers for x-ms-date and x-ms-version.
                        DateTime authorizationTime = DateTime.UtcNow;
                        webRequestMessage.Headers.Add("x-ms-date", authorizationTime.ToString("R", CultureInfo.InvariantCulture));
                        webRequestMessage.Headers.Add("x-ms-version", "2019-02-02");
                        webRequestMessage.Headers.Add("Authorization", $"Bearer {storageAccessToken}");
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

        /// <summary>
        /// Perform an authenticated Http PUT request.
        /// </summary>
        /// <param name="url">The URL to query</param>
        /// <param name="file">The object to serialize</param>
        /// <param name="storageAccountName">The Azure Storage Account Name</param>
        /// <param name="storageAccountKey">The Azure Storage Account Key</param>
        /// <returns></returns>
        public static async Task PutWithAccountKey<TResult>(
            string url,
            TResult file,
            string storageAccountName,
            string storageAccountKey) where TResult : class
        {
            if (!string.IsNullOrEmpty(url))
            {
                MemoryStream memoryStream = null;
                Stream stream = null;
                HttpRequestMessage webRequestMessage = null;
                HttpClient webClient = null;
                HttpResponseMessage webResponse = null;
                StringWriterWithEncoding textWriter = null;
                try
                {
                    webClient = new HttpClient();
                    webRequestMessage = new HttpRequestMessage(HttpMethod.Put, url);

                    XmlSerializer xml = new XmlSerializer(typeof(TResult));
                    textWriter = new StringWriterWithEncoding();
                    xml.Serialize(textWriter, file);

                    webRequestMessage.Content = new StringContent(textWriter.ToString());
                    webRequestMessage.Headers.Add("x-ms-blob-type", "BlockBlob");
                    if (!string.IsNullOrEmpty(storageAccountName) ||
                        !string.IsNullOrEmpty(storageAccountKey))
                    {
                        // Add the request headers for x-ms-date and x-ms-version.
                        AuthenticationHelper.AddAuthorizationHeader(storageAccountName, storageAccountKey, webRequestMessage);
                    }
                    
                    webResponse = await webClient.SendAsync(webRequestMessage);
                }
                finally
                {
                    textWriter?.Close();
                    memoryStream?.Close();
                    stream?.Close();
                    webResponse?.Dispose();
                    webRequestMessage?.Dispose();
                    webClient?.Dispose();
                }
            }
        }

        /// <summary>
        /// Perform an authenticated Http PUT request.
        /// </summary>
        /// <param name="url">The URL to query</param>
        /// <param name="file">The object to serialize</param>
        /// <param name="storageAccountName">The Azure Storage Account Name</param>
        /// <param name="storageAccessToken">The Azure Access Token</param>
        /// <returns></returns>
        public static async Task PutWithAccessToken<TResult>(
            string url,
            TResult file,
            string storageAccountName,
            string storageAccessToken) where TResult : class
        {
            if (!string.IsNullOrEmpty(url))
            {
                MemoryStream memoryStream = null;
                Stream stream = null;
                HttpRequestMessage webRequestMessage = null;
                HttpClient webClient = null;
                HttpResponseMessage webResponse = null;
                StringWriterWithEncoding textWriter = null;

                try
                {
                    webClient = new HttpClient();
                    webRequestMessage = new HttpRequestMessage(HttpMethod.Put, url);

                    XmlSerializer xml = new XmlSerializer(typeof(TResult));
                    textWriter = new StringWriterWithEncoding();
                    xml.Serialize(textWriter, file);

                    webRequestMessage.Content = new StringContent(textWriter.ToString());
                    webRequestMessage.Headers.Add("x-ms-blob-type", "BlockBlob");

                    if (!string.IsNullOrEmpty(storageAccountName) ||
                        !string.IsNullOrEmpty(storageAccessToken))
                    {
                        // Add the request headers for x-ms-date and x-ms-version.
                        DateTime authorizationTime = DateTime.UtcNow;
                        webRequestMessage.Headers.Add("x-ms-date", authorizationTime.ToString("R", CultureInfo.InvariantCulture));
                        webRequestMessage.Headers.Add("x-ms-version", "2019-02-02");
                        webRequestMessage.Headers.Add("Authorization", $"Bearer {storageAccessToken}");
                    }

                    // Make request and get response message as a stream
                    webResponse = await webClient.SendAsync(webRequestMessage);
                }
                finally
                {
                    textWriter?.Close();
                    memoryStream?.Close();
                    stream?.Close();
                    webResponse?.Dispose();
                    webRequestMessage?.Dispose();
                    webClient?.Dispose();
                }
            }
        }

        /// <summary>
        /// Extract a blob name from an absolute url for a blob.
        /// </summary>
        public static string GetBlobName(string absoluteUrl)
        {
            string blobName = string.Empty;

            var blobUri = new Uri(absoluteUrl);
            if (Uri.IsWellFormedUriString(blobUri.AbsoluteUri, UriKind.RelativeOrAbsolute) &&
                Uri.CheckHostName(blobUri.Host) != UriHostNameType.Unknown &&
                blobUri.Segments.Length >= 3) // must hold service endpoint, container name, and blob path
            {
                string containerName = blobUri.Segments[1].Replace("/", "");
                // The blob path is the local path minus the container name,
                // which is the first segments of the path. We strip it and the
                // surrounding directory delimiters from the beginning of the
                // local path.
                blobName = blobUri.LocalPath.Substring(containerName.Length + 2);
            }

            return blobName;
        }

        /// <summary>
        /// Extract a container name from an absolute url for a blob.
        /// </summary>
        public static string GetContainerName(string absoluteUrl)
        {
            string containerName = null;

            var blobUri = new Uri(absoluteUrl);
            if (Uri.IsWellFormedUriString(blobUri.AbsoluteUri, UriKind.RelativeOrAbsolute) &&
                Uri.CheckHostName(blobUri.Host) != UriHostNameType.Unknown &&
                blobUri.Segments.Length >= 3) // must hold service endpoint, container name, and blob path
            {
                containerName = blobUri.Segments[1].TrimEnd('/');
            }

            return containerName;
        }

        /// <summary>
        /// Extract a container uri from an absolute url for a blob.
        /// </summary>
        public static Uri GetContainerUri(string absoluteUrl)
        {
            Uri containerUri = default;

            var blobUri = new Uri(absoluteUrl);
            if (Uri.IsWellFormedUriString(blobUri.AbsoluteUri, UriKind.RelativeOrAbsolute) &&
                Uri.CheckHostName(blobUri.Host) != UriHostNameType.Unknown &&
                blobUri.Segments.Length >= 3) // must hold service endpoint, container name, and blob path
            {
                containerUri = new Uri($"{blobUri.Scheme}://{blobUri.Host}{blobUri.Segments[0]}{blobUri.Segments[1].TrimEnd('/')}");
            }

            return containerUri;
        }

        /// <summary>
        /// Test if the given blob url is in the given container
        /// </summary>
        public static bool InContainer(string absoluteUrl, CloudBlobContainer container)
        {
            var containerName = GetContainerName(absoluteUrl);
            return container != null && container.Uri == GetContainerUri(absoluteUrl);
        }
    }

    public sealed class StringWriterWithEncoding : StringWriter
    {
        private readonly Encoding encoding;

        public StringWriterWithEncoding() : this(Encoding.UTF8) { }

        public StringWriterWithEncoding(Encoding encoding)
        {
            this.encoding = encoding;
        }

        public override Encoding Encoding
        {
            get { return encoding; }
        }
    }
}
