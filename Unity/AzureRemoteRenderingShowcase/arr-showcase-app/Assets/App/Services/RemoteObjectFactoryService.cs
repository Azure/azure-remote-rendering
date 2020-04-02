// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.MixedReality.Toolkit.Utilities;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
	[MixedRealityExtensionService(SupportedPlatforms.WindowsStandalone|SupportedPlatforms.MacStandalone|SupportedPlatforms.LinuxStandalone|SupportedPlatforms.WindowsUniversal)]
	public class RemoteObjectFactoryService : BaseExtensionService, IRemoteObjectFactoryService, IMixedRealityExtensionService
	{
		private RemoteObjectFactoryServiceProfile _remoteObjectFactoryServiceProfile;
        private readonly List<Task> _loadingTasks = new List<Task>();
        private readonly ProgressCollection _progress = new ProgressCollection();
        private uint _connectionId = 0;

        /// <summary>
        /// Get the total loading progress
        /// </summary>
        public float Progress
        {
            get
            {
                return _progress.Progress;
            }
        }

        public RemoteObjectFactoryService(string name, uint priority, BaseMixedRealityProfile profile) : base(name, priority, profile)
        {
            _remoteObjectFactoryServiceProfile = profile as RemoteObjectFactoryServiceProfile;

            if (_remoteObjectFactoryServiceProfile == null)
            {
                _remoteObjectFactoryServiceProfile = ScriptableObject.CreateInstance<RemoteObjectFactoryServiceProfile>();
            }
        }

        public override void Initialize()
        {
            // Listen for connection changes
            if (AppServices.RemoteRendering != null)
            {
                AppServices.RemoteRendering.StatusChanged += RemoteRendering_StatusChanged;
            }
        }


        public override void Destroy()
        {
            base.Destroy();
            if (AppServices.RemoteRendering != null)
            {
                AppServices.RemoteRendering.StatusChanged -= RemoteRendering_StatusChanged;
            }
        }

        public ModelProgressStatus Load(LoadModelFromSASParams loadModelParams)
        {
            var machine = AppServices.RemoteRendering?.PrimaryMachine;

            if (machine == null)
            {
                var msg = $"Unable to load model: this is no remote rendering session. (url = {loadModelParams.ModelUrl})";
                Debug.LogError(msg);
                AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
                return null;
            }

            if (machine.Session.Connection.ConnectionStatus != ConnectionStatus.Connected)
            {
                var msg = $"Unable to load model: manager is not connected. (url = {loadModelParams.ModelUrl})";
                Debug.LogError(msg);
                AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
                return null;
            }

            ModelProgressStatus progressTask = new ModelProgressStatus();
            ScaleLoad(loadModelParams, progressTask);
            return progressTask;
        }

        private async void ScaleLoad(LoadModelFromSASParams loadModelParams, ModelProgressStatus progressTask)
        {
            var machine = AppServices.RemoteRendering?.PrimaryMachine;

            // Remember the current connection, so we can cancel the load on a new connection
            uint connectionId = _connectionId;

            LoadModelAsync loadOperation = null;
            _progress.Add(progressTask);

            while (true)
            {
                if (machine == null)
                {
                    var msg = $"Unable to load model: there is no remote rendering session. (url = {loadModelParams.ModelUrl})";
                    Debug.LogError(msg);
                    AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
                    break;
                }

                lock (_loadingTasks)
                {
                    if (_loadingTasks.Count == 0 || 
                        _loadingTasks.Count < _remoteObjectFactoryServiceProfile.ConcurrentModelLoads)
                    {
                        loadOperation = machine.Actions.LoadModelAsyncAsOperation(loadModelParams);
                        break;
                    }
                }

                await Task.WhenAll(_loadingTasks);

                lock (_loadingTasks)
                {
                    _loadingTasks.Clear();
                }
            }

            if (loadOperation != null)
            {
                if (_connectionId != connectionId)
                {
                    progressTask.Start(null);
                }
                else
                {
                    progressTask.Start(loadOperation);
                    _loadingTasks.Add(IgnoreFailure(progressTask.Result));
                }
            }
        }

        /// <summary>
        /// Handle and ignore load failures. Load failures will be handled by consumers of the factory.
        /// </summary>
        private async Task IgnoreFailure(Task<LoadModelResult> task)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                AppServices.AppNotificationService.RaiseNotification("Unable to load model: " + ex.Message, AppNotificationType.Error);
            }
        }

        private void RemoteRendering_StatusChanged(object sender, IRemoteRenderingStatusChangedArgs args)
        {
            // Cancel loads if now longer connected
            if (args.OldStatus == RemoteRenderingServiceStatus.SessionReadyAndConnected)
            {
                _connectionId++;
                _loadingTasks.Clear();
                _progress.Clear();
            }
        }
	}
}
