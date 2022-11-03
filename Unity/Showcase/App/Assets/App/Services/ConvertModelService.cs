// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using App.Authentication;
using Microsoft.Azure.RemoteRendering;
using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

#if ENABLE_WINMD_SUPPORT
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Storage.Pickers;
#endif

public class ConvertModelService : MonoBehaviour
{
    #region Serialized Fields
    /// <summary>
    /// The button to be used to upload and convert a new model for viewing.
    /// </summary>
    public Interactable UploadModelButton;

    #endregion Serialized Fields

    #region Static Events

    /// <summary>
    /// Called when a model has been converted
    /// </summary>
    public static event Action OnModelConversionSuccess;

    #endregion Static Events

    #region Private Fields

    private IRemoteRenderingService _remoteRenderingService;

#if ENABLE_WINMD_SUPPORT
    private FileOpenPicker openPicker;
#endif

    #endregion

    #region MonoBehaviour Functions
    private void Start()
    {
        _remoteRenderingService = AppServices.RemoteRendering;
        if (_remoteRenderingService != null && UploadModelButton != null)
        {
            // Button listener
            UploadModelButton.OnClick.AddListener(SelectModel);
            
            // Remote rendering status
            _remoteRenderingService.StatusChanged += RemoteRendering_StatusChanged;
            RemoteRendering_StatusChanged(_remoteRenderingService.Status);
        }
    }
    #endregion MonoBehaviour Functions

    #region Private Functions
    private void RemoteRendering_StatusChanged(object sender, IRemoteRenderingStatusChangedArgs e)
    {
        RemoteRendering_StatusChanged(e.NewStatus);
    }
    
    private void RemoteRendering_StatusChanged(RemoteRenderingServiceStatus newStatus)
    {
        if (newStatus != RemoteRenderingServiceStatus.Unknown)
        {
            _remoteRenderingService.StatusChanged -= RemoteRendering_StatusChanged;
            // Enable button
            UploadModelButton.IsEnabled = true;
        }
        else
        {
            // Disable button
            UploadModelButton.IsEnabled = false;
        }
    }

    private void SelectModel()
    {
    #if ENABLE_WINMD_SUPPORT
        UnityEngine.WSA.Application.InvokeOnUIThread(()=> SelectFileAsync(), false);
#elif UNITY_EDITOR
        var modelPath = UnityEditor.EditorUtility.OpenFilePanel("Model", Application.streamingAssetsPath, "fbx,glb,bin");
        if (modelPath.Length != 0)
        {
            _ = ConvertModel(modelPath);
        }
#endif
    }

 #if ENABLE_WINMD_SUPPORT
    private async void SelectFileAsync()
    {
        FileOpenPicker openPicker = new FileOpenPicker();
        openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        openPicker.FileTypeFilter.Add(".fbx");
        openPicker.FileTypeFilter.Add(".glb");
        openPicker.FileTypeFilter.Add(".bin");

        StorageFile file = await openPicker.PickSingleFileAsync();
        if (file != null)
        {
            IRandomAccessStream uwpStream = await file.OpenReadAsync();
            Stream stream = uwpStream.AsStream();
            UnityEngine.WSA.Application.InvokeOnAppThread(()=> ConvertModel(file.Path, stream), false);
        }
    }
 #endif

    private async Task ConvertModel(string modelPath, Stream modelStream = null)
    {
        try
        {
            // Settings
            string msg;
            const string inputContainerName = "arrinput";
            const string outputContainerName = "arroutput";
            
            string modelFile = Path.GetFileName(modelPath);
            string outputFile = Path.ChangeExtension(modelFile, "arrAsset");

            msg = $"File selected for conversion: {modelPath}\n{modelFile}.";
            AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Info);
            Debug.Log(msg);

            // Default model folder name
            string folderName = string.Empty;
            
            // ARR Profile
            var loadedProfile = AppServices.RemoteRendering.LoadedProfile;
            
            // Initialize storage
            var storageAccountData = loadedProfile.StorageAccountData;
            StorageCredentials storageCredentials;
            
            if(storageAccountData.AuthType == AuthenticationType.AccountKey)
            {
                string authKey = await loadedProfile.StorageAccountData.GetAuthData();
                storageCredentials = new StorageCredentials(loadedProfile.StorageAccountData.StorageAccountName, authKey);
                if(loadedProfile.StorageModelPathByUsername) folderName = AKStorageAccountData.MODEL_PATH_BY_USERNAME_FOLDER;
            }
            else
            {
                string authToken = await loadedProfile.StorageAccountData.GetAuthData();
                storageCredentials = new StorageCredentials(new TokenCredential(authToken));
                if(loadedProfile.StorageModelPathByUsername) folderName = AADAuth.SelectedAccount.Username;
            }
            var storageAccount = new CloudStorageAccount(storageCredentials, loadedProfile.StorageAccountData.StorageAccountName, null, true);

            // Storage client
            var blobClient = storageAccount.CreateCloudBlobClient();
            
            // Input container
            var inputContainer = blobClient.GetContainerReference(inputContainerName);
            await inputContainer.CreateIfNotExistsAsync();

            // Output container
            var outputContainer = blobClient.GetContainerReference(outputContainerName);
            await outputContainer.CreateIfNotExistsAsync();

            msg = $"Uploading model.";
            AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Info);
            Debug.Log(msg);

            // Upload model
            CloudBlockBlob modelBlockBlob;
            if(loadedProfile.StorageModelPathByUsername)
            {
                var modelFolder = inputContainer.GetDirectoryReference(folderName);
                modelBlockBlob = modelFolder.GetBlockBlobReference(modelFile);
            }
            else
            {
                modelBlockBlob = inputContainer.GetBlockBlobReference(modelFile);
            }
            
            // Upload using path or provided stream
            if(modelStream == null)
            {
                await modelBlockBlob.UploadFromFileAsync(modelPath);
            }
            else
            {
                await modelBlockBlob.UploadFromStreamAsync(modelStream);
            }

            // Conversion parameters
            var inputUri = $"https://{loadedProfile.StorageAccountName}.blob.core.windows.net/{inputContainerName}";
            var outputUri = $"https://{loadedProfile.StorageAccountName}.blob.core.windows.net/{outputContainerName}";

            var inputParams = new AssetConversionInputOptions(inputUri, null, folderName, modelFile);
            var outputParams = new AssetConversionOutputOptions(outputUri, null, folderName, outputFile);
            var options = AssetConversionOptions.CreateForBlobStorage(null, inputParams, outputParams);

            // Azure authentication
            var client = await loadedProfile.GetClient(loadedProfile.PreferredDomain);

            msg = $"Starting conversion.";
            AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Info);
            Debug.Log(msg);

            // Start conversion
            var conversion = client.StartAssetConversionAsync(options);
            await conversion;

            string conversionId;

            // Conversion result
            if (conversion.IsCompleted)
            {
                msg = $"Conversion started.";
                AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Info);
                Debug.Log(msg);
                conversionId = conversion.Result.ConversionUuid;
            }
            else
            {
                msg = $"Error starting conversion.";
                AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
                Debug.LogError(msg);
                return;
            }

            // Poll conversion process
            while (true)
            {
                // Wait 10 seconds
                await Task.Delay(10000);
                // Poll conversion status
                var task = await client.GetAssetConversionStatusAsync(conversionId);
                ConversionSessionStatus status = task.Result;
                if (status == ConversionSessionStatus.Created || status == ConversionSessionStatus.Running)
                {
                    // In progress
                    msg = $"Conversion Session In Progress...";
                    AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Info);
                    Debug.Log(msg);
                }
                else
                {
                    // Done, success/fail
                    switch(status)
                    {
                        case ConversionSessionStatus.Unknown:
                        case ConversionSessionStatus.Aborted:
                        case ConversionSessionStatus.Failure:
                            msg = $"Conversion Session Failed.";
                            AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
                            Debug.LogError(msg);
                            break;
                        case ConversionSessionStatus.Success:
                            msg = $"Conversion Session Completed Successfully.";
                            AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Info);
                            OnModelConversionSuccess?.Invoke();
                            Debug.Log(msg);
                            break;
                    }

                    break;
                }
            }
        }
        catch(Exception e)
        {
            var msg = $"Conversion Process Failed.\n{e}";
            AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
            Debug.LogError(msg);
            Debug.LogError(e.StackTrace);
        }
    }
    #endregion Private Functions
}