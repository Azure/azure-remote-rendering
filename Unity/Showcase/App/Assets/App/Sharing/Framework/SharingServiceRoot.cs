// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions
{
    /// <summary>
    /// The sharing service's avatars will be placed relative to this transform.
    /// </summary>
    public class SharingServiceRoot : MonoBehaviour
    {
        LogHelper<SharingServiceRoot> _log = new LogHelper<SharingServiceRoot>();
        bool _shouldCreateNewAddress = false;

        #region Serialized Fields
        [SerializeField]
        [Tooltip("This is the movable anchor whose transform is shared with the other players as the sharing root. The anchor id from this movable anchor will be shared.")]
        private MovableAnchor anchor;

        /// <summary>
        /// This is the movable anchor whose transform is shared with the other players as the sharing root. The anchor id from this movable anchor will be shared.
        /// </summary>
        public MovableAnchor Anchor
        {
            get => anchor;
            set => anchor = value;
        }

        [SerializeField]
        [Tooltip("The component that'll show and hide local sharing addresses. Addresses will be turned on when the 'anchor' is moving.")]
        private SharingServiceShowAllAddresses showAddresses = null;

        /// <summary>
        /// The component that'll show and hide local sharing addresses. Addresses will be turned on when the 'anchor' is moving.
        /// </summary>
        public SharingServiceShowAllAddresses ShowAddresses
        {
            get => showAddresses;
            set => showAddresses = value;
        }
        #endregion Serialized Fields

        #region MonoBehavior Functions
        private void Awake()
        {
            if (showAddresses == null)
            {
                showAddresses = GetComponent<SharingServiceShowAllAddresses>();
            }

            if (showAddresses != null)
            {
                showAddresses.Selected += HandleAddressSelected;
            }

            if (anchor == null)
            {
                anchor = GetComponentInParent<MovableAnchor>();
            }

            if (anchor != null)
            {
                anchor.ApplyAnchor(new SharingServiceRootAddress(AppServices.SharingService));
                anchor.Moving.AddListener(HandleAnchorMoving);
                anchor.MovingEnding.AddListener(HandleAnchorMoved);
            }
        }

        private void OnDestroy()
        {
            if (showAddresses != null)
            {
                showAddresses.Selected += HandleAddressSelected;
            }

            if (anchor != null)
            {
                anchor.Moving.RemoveListener(HandleAnchorMoving);
                anchor.MovingEnding.RemoveListener(HandleAnchorMoved);
            }
        }
        #endregion MonoBehavior Functions

        #region Private Methods
        /// <summary>
        /// Invoked when the local user selected an existing address. If a user selects an
        /// existing address, don't create a new address
        /// </summary>
        private void HandleAddressSelected(SharingServiceShowAllAddresses sendering, SharingServiceAddress address)
        {
            _log.LogVerbose("Address selected.");

            _shouldCreateNewAddress = false;
            AppServices.SharingService.SetAddress(address);
        }

        /// <summary>
        /// Invoked when the local user starts moving this object
        /// </summary>
        private void HandleAnchorMoving()
        {
            _log.LogVerbose("Sharing root is moving.");

            _shouldCreateNewAddress = true;
            if (showAddresses != null)
            {
                showAddresses.VisibleAddresses = true;
            }
        }

        /// <summary>
        /// Invoked when the local user stops moving this object
        /// </summary>
        private void HandleAnchorMoved()
        {
            _log.LogVerbose("Sharing root has moved.");

            if (showAddresses != null)
            {
                showAddresses.VisibleAddresses = false;
            }

            if (_shouldCreateNewAddress)
            {
                _shouldCreateNewAddress = false;
                AppServices.SharingService.CreateAddress();
            }
        }
        #endregion Private Methods
    }
}
