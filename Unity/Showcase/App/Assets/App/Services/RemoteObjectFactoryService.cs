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

        /// <summary>
        /// Get if there are models currently being loaded.
        /// </summary>
        public bool IsLoading { get; private set; }


        /// <summary>
        /// Event raised when loading of models has started.
        /// </summary>
        public event Action<IRemoteObjectFactoryService> LoadStarted;


        /// <summary>
        /// Event raised when loading of models has completed.
        /// </summary>
        public event Action<IRemoteObjectFactoryService> LoadCompleted;

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

            _progress.ProgressChanged += ProgressChanged;
            _progress.Completed += ProgressCompleted;
        }

        public override void Destroy()
        {
            base.Destroy();
            if (AppServices.RemoteRendering != null)
            {
                AppServices.RemoteRendering.StatusChanged -= RemoteRendering_StatusChanged;
            }

            _progress.ProgressChanged -= ProgressChanged;
            _progress.Completed -= ProgressCompleted;
        }

        public Task<LoadModelResult> Load(RemoteModel model, Entity parent)
        {
            var machine = AppServices.RemoteRendering?.PrimaryMachine;

            if (machine == null)
            {
                var msg = $"Unable to load model: this is no remote rendering session. (url = {model.Url})";
                Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  msg);
                AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
                return null;
            }

            if (machine.Session.Connection.ConnectionStatus != ConnectionStatus.Connected)
            {
                var msg = $"Unable to load model: manager is not connected. (url = {model.Url})";
                Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  msg);
                AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
                return null;
            }

            ModelProgressStatus progressTask = new ModelProgressStatus();
            var loadModelTask = ScaleLoad(model, parent, progressTask);
            return loadModelTask;
        }

        private async Task<LoadModelResult> ScaleLoad(RemoteModel model, Entity parent, ModelProgressStatus progressTask)
        {
            var machine = AppServices.RemoteRendering?.PrimaryMachine;

            // Remember the current connection, so we can cancel the load on a new connection
            uint connectionId = _connectionId;

            Task<LoadModelResult> loadOperation = null;
            _progress.Add(progressTask);

            while (true)
            {
                if (machine == null)
                {
                    var msg = $"Unable to load model: there is no remote rendering session. (url = {model.Url})";
                    Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  msg);
                    AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
                    break;
                }

                lock (_loadingTasks)
                {
                    if (_loadingTasks.Count == 0 || 
                        _loadingTasks.Count < _remoteObjectFactoryServiceProfile.ConcurrentModelLoads)
                    {
                        loadOperation = machine.Actions.LoadModelAsyncAsOperation(model, parent, progressTask);
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
                }
                else
                {
                    _loadingTasks.Add(IgnoreFailure(loadOperation));
                }
            }
            return await loadOperation;
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

        private void ProgressCompleted(object sender, ProgressTaskChangeArgs args)
        {
            if (IsLoading)
            {
                IsLoading = false;
                LoadCompleted?.Invoke(this);
            }
        }

        private void ProgressChanged(object sender, ProgressTaskChangeArgs args)
        {
            if (!IsLoading && args.NewValue > 0.0f && args.NewValue < 1.0f)
            {
                IsLoading = true;
                LoadStarted?.Invoke(this);
            }
        }
    }
}
