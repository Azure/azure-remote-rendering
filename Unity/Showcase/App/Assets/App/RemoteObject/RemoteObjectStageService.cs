// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
	[MixedRealityExtensionService((SupportedPlatforms)(-1))]
	public class RemoteObjectStageService : BaseExtensionService, IRemoteObjectStageService, IMixedRealityExtensionService
	{
		private TaskCompletionSource<IRemoteObjectStage> _stage = new TaskCompletionSource<IRemoteObjectStage>();

		public RemoteObjectStageService(string name,  uint priority,  BaseMixedRealityProfile profile) : base(name, priority, profile) 
		{
		}

		#region IRemoteObjectStageService Functions
		/// <summary>
		/// Register the main object stage. There can be only one object stage.
		/// </summary>
		public void SetRemoteStage(IRemoteObjectStage stage)
        {
			_stage.TrySetResult(stage);
		}

		/// <summary>
		/// Wait to get the main object stage. This will return once something calls SetRemoteStage(...)
		/// </summary>
		public Task<IRemoteObjectStage> GetRemoteStage()
        {
			return _stage.Task;
		}
		#endregion IRemoteObjectStageService Functions


		#region IMixedRealityExtensionService Functions
		#endregion IMixedRealityExtensionService Functions
	}
}
