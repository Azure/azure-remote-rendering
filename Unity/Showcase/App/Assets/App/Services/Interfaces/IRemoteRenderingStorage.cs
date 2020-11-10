// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;

/// <summary>
/// Represents Azure storage APIs for obtain remote models.
/// </summary>
public interface IRemoteRenderingStorage 
{
    /// <summary>
    /// Query an Azure container for all Azure Remote Rendering models. This uses the configured storage account id and key.
    /// </summary>
    /// <param name="containerName">
    /// The name of the container to query. If null or empty, the default container name is used.
    /// </param>
    /// <returns>
    /// An array of remote model containers that represent the ARR models within the given container.
    /// </returns>
    Task<RemoteContainer[]> QueryModels(string containerName = null);
}
