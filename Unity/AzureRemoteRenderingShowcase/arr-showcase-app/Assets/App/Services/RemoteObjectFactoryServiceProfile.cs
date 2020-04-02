// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
	[MixedRealityServiceProfile(typeof(IRemoteObjectFactoryService))]
	[CreateAssetMenu(fileName = "RemoteObjectFactoryServiceProfile", menuName = "MixedRealityToolkit/RemoteObjectFactoryService Configuration Profile")]
	public class RemoteObjectFactoryServiceProfile : BaseMixedRealityProfile
	{
        [Tooltip("The max number of models to load at a time")]
        public int ConcurrentModelLoads = 10;
    }
}