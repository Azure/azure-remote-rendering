// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Editor;

namespace Microsoft.MixedReality.Toolkit.Extensions.Editor
{	
    [MixedRealityServiceInspector(typeof(IPointerStateService))]
    public class PointerStateServiceInspector : BaseMixedRealityServiceInspector
    {
        public override void DrawInspectorGUI(object target)
        {
            PointerStateService service = (PointerStateService)target;
            
            // Draw inspector here
        }
    }
}