// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Identity.Client;
using System;
using System.IO;
using UnityEngine;

namespace App.Authentication
{
    public static class AADTokenCache
    {
        /// <summary>
        /// Path to the token cache
        /// </summary>
        public static readonly string CacheFilePath = Application.persistentDataPath + "/msalcache.bin3";

        private static readonly object FileLock = new object();

        public static void PrepareOnMainThread()
        {
            // Nothing needs to be done here, just need to wake up this static class on the main thread
        }

        public static void EnableSerialization(ITokenCache tokenCache)
        {
            tokenCache.SetBeforeAccess(BeforeAccessNotification);
            tokenCache.SetAfterAccess(AfterAccessNotification);
        }

        private static void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            lock (FileLock)
            {
                try
                {
                    byte[] tokenData = null;
                    if (File.Exists(CacheFilePath))
                    {
                        tokenData = File.ReadAllBytes(CacheFilePath);
                    }

                    args.TokenCache.DeserializeMsalV3(tokenData);

                } catch(Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        private static void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            // if the access operation resulted in a cache update
            if (args.HasStateChanged)
            {
                lock (FileLock)
                {
                    try
                    {
                        File.WriteAllBytes(CacheFilePath, args.TokenCache.SerializeMsalV3());
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
            }
        }
    }
}
