// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

public class BuildAppUriHandler
{
    [PostProcessBuild(1)]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        // Find the appxmanifest, assume the one we want is the first one
        string[] manifests = Directory.GetFiles(
            pathToBuiltProject, 
            "Package.appxmanifest", 
            SearchOption.AllDirectories);

        if (manifests != null && manifests.Length > 0)
        {
            XAppUriHandler xAppUriHandler = new XAppUriHandler();
            try
            {
                xAppUriHandler.Update(manifests[0]);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to add AppUriHandler to app manifest. Exception: {ex}");
            }
        }
    }

    private class XAppUriHandler
    {
        private XNamespace _defaultNamespace = null;
        private XNamespace _uap3Namespace = null;

        public XAppUriHandler()
        {
        }

        public void Update(string manifestFile)
        {
            var rootNode = XElement.Load(manifestFile);
            if (rootNode == null)
            {
                return;
            }

            _defaultNamespace = rootNode.GetDefaultNamespace();
            _uap3Namespace = rootNode.GetNamespaceOfPrefix("uap3");

            var applicationTag = "Application";
            var idAttribute = "Id";
            var idAttributeValue = "App";

            var applicationNode = rootNode.Descendants(_defaultNamespace + applicationTag).FirstOrDefault(
                el => (string)el?.Attribute(idAttribute) == idAttributeValue);

            if (applicationNode != null)
            {
                ModifyApplicationNode(applicationNode);
                rootNode.Save(manifestFile);
            }
        }

        private void ModifyApplicationNode(XElement applicationNode)
        {
            var defaultNamespace = applicationNode.GetDefaultNamespace();
            var extensionsTag = "Extensions";

            var extensions = applicationNode.Descendants(defaultNamespace + extensionsTag).FirstOrDefault();
            if (extensions == null)
            {
                extensions = new XElement(defaultNamespace + extensionsTag);
                applicationNode.Add(extensions);
            }

            ModifyExtensionsNode(extensions);
        }

        private void ModifyExtensionsNode(XElement extensionsNode)
        {
            var extensionTag = "Extension";
            var categoryAttribute = "Category";
            var categoryAttributeValue = "windows.appUriHandler";

            var extensionNode = extensionsNode.Descendants(_uap3Namespace + extensionTag).FirstOrDefault(
                el => (string)el?.Attribute(categoryAttribute) == categoryAttributeValue);

            if (extensionNode == null)
            {
                extensionNode = new XElement(_uap3Namespace + extensionTag);
                extensionNode.Add(new XAttribute(categoryAttribute, categoryAttributeValue));
                AddAppUriHandler(extensionNode);
                extensionsNode.Add(extensionNode);
            }
        }

        private void AddAppUriHandler(XElement extensionNode)
        {
            var appUriHandlerTag = "AppUriHandler";
            var hostTag = "Host";
            var nameAttribute = "Name";
            var nameAttributeValue = "white-hill-0d765441e.azurestaticapps.net";

            var appUriHandler = new XElement(_uap3Namespace + appUriHandlerTag);
            var host = new XElement(_uap3Namespace + hostTag);
            var name = new XAttribute(nameAttribute, nameAttributeValue);

            host.Add(name);
            appUriHandler.Add(host);
            extensionNode.Add(appUriHandler);
        }
    }
}
#endif