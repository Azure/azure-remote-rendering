// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    public class OfflineLocation : SharingServiceLocation, IDisposable
    {
        List<IDisposable> _owenedDisables = new List<IDisposable>();
        object _initializationLock = new object();
        TaskCompletionSource<bool> _initializationTask = null;

        #region Constructor
        private OfflineLocation(
            ISharingServiceRoomAddresses ownedAddresses,
            ISharingServiceAddressFactory ownedFactory,
            ISharingServiceAddressSearchStrategy ownedSearch) : base(ownedAddresses, ownedFactory, ownedSearch)
        {
            if (ownedAddresses is IDisposable)
            {
                _owenedDisables.Add((IDisposable)ownedAddresses);
            }

            if (ownedFactory is IDisposable)
            {
                _owenedDisables.Add((IDisposable)ownedFactory);
            }

            if (ownedSearch is IDisposable)
            {
                _owenedDisables.Add((IDisposable)ownedSearch);
            }
        }
        #endregion Constructor

        #region Protected Methods
        protected override void OnDispose(bool disposing)
        {
            if (disposing && _owenedDisables != null)
            {
                foreach (var dispose in _owenedDisables)
                {
                    dispose.Dispose();
                }
                _owenedDisables = null;
            }
        }
        #endregion Protected Methods

        #region Public Methods
        public static OfflineLocation Create()
        {
            ISharingServiceRoomAddresses rooms = new OfflineRoomAddresses();
            ISharingServiceAddressFactory factory = new OfflineAnchorFactory();
            ISharingServiceAddressSearchStrategy search = new OfflineAnchorSearchStrategy();
            return new OfflineLocation(rooms, factory, search);
        }

        /// <summary>
        /// After a brief delay, try to find and set the default offline address.
        /// </summary>
        public Task Initialize()
        {
            TaskCompletionSource<bool> taskSource;
            lock (_initializationLock)
            {
                if (_initializationTask == null || 
                    _initializationTask.Task.IsFaulted)
                {
                    _initializationTask = new TaskCompletionSource<bool>();
                    DelayInitialization(_initializationTask);
                }
                taskSource = _initializationTask;
            }
            return taskSource.Task;
        }
        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// After a brief delay, try to find and set the default offline address.
        /// </summary>
        private async void DelayInitialization(TaskCompletionSource<bool> taskSource)
        {
            try
            {
                if (AnchorSupport.IsNativeEnabled)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));
                    await TrySetDefaultAddress(allowPrompt: false);
                }
                taskSource.TrySetResult(true);
            }
            catch (Exception ex)
            {
                taskSource.TrySetException(ex);
                throw ex;
            }
        }
        #endregion Private Methods
    }
}
