// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;

public static class XmlHelper 
{
    #region Public Functions
    public static Task<string> Serialize<T>(T data) where T : class
    {
        return Task.Run(() =>
        {
            string result = null;
            XmlSerializer xml = new XmlSerializer(typeof(T));
            using (StringWriter textWriter = new StringWriter())
            {
                xml.Serialize(textWriter, data);
                result = textWriter.ToString();
            }
            return result;
        });
    }

    public static Task<T> Deserialize<T>(string value) where T : class
    {
        return Task.Run(() =>
        {
            T result = null;
            XmlSerializer xml = new XmlSerializer(typeof(T));
            using (StringReader textReader = new StringReader(value))
            {
                result = xml.Deserialize(textReader) as T;
            }
            return result;
        });
    }
    #endregion Public Functions
}

