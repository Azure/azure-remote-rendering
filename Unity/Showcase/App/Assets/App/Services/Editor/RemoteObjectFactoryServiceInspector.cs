// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Editor;

namespace Microsoft.MixedReality.Toolkit.Extensions.Editor
{	
    [MixedRealityServiceInspector(typeof(RemoteObjectFactoryService))]
    public class RemoteObjectFactoryServiceInspector : BaseMixedRealityServiceInspector
    {
        public override void DrawInspectorGUI(object target)
        {
            RemoteObjectFactoryService service = (RemoteObjectFactoryService)target;            
            // Draw inspector here
        }
    }
}