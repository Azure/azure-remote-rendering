// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    [MixedRealityServiceProfile(typeof(IRemoteRenderingService))]
    public abstract class BaseRemoteRenderingServiceProfile : BaseMixedRealityProfile, IRemoteRenderingServiceProfile
    {
        public abstract float MaxLeaseTime { get; set; }
        public abstract TimeSpan MaxLeaseTimespan { get; set; }
        public abstract string PreferredDomain { get; set; }
        public abstract string UnsafeSizeOverride { get; set; }
        public abstract RenderingSessionVmSize Size { get; set; }
        public abstract string SessionOverride { get; set; }
        public abstract bool AutoReconnect { get; set; }
        public abstract float AutoReconnectRate { get; set; }
        public abstract bool AlwaysIncludeDefaultModels { get; set; }
        public abstract bool AutoRenewLease { get; set; }
        public abstract string StorageAccountName { get; set; }
        public abstract string StorageModelContainer { get; set; }
        public abstract bool StorageModelPathByUsername { get; set; }
        public abstract RemoteRenderingServiceRegion[] RemoteRenderingDomains { get; set; }
        public abstract BaseStorageAccountData StorageAccountData { get; }
        public abstract AuthenticationType AuthType { get; }
        public abstract Task<RemoteRenderingClient> GetClient(string domain);
        public abstract bool ValidateProfile(out string validateMessages);
    }

    [Serializable]
    public struct RemoteRenderingServiceRegion
    {
        [Tooltip("The friendly name of the region.")]
        public string Label;

        [Tooltip("The id value of the region")]
        public string Value;

        [Tooltip("The url domain of the region")]
        public string Domain;

        public RemoteRenderingServiceRegionValue ValueEnum
        {
            get
            {
                RemoteRenderingServiceRegionValue result;
                if (!Enum.TryParse(Value, out result))
                {
                    result = RemoteRenderingServiceRegionValue.error;
                }
                return result;
            }

            set
            {
                Value = value.ToString();
            }
        }

        public bool ShouldSerializeValueEnum()
        {
            return false;
        }

        public static readonly RemoteRenderingServiceRegion[] Defaults = new RemoteRenderingServiceRegion[]
        {
            new RemoteRenderingServiceRegion { Label = "West US 2", ValueEnum = RemoteRenderingServiceRegionValue.westus2, Domain = "westus2.mixedreality.azure.com" },
            new RemoteRenderingServiceRegion { Label = "Australia East", ValueEnum = RemoteRenderingServiceRegionValue.australiaeast, Domain = "australiaeast.mixedreality.azure.com" },
            new RemoteRenderingServiceRegion { Label = "East US", ValueEnum = RemoteRenderingServiceRegionValue.eastus, Domain = "eastus.mixedreality.azure.com" },
            new RemoteRenderingServiceRegion { Label = "East US 2", ValueEnum = RemoteRenderingServiceRegionValue.eastus2, Domain = "eastus2.mixedreality.azure.com" },
            new RemoteRenderingServiceRegion { Label = "Japan East", ValueEnum = RemoteRenderingServiceRegionValue.japaneast, Domain = "japaneast.mixedreality.azure.com" },
            new RemoteRenderingServiceRegion { Label = "North Europe", ValueEnum = RemoteRenderingServiceRegionValue.northeurope, Domain = "northeurope.mixedreality.azure.com" },
            new RemoteRenderingServiceRegion { Label = "South Central US", ValueEnum = RemoteRenderingServiceRegionValue.southcentralus, Domain = "southcentralus.mixedreality.azure.com" },
            new RemoteRenderingServiceRegion { Label = "Southeast Asia", ValueEnum = RemoteRenderingServiceRegionValue.southeastasia, Domain = "southeastasia.mixedreality.azure.com" },
            new RemoteRenderingServiceRegion { Label = "UK South", ValueEnum = RemoteRenderingServiceRegionValue.uksouth, Domain = "uksouth.mixedreality.azure.com" },
            new RemoteRenderingServiceRegion { Label = "West Europe", ValueEnum = RemoteRenderingServiceRegionValue.westeurope, Domain = "westeurope.mixedreality.azure.com" },
        };

        static RemoteRenderingServiceRegion()
        {
            Debug.Assert(
                Defaults.Length == (int)RemoteRenderingServiceRegionValue.count,
                "[RemoteRenderingServiceRegion] The default regions array needs to be updated so it includes all the RemoteRenderingServiceRegionValue enumeration values once.");
        }
    }

    public enum RemoteRenderingServiceRegionValue
    {
        // Do not change the order of these enum values.
        // Changing the order will break assets in Unity scenes.
        westus2,
        eastus,
        westeurope,
        southeastasia,
        australiaeast,
        eastus2,
        japaneast,
        northeurope,
        southcentralus,
        uksouth,

        count,
        error = 255
    };
}
