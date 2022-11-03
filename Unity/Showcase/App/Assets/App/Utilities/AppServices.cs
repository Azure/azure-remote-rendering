// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Input;
using System.Collections.Generic;
using System;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    public static class AppServices
    {
        private static readonly Dictionary<Type, WeakReference<IMixedRealityService>> _serviceCache 
            = new Dictionary<Type, WeakReference<IMixedRealityService>>();

        #region Public Properties
        public static IRemoteRenderingService RemoteRendering
        {
            get
            {
                return GetService<IRemoteRenderingService>();
            }
        }

        public static IRemoteObjectFactoryService RemoteObjectFactory
        {
            get
            {
                return GetService<IRemoteObjectFactoryService>();
            }
        }

        public static IRemoteFocusProvider RemoteFocusProvider
        {
            get
            {
                return GetService<IRemoteFocusProvider>();
            }
        }

        public static IPointerStateService PointerStateService
        {
            get
            {
                return GetService<IPointerStateService>();
            }
        }

        public static IAppSettingsService AppSettingsService
        {
            get
            {
                return GetService<IAppSettingsService>();
            }
        }

        public static IAppNotificationService AppNotificationService
        {
            get
            {
                return GetService<IAppNotificationService>();
            }
        }

        public static ISharingService SharingService
        {
            get
            {
                return GetService<ISharingService>();
            }
        }

        public static IAnchoringService AnchoringService
        {
            get
            {
                return GetService<IAnchoringService>();
            }
        }

        public static IRemoteObjectStageService RemoteObjectStageService
        {
            get
            {
                return GetService<IRemoteObjectStageService>();
            }
        }
        #endregion Public Properties

        #region Private Methods
        /// <summary>
        /// Get or create an application service
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        private static T GetService<T>() where T : IMixedRealityService
        {
            Type serviceType = typeof(T);

            // See if we already have a WeakReference entry for this service type
            if (_serviceCache.ContainsKey(serviceType))
            {
                IMixedRealityService svc;
                // If our reference object is still alive, return it
                if (_serviceCache[serviceType].TryGetTarget(out svc))
                {
                    return (T)svc;
                }

                // Our reference object has been collected by the GC. Try to get the latest service if available
                _serviceCache.Remove(serviceType);
            }

            // This is the first request for the given service type. See if it is available and if so, add entry
            T service;
            if (!MixedRealityServiceRegistry.TryGetService(out service))
            {
                return default(T);
            }

            _serviceCache.Add(typeof(T), new WeakReference<IMixedRealityService>(service, false));
            return service;
        }
        #endregion Private Methods
    }
}
