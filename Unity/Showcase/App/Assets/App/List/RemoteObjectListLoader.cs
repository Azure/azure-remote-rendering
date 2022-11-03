// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.Storage;
using Microsoft.MixedReality.Toolkit.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// This populates the model menu with a list of Azure Remote Rendering (ARR) models. The source of this data can 
/// originate from one of the following locations. This class will attempt to load data starting with #1. If #1 fails,
/// it'll try #2 and soon on.
///
/// 1. An override file placed in the app's local state directory. This file is an index, where each entry points to
///    to an ARR model. If this override file is used, all models referenced by it are added to the model menu.
///    
/// 2. An Azure storage container. All ARR models in this container are added to the model menu. If the container has a
///    models.xml index file, the files referenced by this index also added to the model menu.
///
/// 3. A remote index file, specified via an URL. Each entry points to ARR model. If this remote index file is used,
///    all models referenced by it are added to the model menu.
///
/// 4. A fallback file placed in the app's local state directory. This file is an index, where each entry points to
///    to ARR model. If this fallback file is used, all models referenced by it are added to the model menu.
///
/// 5. A fallback object, specified via a RemoteObjectDataList object. If this fallback is used, all models referenced
///    by it are added to the model menu.
/// </summary>
public class RemoteObjectListLoader : MonoBehaviour
{
    private bool _pendingLoad;

    #region Serialized Fields
    [Header("Parts")]

    [SerializeField]
    [Tooltip("The list target for the loaded data.")]
    private ListItemRepeater target = null;

    /// <summary>
    /// The list target for the loaded data.
    /// </summary>
    public ListItemRepeater Target
    {
        get => target;
        set => target = value;
    }
    
    [Header("Models")]

    [SerializeField]
    [Tooltip("The xml file name to import data from. First attempt is to load models from this.")]
    private string overrideFileName = "models.xml";

    /// <summary>
    /// The xml file name to import data from. First attempt is to load models from this. 
    /// </summary>
    public string OverrideFileName
    {
        get => overrideFileName;
        set => overrideFileName = value;
    }

    [SerializeField]
    [Tooltip("Should the configured azure container be queried for models.")]
    private bool useAzureContainerQuery = true;

    /// <summary>
    /// Should the configured azure container be queried for models.
    /// </summary>
    public bool UseAzureContainerQuery
    {
        get => useAzureContainerQuery;
        set => useAzureContainerQuery = value;
    }
    
    [SerializeField]
    [Tooltip("Should all model sources be combined when querying for models instead of picking the first that returns any data.")]
    private bool combineAllModelSources = true;

    /// <summary>
    /// Should all model sources be combined when querying for models instead of picking the first that returns any data.
    /// </summary>
    public bool CombineAllModelSources
    {
        get => combineAllModelSources;
        set => combineAllModelSources = value;
    }

    [SerializeField]
    [Tooltip("The xml file URL to download data from. Second attempt is to load models from this. Used if 'Override File Name' doesn't exist or fails to load.")]
    private string cloudFileUrl = "";

    /// <summary>
    /// The xml file URL to download data from. Second attempt is to load models from this. Used if 'Override File
    /// Name' doesn't exist or fails to load.
    /// </summary>
    public string CloudFileUrl
    {
        get => cloudFileUrl;
        set => cloudFileUrl = value;
    }

    [SerializeField]
    [Tooltip("The fallback xml file name to import data from. Third attempt is to load models from this. Used if 'Override File Name' and 'Cloud File' don't exist or fail to load.")]
    private string fallbackFileName = "models.fallback.xml";

    /// <summary>
    /// The fallback xml file name to import data from. Third attempt is to load models from this. Used if 'Override 
    /// File Name' and 'Cloud File' don't exist or fail to load.
    /// </summary>
    public string FallbackFileName
    {
        get => fallbackFileName;
        set => fallbackFileName = value;
    }

    [SerializeField]
    [Tooltip("The default list of remote objects to choose from. Fourth attempt is to load models from this. Used if all other options failed.")]
    private RemoteObjectDataList fallbackData;

    /// <summary>
    /// The default list of remote objects to choose from.  Fourth attempt is to load models from this. Used if all 
    /// other options failed.
    /// </summary>
    public RemoteObjectDataList FallbackData
    {
        get => fallbackData;
        set => fallbackData = value;
    }
    #endregion Serialized Fields

    #region Public Properties
    /// <summary>
    /// The xml file name path to import data from.
    /// </summary>
    public string OverrideFilePath { get; private set; }

    /// <summary>
    /// The fallback xml file name to import data from. Used if 'Override File Path' and 'Cloud File' don't exist or fail to load.
    /// </summary>
    public string FallbackFilePath { get; private set; }
    #endregion Public Properties

    #region MonoBehavior Methods
    private void Start()
    {
        if (target == null)
        {
            target = GetComponent<ListItemRepeater>();
        }

        OverrideFilePath = Application.persistentDataPath + "/" + overrideFileName;
        FallbackFilePath = Application.persistentDataPath + "/" + fallbackFileName;

        ConvertModelService.OnModelConversionSuccess += DelayLoadData;
        DelayLoadData();
    }

    private void OnEnable()
    {
        DelayLoadData();
    }

    private void OnDestroy()
    {
        ConvertModelService.OnModelConversionSuccess -= DelayLoadData;
    }
    #endregion MonoBehavior Methods

    #region Public Methods
    #endregion Public Methods

    #region Private Methods
    private bool DataEmpty(RemoteModelFile fileData)
    {
        return fileData == null || fileData.Containers == null || fileData.Containers.Length == 0;
    }

    private void DelayLoadData()
    {
        _pendingLoad = true;
        StartCoroutine(DelayLoadDataIfPending());
    }

    private IEnumerator DelayLoadDataIfPending()
    {
        yield return 0;
        if (_pendingLoad)
        {
            LoadData();
        }
    }

    private async void LoadData()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        _pendingLoad = false;
        RemoteModelFile combinedFileData = new RemoteModelFile();
        
        // Load from Override
        RemoteModelFile fileData = await TryLoadFromOverride();
        CombineData(fileData, ref combinedFileData);

        // Load from Azure container
        if ((DataEmpty(fileData) || combineAllModelSources) && useAzureContainerQuery)
        {
            fileData = await TryLoadFromAzureContainer();
            CombineData(fileData, ref combinedFileData);
        }

        if (DataEmpty(fileData) || combineAllModelSources)
        {
            fileData = await TryLoadFromCloud();
            CombineData(fileData, ref combinedFileData);
        }

        var includeDefaultModels = AppServices.RemoteRendering?.LoadedProfile?.AlwaysIncludeDefaultModels ?? false;
        if (DataEmpty(fileData) || combineAllModelSources || includeDefaultModels)
        {
            // Try to load fallback from XML
            fileData = await TryLoadFromFallback();
            // Save fallback data to file, so consumers see a sample of the file format
            if (DataEmpty(fileData) && TryLoadFromFallbackData(out fileData))
            {
                await TrySave(fileData);
            }
            CombineData(fileData, ref combinedFileData);
        }

        List<object> sortedData = null;
        if (combinedFileData != null)
        {
            sortedData = await FilterCopyAndSort(combinedFileData);
        }

        ApplyData(sortedData);
    }

    private async Task<RemoteModelFile> TryLoadFromOverride()
    {
        RemoteModelFile fileData = null;
        try
        {
            fileData = await LocalStorageHelper.Load<RemoteModelFile>(OverrideFilePath);
        }
        catch (Exception ex)
        {
            var msg = $"Failed to load remote model data from override file '{OverrideFilePath}'. Reason: {ex.Message}";
            AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  msg);
        }

        return IsEmpty(fileData) ? null : fileData;
    }

    private async Task<RemoteModelFile> TryLoadFromCloud()
    {
        RemoteModelFile fileData = null;
        try
        {
            fileData = await AzureStorageHelper.Get<RemoteModelFile>(cloudFileUrl);
        }
        catch (Exception ex)
        {
            var msg = $"Failed to load remote model data from cloud URL '{cloudFileUrl}'. Reason: {ex.Message}";
            AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  msg);
        }

        return IsEmpty(fileData) ? null : fileData;
    }

    private async Task<RemoteModelFile> TryLoadFromFallback()
    {
        RemoteModelFile fileData = null;
        try
        {
            fileData = await LocalStorageHelper.Load<RemoteModelFile>(FallbackFilePath);
        }
        catch (Exception ex)
        {
            var msg = $"Failed to load remote model data from fallback file '{FallbackFilePath}'. Reason: {ex.Message}";
            AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  msg);
        }

        return IsEmpty(fileData) ? null : fileData;
    }

    private bool TryLoadFromFallbackData(out RemoteModelFile fileData)
    {
        Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}",  "No file data, using fallback data.");
        fileData = new RemoteModelFile();

        if (fallbackData != null && fallbackData.Objects != null)
        {
            int items = fallbackData.Objects.Length;
            fileData.Containers = new RemoteContainer[items];
            for (int i = 0; i < items; i++)
            {
                var container = new RemoteContainer();
                container.Items = new RemoteItemBase[1]
                {
                    fallbackData.Objects[i].Model
                };
                container.Name = container.Items[0].Name;
                container.Transform.Center = true;
                fileData.Containers[i] = container;
            }
        }

        return !IsEmpty(fileData);
    }

    private async Task<RemoteModelFile> TryLoadFromAzureContainer()
    {
        RemoteModelFile result = new RemoteModelFile();
        try
        {
            App.Authentication.AADAuth.PrepareOnMainThread();
            result.Containers = await AppServices.RemoteRendering.Storage.QueryModels();
        }
        catch (Exception ex)
        {
            var msg = $"Failed to load remote model data from Azure container. Reason: {ex.Message}";
            AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  msg);
        }
        return result;
    }

    private async Task TrySave(RemoteModelFile fileData)
    {
        try
        {
            await LocalStorageHelper.Save(FallbackFilePath, fileData);
        }
        catch (Exception ex)
        {
            var msg = $"Failed to save remote model data to file. Reason: {ex.Message}";
            AppServices.AppNotificationService.RaiseNotification(msg, AppNotificationType.Error);
            Debug.LogFormat(LogType.Error, LogOption.NoStacktrace, null, "{0}",  msg);
        }
    }

    private bool IsEmpty(RemoteModelFile fileData)
    {
        if (fileData?.Containers == null || 
            fileData.Containers.Length == 0)
        {
            return true;
        }

        bool foundEnabled = false;
        foreach (var container in fileData.Containers)
        {
            if (container != null)
            {
                foundEnabled = container.Enabled;
            }

            if (foundEnabled)
            {
                break;
            }
        }

        return !foundEnabled;
    }

    private async Task<List<object>> FilterCopyAndSort(RemoteModelFile fileData)
    {
        if (IsEmpty(fileData))
        {
            return null;
        }

        return await Task.Run(() =>
        {
            List<object> sorted = new List<object>(fileData.Containers.Length);
            foreach (var container in fileData.Containers)
            {
                if (container != null && 
                    container.Enabled)
                {
                    sorted.Add(container);
                }
            }
            sorted.Sort(CompareContainersByName);
            return sorted;
        });
    }
    
    private void CombineData(RemoteModelFile fileData, ref RemoteModelFile combinedFileData)
    {
        if (IsEmpty(fileData))
        {
            return;
        }

        combinedFileData.Containers = combinedFileData.Containers.Concat(fileData.Containers).ToArray();
    }

    private static int CompareContainersByName(object container1, object container2)
    {
        return StringComparer.InvariantCultureIgnoreCase.Compare(((RemoteContainer)container1).Name, ((RemoteContainer)container2).Name);
    }

    private void ApplyData(List<object> objectData)
    {
        if (objectData == null)
        {
            return;
        }

        if (target != null)
        {
            target.DataSource = objectData;
        }
    }
    #endregion Private Methods
}
