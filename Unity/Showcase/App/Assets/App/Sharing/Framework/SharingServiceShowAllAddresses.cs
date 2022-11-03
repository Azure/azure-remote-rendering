// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// A behavior for showing and hiding all addresses near the user
    /// </summary>
    public class SharingServiceShowAllAddresses : MonoBehaviour
    {
        LogHelper<SharingServiceShowAllAddresses> _log = new LogHelper<SharingServiceShowAllAddresses>();
        List<GameObject> _addressLocations = new List<GameObject>();
        bool _visible = false;

        #region Serialized Fields
        [SerializeField]
        [Tooltip("The visual to show on each of the addresses")]
        private GameObject addressVisual = null;

        /// <summary>
        /// The visual to show on each of the addresses.
        /// </summary>
        public GameObject AddressVisual
        {
            get => addressVisual;
            set => addressVisual = value;
        }
        #endregion Serialized Fields

        #region Public Properties
        /// <summary>
        /// Show or hide visible addresses. If showing, update the known local address list
        /// </summary>
        public bool VisibleAddresses
        {
            get => _visible;

            set
            {
                if (_visible != value)
                {
                    _visible = value;
                    SetAddressVisibilityOnVisuals(_visible);

                    if (_visible)
                    {
                        AppServices.SharingService.FindAddresses();
                    }
                }
            }
        }

        /// <summary>
        /// Event raised when this address was selected by the user.
        /// </summary>
        public event Action<SharingServiceShowAllAddresses, SharingServiceAddress> Selected;
        #endregion Public Properties

        #region MonoBehavior Functions
        private void OnEnable()
        {
            _log.LogVerbose("Component is enabled");
            UpdateAddresses(AppServices.SharingService.LocalAddresses);
            AppServices.SharingService.LocalAddressesChanged += OnSharingServiceLocalAddressesChanged;
        }

        private void OnDisable()
        {
            _log.LogVerbose("Component is disabled");
            AppServices.SharingService.LocalAddressesChanged -= OnSharingServiceLocalAddressesChanged;
        }

        private void OnDestroy()
        {
            _log.LogVerbose("Component is destroyed");
            DestroyAddressLocations();
        }
        #endregion MonoBehavior Functions

        #region Private Functions
        private void OnSharingServiceLocalAddressesChanged(ISharingService sender, IReadOnlyList<SharingServiceAddress> addresses)
        {
            _log.LogVerbose("Sharing service local addresses have changed.");
            UpdateAddresses(addresses);
        }

        private void UpdateAddresses(IReadOnlyList<SharingServiceAddress> addresses)
        {
            DestroyAddressLocations();
            if (addressVisual == null)
            {
                _log.LogError("No address visualizer has been set.");
            }
            else if (addresses == null)
            {
                _log.LogVerbose("No addresses to visualize.");
            }
            else
            {
                _log.LogVerbose("Creating visuals for addresses. {0}", addresses);
                int count = addresses.Count;
                for (int i = 0; i < count; i++)
                {
                    CreateAddressVisual(addresses[i]);
                }
            }
        }

        private void CreateAddressVisual(SharingServiceAddress address)
        {
            if (address == null)
            {
                _log.LogVerbose("Ignoring address visualization request. It's null.");
            }
            else if (AnchorSupport.IsNativeEnabled && address.Type != SharingServiceAddressType.Anchor)
            {
                _log.LogVerbose("Ignoring address visualization request. It's not an anchor address, on a platform that supports anchors. {0}", address);
            }
            else
            {
                GameObject newObject = Instantiate(addressVisual, transform);
                newObject.name = address.ToString();
                newObject.SetActive(_visible);
                _addressLocations.Add(newObject);
                var visual = newObject.EnsureComponent<SharingServiceAddressVisual>();
                visual.Address = address;
                visual.Selected += OnChildAddressVisualSelected;
                _log.LogVerbose("Created visual for address. {0} (visible: {1}) (activeInHierarchy: {2})", address, _visible, newObject.activeInHierarchy);
            }
        }

        private void DestroyAddressLocations()
        {
            _log.LogVerbose("Destroying all address visualizations.");
            foreach (var location in _addressLocations)
            {
                if (location != null)
                {
                    var visual = location.GetComponent<SharingServiceAddressVisual>();
                    if (visual != null)
                    { 
                        visual.Selected -= OnChildAddressVisualSelected;
                    }
                    Destroy(location);
                }
            }
            _addressLocations.Clear();
        }

        private void SetAddressVisibilityOnVisuals(bool visible)
        {
            _log.LogVerbose("Setting visibility to {0}", visible);
            foreach (var location in _addressLocations)
            {
                if (location != null)
                {
                    _log.LogVerbose("Setting visibility to {0}. {1}", visible, location.name);
                    location.SetActive(visible);
                }
            }
        }

        private void OnChildAddressVisualSelected(SharingServiceAddressVisual sender, SharingServiceAddress address)
        {
            Selected?.Invoke(this, address);
        }
        #endregion Private FUnctions
    }
}
