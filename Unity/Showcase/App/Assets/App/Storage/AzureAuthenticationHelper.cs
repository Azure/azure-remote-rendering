// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Azure.Storage
{
    /// <summary>
    /// You can take this class and drop it into another project and use this code
    /// to create the headers you need to make a REST API call to Azure Storage.
    /// </summary>
    internal static class AuthenticationHelper
    {
        /// <summary>
        /// This creates the authorization header. This is required, and must be built 
        ///   exactly following the instructions. This will return the authorization header
        ///   for most storage service calls.
        /// Create a string of the message signature and then encrypt it.
        /// </summary>
        /// <param name="storageAccountName">The name of the storage account to use.</param>
        /// <param name="storageAccountKey">The access key for the storage account to be used.</param>
        /// <param name="now">Date/Time stamp for now.</param>
        /// <param name="httpRequestMessage">The HttpWebRequest that needs an auth header.</param>
        /// <param name="ifMatch">Provide an eTag, and it will only make changes
        /// to a blob if the current eTag matches, to ensure you don't overwrite someone else's changes.</param>
        /// <param name="md5">Provide the md5 and it will check and make sure it matches the blob's md5.
        /// If it doesn't match, it won't return a value.</param>
        /// <returns></returns>
        internal static void AddAuthorizationHeader(
           string storageAccountName,
           string storageAccountKey,
           HttpRequestMessage httpRequestMessage,
           string ifMatch = "",
           string md5 = "")
        {
            DateTime authorizationTime = DateTime.UtcNow;

            // Add the request headers for x-ms-date and x-ms-version.
            httpRequestMessage.Headers.Add("x-ms-date", authorizationTime.ToString("R", CultureInfo.InvariantCulture));
            httpRequestMessage.Headers.Add("x-ms-version", "2019-02-02");
            httpRequestMessage.Headers.Authorization = GetAuthorizationHeader(
                storageAccountName,
                storageAccountKey,
                authorizationTime,
                httpRequestMessage,
                ifMatch,
                md5);
        }

        /// <summary>
        /// This creates the authorization header. This is required, and must be built 
        ///   exactly following the instructions. This will return the authorization header
        ///   for most storage service calls.
        /// Create a string of the message signature and then encrypt it.
        /// </summary>
        /// <param name="storageAccountName">The name of the storage account to use.</param>
        /// <param name="storageAccountKey">The access key for the storage account to be used.</param>
        /// <param name="now">Date/Time stamp for now.</param>
        /// <param name="httpRequestMessage">The HttpWebRequest that needs an auth header.</param>
        /// <param name="ifMatch">Provide an eTag, and it will only make changes
        /// to a blob if the current eTag matches, to ensure you don't overwrite someone else's changes.</param>
        /// <param name="md5">Provide the md5 and it will check and make sure it matches the blob's md5.
        /// If it doesn't match, it won't return a value.</param>
        /// <returns></returns>
        internal static void AddAuthorizationHeader(
           string accessToken,
           HttpRequestMessage httpRequestMessage)
        {
            DateTime authorizationTime = DateTime.UtcNow;

            // Add the request headers for x-ms-date and x-ms-version.
            httpRequestMessage.Headers.Add("x-ms-date", authorizationTime.ToString("R", CultureInfo.InvariantCulture));
            httpRequestMessage.Headers.Add("x-ms-version", "2019-02-02");
            httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        /// <summary>
        /// This creates the authorization header. This is required, and must be built 
        ///   exactly following the instructions. This will return the authorization header
        ///   for most storage service calls.
        /// Create a string of the message signature and then encrypt it.
        /// </summary>
        /// <param name="storageAccountName">The name of the storage account to use.</param>
        /// <param name="storageAccountKey">The access key for the storage account to be used.</param>
        /// <param name="now">Date/Time stamp for now.</param>
        /// <param name="httpRequestMessage">The HttpWebRequest that needs an auth header.</param>
        /// <param name="ifMatch">Provide an eTag, and it will only make changes
        /// to a blob if the current eTag matches, to ensure you don't overwrite someone else's changes.</param>
        /// <param name="md5">Provide the md5 and it will check and make sure it matches the blob's md5.
        /// If it doesn't match, it won't return a value.</param>
        /// <returns></returns>
        internal static AuthenticationHeaderValue GetAuthorizationHeader(
           string storageAccountName, 
           string storageAccountKey, 
           DateTime now,
           HttpRequestMessage httpRequestMessage,
           string ifMatch = "", 
           string md5 = "")
        {
            // This is the raw representation of the message signature.
            HttpMethod method = httpRequestMessage.Method;

            // Create message signature using this format:
            //
            // StringToSign = 
            //   VERB + "\n" +  
            //   Content-Encoding + "\n" +  
            //   Content-Language + "\n" +  
            //   Content-Length + "\n" +  
            //   Content-MD5 + "\n" +  
            //   Content-Type + "\n" +  
            //   Date + "\n" +  
            //   If-Modified-Since + "\n" +  
            //   If-Match + "\n" +  
            //   If-None-Match + "\n" +  
            //   If-Unmodified-Since + "\n" +  
            //   Range + "\n" +  
            //   CanonicalizedHeaders +   
            //   CanonicalizedResource;  
            //

            string verb = method.ToString().ToUpper();
            string contentEncoding = (method == HttpMethod.Get || method == HttpMethod.Head) ? string.Empty : httpRequestMessage.Content.Headers.ContentEncoding.ToString(); 
            string contentLanguage = (method == HttpMethod.Get || method == HttpMethod.Head) ? string.Empty : httpRequestMessage.Content.Headers.ContentLanguage.ToString();
            string contentLength = (method == HttpMethod.Get || method == HttpMethod.Head) ? string.Empty : httpRequestMessage.Content.Headers.ContentLength.ToString();
            string contentType = (method == HttpMethod.Get || method == HttpMethod.Head) ? string.Empty : httpRequestMessage.Content.Headers.ContentType.ToString();
            string date = string.Empty;
            string ifModifiedSince = string.Empty;
            string ifNoneMatch = string.Empty;
            string ifUnmodifiedSince = string.Empty;
            string range = string.Empty;
            string canonicalizedHeaders = GetCanonicalizedHeaders(httpRequestMessage);
            string canonicalizedResource = GetCanonicalizedResource(httpRequestMessage.RequestUri, storageAccountName);

            string messageSignature =
                $"{verb}\n{contentEncoding}\n{contentLanguage}\n{contentLength}\n{md5}\n{contentType}\n{date}\n{ifModifiedSince}\n{ifMatch}\n{ifNoneMatch}\n{ifUnmodifiedSince}\n{range}\n{canonicalizedHeaders}{canonicalizedResource}";

            // Now turn it into a byte array.
            byte[] signatureBytes = Encoding.UTF8.GetBytes(messageSignature);

            // Create the HMACSHA256 version of the storage key.
            HMACSHA256 sha256 = new HMACSHA256(Convert.FromBase64String(storageAccountKey));

            // Compute the hash of the SignatureBytes and convert it to a base64 string.
            string signature = Convert.ToBase64String(sha256.ComputeHash(signatureBytes));

            // This is the actual header that will be added to the list of request headers.
            // You can stop the code here and look at the value of 'authHV' before it is returned.
            AuthenticationHeaderValue authHV = new AuthenticationHeaderValue("SharedKey", $"{storageAccountName}:{signature}");
            return authHV;
        }

        /// <summary>
        /// Put the headers that start with x-ms in a list and sort them.
        /// Then format them into a string of [key:value\n] values concatenated into one string.
        /// (Canonicalized Headers = headers where the format is standardized).
        /// </summary>
        /// <param name="httpRequestMessage">The request that will be made to the storage service.</param>
        /// <returns>Error message; blank if okay.</returns>
        private static string GetCanonicalizedHeaders(HttpRequestMessage httpRequestMessage)
        {
            var headers =
                from kvp in httpRequestMessage.Headers
                where kvp.Key.StartsWith("x-ms-", StringComparison.OrdinalIgnoreCase)
                orderby kvp.Key
                select new { Key = kvp.Key.ToLowerInvariant(), kvp.Value };

            StringBuilder sb = new StringBuilder();

            // Create the string in the right format; this is what makes the headers "canonicalized" --
            //   it means put in a standard format. http://en.wikipedia.org/wiki/Canonicalization
            foreach (var kvp in headers)
            {
                StringBuilder headerBuilder = new StringBuilder(kvp.Key);
                char separator = ':';

                // Get the value for each header, strip out \r\n if found, then append it with the key.
                foreach (string headerValues in kvp.Value)
                {
                    string trimmedValue = headerValues.TrimStart().Replace("\r\n", String.Empty);
                    headerBuilder.Append(separator).Append(trimmedValue);

                    // Set this to a comma; this will only be used 
                    //   if there are multiple values for one of the headers.
                    separator = ',';
                }
                sb.Append(headerBuilder.ToString()).Append("\n");
            }
            return sb.ToString();
        }

        /// <summary>
        /// This part of the signature string represents the storage account 
        ///   targeted by the request. Will also include any additional query parameters/values.
        /// For ListContainers, this will return something like this:
        ///   /storageaccountname/\ncomp:list
        /// </summary>
        /// <param name="address">The URI of the storage service.</param>
        /// <param name="accountName">The storage account name.</param>
        /// <returns>String representing the canonicalized resource.</returns>
        private static string GetCanonicalizedResource(Uri address, string storageAccountName)
        {
            // The absolute path is "/" because for we're getting a list of containers.
            StringBuilder sb = new StringBuilder("/").Append(storageAccountName).Append(address.AbsolutePath);

            // Address.Query is the resource, such as "?comp=list".
            // This ends up with a NameValueCollection with 1 entry having key=comp, value=list.
            // It will have more entries if you have more query parameters.
            NameValueCollection values = ParseQueryString(address.Query);

            foreach (var item in values.AllKeys.OrderBy(k => k))
            {
                sb.Append('\n').Append(item.ToLower()).Append(':').Append(values[item]);
            }

            return sb.ToString();
        }


        /// <summary>
        /// Parse a given url query string.
        /// </summary>
        private static NameValueCollection ParseQueryString(string queryString)
        {
            NameValueCollection result = new NameValueCollection();
            var keyValues = queryString.Split('?', '&','=');

            int count = keyValues.Length;
            for (int i = 1; i < count; i++)
            {
                string key = keyValues[i];
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                string value = ++i < count ? keyValues[i] : string.Empty;
                result.Add(key, value);
            }

            return result;
        }
    }
}