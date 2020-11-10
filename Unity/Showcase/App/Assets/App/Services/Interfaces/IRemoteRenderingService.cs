// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
	public interface IRemoteRenderingService : IMixedRealityExtensionService
	{
        /// <summary>
        /// Get the current remote rendering machine
        /// </summary>
        IRemoteRenderingMachine PrimaryMachine { get; }

        /// <summary>
        /// Get the status of the rendering service.
        /// </summary>
        RemoteRenderingServiceStatus Status { get; }

        /// <summary>
        /// Get all the known machines
        /// </summary>
        IReadOnlyCollection<IRemoteRenderingMachine> Machines { get; }

        /// <summary>
        /// Get the storage interface for obtaining the configured account's remote models.
        /// </summary>
        IRemoteRenderingStorage Storage { get; }

        /// <summary>
        /// Get the loaded profile. This is the profile object that also includes overrides from the various override files, as well as the default values.
        /// </summary>
        BaseRemoteRenderingServiceProfile LoadedProfile { get; }

        /// <summary>
        /// A string used for debugging
        /// </summary>
        string DebugStatus { get; }

        /// <summary>
        /// Event raised when current machine changes.
        /// </summary>
        event EventHandler<IRemoteRenderingMachine> PrimaryMachineChanged;

        /// <summary>
        /// Event raised when the status changes.
        /// </summary>
        event EventHandler<IRemoteRenderingStatusChangedArgs> StatusChanged;

        /// <summary>
        /// Reload the settings profile.
        /// </summary>
        Task ReloadProfile();

        /// <summary>
        /// Shut down all known machines, and forget about them
        /// </summary>
        Task StopAll();

        /// <summary>
        /// Clear all known machine, without shutting them down.
        /// </summary>
        Task ClearAll();

        /// <summary>
        /// Start a new Azure Remote Rendering session or connect to the last known session. Once created connect must be called on the session.
        /// </summary>
        Task<IRemoteRenderingMachine> Create();

        /// <summary>
        /// Connect to a known session id. Once created connect must be called on the machine.
        /// </summary>
        Task<IRemoteRenderingMachine> Open(string id);

        /// <summary>
        /// Connect to an existing or new session if not already connected.
        /// </summary>
        Task<IRemoteRenderingMachine> AutoConnect();
    }
}
