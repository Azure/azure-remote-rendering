// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
	public interface IRemoteObjectStageService : IMixedRealityExtensionService
	{
		/// <summary>
		/// Register the main object stage. There can be only one object stage.
		/// </summary>
		void SetRemoteStage(IRemoteObjectStage stage);

		/// <summary>
		/// Wait to get the main object stage. This will return once something calls SetRemoteStage(...)
		/// </summary>
		Task<IRemoteObjectStage> GetRemoteStage();
	}
}