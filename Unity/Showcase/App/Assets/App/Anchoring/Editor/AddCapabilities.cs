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

public class AddCapabilities
{
    [PostProcessBuild(0)]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        // Find the appxmanifest, assume the one we want is the first one
        string[] manifests = Directory.GetFiles(
            pathToBuiltProject,
            "Package.appxmanifest",
            SearchOption.AllDirectories);

        if (manifests != null && manifests.Length > 0)
        {
            var manifest = manifests[0];
            XCapabilities capabilities = new XCapabilities();
            capabilities.Open(manifest);
            capabilities.AddCapability("wiFiControl", CapabilityType.DeviceCapability);
            capabilities.Close(manifest);
        }
    }

    private enum CapabilityType
    {
        Capability,
        DeviceCapability
    }

    /// <summary>
    /// Sample output
    /// 
    ///  <Capabilities>
    ///      <uap:Capability Name="enterpriseAuthentication" />
    ///      <Capability Name="internetClient" />
	///      <DeviceCapability Name="wiFiControl"/>
    ///  </Capabilities>
    ///  
    /// </summary>
    private class XCapabilities
    {
        private XElement _root = null;
        private XElement _capabilitiesNode = null;
        private XNamespace _defaultNamespace = null;

        public XCapabilities()
        {
        }

        /// <summary>
        /// Open a manifest file to start inserting capabilities
        /// </summary>
        /// <param name="manifestFile">The manifest file to open.</param>
        public void Open(string manifestFile)
        {
            if (_root != null)
            {
                return;
            }

            try
            {
                _root = XElement.Load(manifestFile);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load manifest file ({manifestFile}). Exception: {ex}");
            }

            if (_root == null)
            {
                return;
            }

            _defaultNamespace = _root.GetDefaultNamespace();

            // Find or create "in proc" node of _dllName
            _capabilitiesNode = FindOrCreateCapabilitiesNode();
        }

        /// <summary>
        /// Add an capability class to the opened manifest file.
        /// </summary>
        /// <param name="name">The capability name to insert.</param>
        /// <param name="type">The type of capability.</param>
        public void AddCapability(string name, CapabilityType type)
        {
            if (_root == null)
            {
                Debug.LogError("Manifest file not opened.");
                return;
            }

            AddCapability(name, type.ToString());
        }

        /// <summary>
        /// Close the current manifest file, and save the changes to the given file path
        /// </summary>
        /// <param name="destinationFile">The file path to save manifest files changes to.</param>
        public void Close(string destinationFile)
        {
            if (_root == null)
            {
                return;
            }

            try
            {
                _root.Save(destinationFile);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to save manifest file ({destinationFile}). Exception: {ex}");
            }

            _root = null;
            _capabilitiesNode = null;
        }

        private XElement FindOrCreateCapabilitiesNode()
        {
            var capabilitiesTag = "Capabilities";

            var capabilitiesNode = _root.Element(_defaultNamespace + capabilitiesTag);
            if (capabilitiesNode == null)
            {
                capabilitiesNode = new XElement(_defaultNamespace + capabilitiesTag);
                _root.Add(capabilitiesNode);
            }

            return capabilitiesNode;
        }

        private void AddCapability(string capability, string type)
        {
            var capabilityTag = type;
            var nameAttribute = "Name";

            var capabilityNode = _capabilitiesNode.Descendants(_defaultNamespace + capabilityTag).FirstOrDefault(
                el => (string)el?.Attribute(nameAttribute) == capability);

            if (capabilityNode == null)
            {
                capabilityNode = new XElement(_defaultNamespace + capabilityTag);
                capabilityNode.Add(new XAttribute(nameAttribute, capability));
                _capabilitiesNode.Add(capabilityNode);
            }
        }
    }
}
#endif