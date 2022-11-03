// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using Microsoft.MixedReality.Toolkit.Editor;

namespace Microsoft.MixedReality.Toolkit.Extensions.Editor
{	
	[MixedRealityServiceInspector(typeof(IRemoteObjectStageService))]
	public class RemoteObjectStageServiceInspector : BaseMixedRealityServiceInspector
	{
		public override void DrawInspectorGUI(object target)
		{
		}
	}
}
#endif