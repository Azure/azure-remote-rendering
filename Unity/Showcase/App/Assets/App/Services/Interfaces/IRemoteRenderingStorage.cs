// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;

/// <summary>
/// Represents Azure storage APIs for obtain remote models.
/// </summary>
public interface IRemoteRenderingStorage 
{
    /// <summary>
    /// Query an Azure container for all Azure Remote Rendering models. This uses the configured storage account id, container and authentication.
    /// </summary>
    /// <returns>
    /// An array of remote model containers that represent the ARR models within the configured storage account.
    /// </returns>
    Task<RemoteContainer[]> QueryModels();

    /// <summary>
    /// Loads a blob from the configured storage account and container.
    /// </summary>
    /// <param name="blobUrl">Absolute URL to the blob inside the default storage container.</param>
    /// <returns>A byte array containing the file content</returns>
    Task<byte[]> LoadBlob(string blobUrl);
}
