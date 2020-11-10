// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;

#if !UNITY_EDITOR && WINDOWS_UWP
using System;
using Windows.Storage;
#endif

public static class LocalStorageHelper
{
    public static async Task<TResult> Load<TResult>(string filePath) 
    {
        bool appxFile = filePath?.StartsWith("ms-appx:///") == true;
        TResult result = default;

        if (appxFile || File.Exists(filePath))
        {
            Stream stream = null;
            FileStream file = null;
            try
            {
#if !UNITY_EDITOR && WINDOWS_UWP
                if (filePath.StartsWith("ms-appx:///"))
                {
                    StorageFile storeFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri(filePath));
                    stream = await storeFile.OpenStreamForReadAsync();
                }
#endif

                if (stream == null)
                {
                    file = File.Open(filePath, FileMode.Open, FileAccess.Read);
                    file.Position = 0;
                    stream = new MemoryStream();
                    await file.CopyToAsync(stream);
                    await stream.FlushAsync();
                    stream.Position = 0;
                }

                result = await Task.Run(() =>
                {
                    XmlSerializer xml = new XmlSerializer(typeof(TResult));
                    return (TResult)xml.Deserialize(stream);
                });
            }
            finally
            {
                stream?.Close();
                file?.Close();
            }
        }

        return result;
    }

    public static async Task Save<TResult>(string filePath, TResult data)
    {
        MemoryStream stream = null;
        FileStream file = null;
        try
        {
            stream = new MemoryStream();
            await Task.Run(() =>
            {
                XmlSerializer xml = new XmlSerializer(typeof(TResult));
                xml.Serialize(stream, data);
            });
            stream.Position = 0;

            file = File.Create(filePath);
            await stream.CopyToAsync(file);
            await file.FlushAsync();
        }
        finally
        {
            file?.Close();
            stream?.Close();
        }
    }
}
