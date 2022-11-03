// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    public class SharingServiceLocation : IDisposable
    {
        private bool _disposed;
        private ISharingServiceRoomAddresses _room;
        private ISharingServiceAddressFactory _factory;
        private ISharingServiceAddressSearchStrategy _search;
        private SharingServiceAddress _primaryAddress = null;
        private List<SharingServiceAddress> _localAddresses = new List<SharingServiceAddress>();
        private LogHelper<SharingServiceLocation> _log = new LogHelper<SharingServiceLocation>();
        private CancellationTokenSource _readingCancellationToken = new CancellationTokenSource();
        private CancellationTokenSource _writtingCancellationToken = new CancellationTokenSource();

        /// <summary>
        /// A special state that forces non-HoloLens devices to use an ASA address from other partipants.
        /// </summary>
        private static bool _forceSearch = false;

        #region Constructors
        public SharingServiceLocation(
            ISharingServiceRoomAddresses roomAddresses,
            ISharingServiceAddressFactory addressFactory,
            ISharingServiceAddressSearchStrategy searchStrategy)
        {
            _search = searchStrategy ?? throw new ArgumentNullException("The ISharingServiceAddressSearchStrategy can't be null");
            _factory = addressFactory ?? throw new ArgumentNullException("The ISharingServiceAddressFactory can't be null");
            _room = roomAddresses ?? throw new ArgumentNullException("The ISharingServiceRoomAddresses can't be null");
            _room.ParticipantsChanged += OnPlatformParticipantsChanged;
        }
        #endregion Constructors

        #region Public Properties
        public SharingServiceAddress PrimaryAddress
        {
            get => _primaryAddress;
        }

        public IReadOnlyList<SharingServiceAddress> LocalAddresses => _localAddresses;

        /// <summary>
        /// Get if the local addresses have been loaded once.
        /// </summary>
        public bool LocalAddressesInitialized { get; private set; }

        /// <summary>
        /// Get the name of the location.
        /// </summary>
        public string Name => _room.Name;
        #endregion Public Properties

        #region Public Events
        /// <summary>
        /// Event raised when the location id has changed.
        /// </summary>
        public event Action<SharingServiceLocation, SharingServiceAddress> PrimaryAddressChanged;

        /// <summary>
        /// Event raised when the list of local addresses have changed.
        /// </summary>
        public event Action<SharingServiceLocation, IReadOnlyList<SharingServiceAddress>> LocalAddressesChanged;

        /// <summary>
        /// Event raised when a address's participants have changed
        /// </summary>
        public event Action<SharingServiceLocation, SharingServiceAddress> AddressParticipantsChanged;
        #endregion Public Events

        #region IDisposable
        /// <summary>
        /// Dispose owned resources
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                CancelWriteOperations(disposing: true);
                CancelReadOperations(disposing: true);

                _primaryAddress = null;
                DisposeLocalAddresses();
            }

            OnDispose(!_disposed);
            _disposed = true;
        }
        #endregion IDisposable

        #region Public Functions

        /// <summary>
        /// Try to set a default address for this device.
        /// </summary>
        public async Task TrySetDefaultAddress(bool allowPrompt = true, Transform fallback = null, CancellationToken ct = default(CancellationToken))
        {
            // Check if disposed
            if (_readingCancellationToken == null)
            {
                return;
            }

            CancelWriteOperations();
            CancelReadOperations();
            using (CancellationTokenSource writeTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct, _writtingCancellationToken.Token))
            using (CancellationTokenSource readTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct, _readingCancellationToken.Token))
            {
                if ((await FindAndSetAddresses(allowPrompt, readTokenSource.Token)).Count == 0 && fallback != null)
                {
                    await CreateThenSetAddress(fallback, writeTokenSource.Token);
                }
                else
                {
                    await SetPrimaryAddressWorker(GetDefaultAddress(), writeTokenSource.Token);
                }
            }
        }

        /// <summary>
        /// Search for addresses in the device's physical space.
        /// </summary>
        public async Task FindAddresses(CancellationToken ct = default(CancellationToken))
        {
            // Check if disposed
            if (_readingCancellationToken == null)
            {
                return;
            }

            CancelReadOperations();
            using (CancellationTokenSource readTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct, _readingCancellationToken.Token))
            {
                await FindAndSetAddresses(allowPrompt: false, readTokenSource.Token);
            }
        }

        /// <summary>
        /// Create a new address in the device's physical space at the given transform.
        /// </summary>
        public async Task CreateAddress(Transform transform, CancellationToken ct)
        {
            // Check if disposed
            if (_writtingCancellationToken == null)
            {
                return;
            }

            CancelWriteOperations();
            using (CancellationTokenSource tokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct, _writtingCancellationToken.Token))
            {
                await CreateThenSetAddress(transform, tokenSource.Token);
            }
        }

        /// <summary>
        /// Set the address for this device.
        /// </summary>
        public async Task SetAddress(SharingServiceAddress address, CancellationToken ct)
        {
            // Check if disposed
            if (_writtingCancellationToken == null)
            {
                return;
            }

            CancelWriteOperations();
            using (CancellationTokenSource tokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct, _writtingCancellationToken.Token))
            {
                await SetPrimaryAddressWorker(address, tokenSource.Token);
            }
        }

        /// <summary>
        /// Get if the player is colocated.
        /// </summary>
        public bool Colocated(string playerId)
        {
            var address = _room.GetAddress(playerId);
            return address != null &&
                PrimaryAddress != null &&
                PrimaryAddress.Data == address.Data &&
                PrimaryAddress.Type == address.Type;
        }

        /// <summary>
        /// Force change events to re-fire.
        /// </summary>
        public void ReplayPropertyChangeEvents()
        {
            if (_localAddresses != null && _localAddresses.Count > 0)
            {
                RaiseLocalAddressesChanged();
            }

            if (_primaryAddress != null)
            {
                RaisePrimaryAddressesChanged();
            }
        }
        #endregion Public Functions

        #region Protected Functions
        /// <summary>
        /// Override to handle disable behavior
        /// </summary>
        protected virtual void OnDispose(bool disposing)
        {
        }
        #endregion Protected Functions

        #region Private Functions
        /// <summary>
        /// Call this when the particpants at the given address has changed.
        /// </summary>
        private void OnPlatformParticipantsChanged(ISharingServiceRoomAddresses sender, ParticipantsChangedArgs args)
        {
            AddressParticipantsChanged?.Invoke(this, args.Address);
        }

        private void CancelReadOperations(bool disposing = false)
        {
            _log.LogVerbose("[{0}] Cancelling read operations.", Name);
            CancelOperations(ref _readingCancellationToken, disposing);
        }

        private void CancelWriteOperations(bool disposing = false)
        {
            _log.LogVerbose("[{0}] Cancelling write operations.", Name);
            CancelOperations(ref _writtingCancellationToken, disposing);
        }

        private void CancelOperations(ref CancellationTokenSource cancellationToken, bool disposing)
        {
            if (cancellationToken != null)
            {
                cancellationToken.Cancel();
                cancellationToken.Dispose();
                cancellationToken = null;
            }

            if (!disposing)
            {
                cancellationToken = new CancellationTokenSource();
            }
        }

        /// <summary>
        /// Find then set the address if an existing address is found.
        /// </summary>
        private async Task<IReadOnlyList<SharingServiceAddress>> FindAndSetAddresses(bool allowPrompt, CancellationToken ct)
        {
            if (!ct.IsCancellationRequested)
            {
                IList<SharingServiceAddress> addresses;
                if (_search.Enabled || _forceSearch)
                {
                    addresses = LocalAddressesInitialized || !allowPrompt ?
                        await FindAddressAnchorWithoutUserPrompt(ct) :
                        await FindAddressAnchorWithUserPrompt(ct);
                }
                else
                {
                    addresses = new List<SharingServiceAddress>();
                    addresses.Add(await CreateAddressAnchor(transform: null, ct));
                }

                if (!ct.IsCancellationRequested)
                {
                    SetLocalAddreseses(addresses);
                }
                else
                {
                    DisposeLocalAddresses(addresses);
                }
            }

            return LocalAddresses;
        }

        /// <summary>
        /// Create then set the address.
        /// </summary>
        private async Task CreateThenSetAddress(Transform location, CancellationToken ct)
        {
            // Creating address will take time. While this is happening notify all other users that this client is not 
            // at a well defined address
            if (!ct.IsCancellationRequested && _primaryAddress != null)
            {
                _log.LogVerbose("[{0}] Clearing address", Name);
                await ClearAddressWorker(ct);
                _log.LogVerbose("[{0}] Clearing address done", Name);
            }

            SharingServiceAddress address = null;
            if (!ct.IsCancellationRequested)
            {
                _log.LogVerbose("[{0}] Creating new address", Name);
                address = await CreateAddressAnchor(location, ct);
            }

            if (ct.IsCancellationRequested)
            {
                _log.LogVerbose("[{0}] Ignoring create address request. Operation cancelled.", Name);
                address?.Dispose();
            }
            else 
            {
                await SetAddressToLocallyCreatedAddress(address, ct);
            }
        }

        /// <summary>
        /// Keep searching for a shared physical location until the user says they are at a new location.
        /// </summary>
        private async Task<IList<SharingServiceAddress>> FindAddressAnchorWithUserPrompt(CancellationToken ct)
        {
            int searchCount = 0;
            IList<SharingServiceAddress> addresses = null;
            AppDialog.AppDialogResult result = AppDialog.AppDialogResult.Ok;

            string sessionName = _room?.Name;
            if (string.IsNullOrEmpty(sessionName))
            {
                sessionName = "your session";
            }
            else
            {
                sessionName = $"'{sessionName}'";
            }

            while (result == AppDialog.AppDialogResult.Ok && (addresses == null || addresses.Count == 0) && !ct.IsCancellationRequested)
            {
                searchCount++;
                result = await AppServices.AppNotificationService.ShowDialog(new DialogOptions()
                {
                    Title = "Are There Nearby Users?",
                    Message = searchCount == 1 ?
                        $"Are there other users at your real world location, and are these users connected to {sessionName}?\n\nShould we try searching for them?" :
                        $"We couldn't find people at your real world location, who connected to {sessionName}.\n\nShould we keep searching?",
                    OKLabel = "Yes",
                    NoLabel = "No",
                    Buttons = AppDialog.AppDialogButtons.Ok | AppDialog.AppDialogButtons.No
                });

                if (result != AppDialog.AppDialogResult.Ok || ct.IsCancellationRequested)
                {
                    break;
                }

                if (searchCount == 2)
                {
                    _ = AppServices.AppNotificationService.ShowDialog(new DialogOptions()
                    {
                        Title = "Searching for Other Users",
                        Message = $"We'll search for others at your real world location. We'll only find for users that are connected to {sessionName}.\n\nTo help locate people, look around the surroundings of those nearby.",
                        OKLabel = "Ok",
                        Buttons = AppDialog.AppDialogButtons.Ok
                    });
                }

                try
                {
                    addresses = await _search.FindAddresses(ct);
                }
                catch (Exception ex)
                {
                    _log.LogError("[{0}] Failed to find location address. Exception: {1}", Name, ex);
                }
            }

            return addresses;
        }

        /// <summary>
        /// Keep searching for a shared physical location with no user prompt
        /// </summary>
        private async Task<IList<SharingServiceAddress>> FindAddressAnchorWithoutUserPrompt(CancellationToken ct)
        {
            IList<SharingServiceAddress> addresses = null;
            try
            {
                addresses = await _search.FindAddresses(ct);
            }
            catch (Exception ex)
            {
                _log.LogError("[{0}] Failed to find location address. Exception: {1}", Name, ex);
                addresses = new List<SharingServiceAddress>();
            }
            return addresses;
        }

        /// <summary>
        /// Create an anchor for the user's physical location.
        /// </summary>
        private async Task<SharingServiceAddress> CreateAddressAnchor(Transform transform, CancellationToken ct)
        {
            SharingServiceAddress address;

            if (transform == null)
            {
                _log.LogVerbose("[{0}] Trying to create a new address at a null transform. Defaulting to main camera transform.", Name);
                transform = CameraCache.Main.transform;
            }

            try
            {
                _log.LogVerbose("[{0}] Trying to create a new anchor at {1}", Name, transform.name);
                address = await _factory.CreateAddress(transform, ct);
                _log.LogVerbose("[{0}] Created a new anchor at {1}. ({2})", Name, transform.name, address);
            }
            catch (Exception ex)
            {
                _log.LogError("[{0}] Failed to create location address from anchor.", Name);
                throw ex;
            }

            return address;
        }

        /// <summary>
        /// Clear the address by setting it to a device only address.
        /// </summary>
        /// <returns></returns>
        private Task ClearAddressWorker(CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                _log.LogVerbose("[{0}] Ignoring clear address request. Operation cancelled.", Name);
                return Task.CompletedTask;
            }

            var deviceAddress = SharingServiceAddress.DeviceAddress();
            return SetAddressToLocallyCreatedAddress(deviceAddress, ct);
        }

        /// <summary>
        /// Set an address that was just created locally. Determine if address is known or not, then make it the primary address.
        /// </summary>
        /// <returns>True if address is known</returns>
        private async Task SetAddressToLocallyCreatedAddress(SharingServiceAddress address, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                _log.LogVerbose("[{0}] Ignoring set address request. Operation cancelled.", Name);
                return;
            }

            if (IsKnownLocalAddress(address))
            {
                _log.LogVerbose("[{0}] Setting a known address, {1}", Name, address);
                // duplicate addresses can occur on platforms that don't support anchors
                ReplaceKnownLocalAddress(_localAddresses, address);
                await SetPrimaryAddressWorker(address, ct);
            }
            else
            {
                _log.LogVerbose("[{0}] Setting an unknown address, {1} -> {2}", Name, address, _localAddresses);
                await SetUnknownPrimaryAddressWorker(address, ct);
            }
        }

        /// <summary>
        /// Set the primary address. If the address is not in the list of known anchors, the request is ignored
        /// </summary>
        private Task SetUnknownPrimaryAddressWorker(SharingServiceAddress address, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                _log.LogVerbose("[{0}] Ignoring set address request. Operation cancelled.", Name);
                return Task.CompletedTask;
            }

            if (address == null)
            {
                _log.LogVerbose("[{0}] Ignoring set address request. Address is null.", Name);
                return Task.CompletedTask;
            }

            _log.LogVerbose("[{0}] Inserting a new unknown address into the known address list. {1}", Name, address);
            InsertAddress(address);
            return SetPrimaryAddressWorker(address, ct);
        }

        /// <summary>
        /// Set the primary address to the service and locally.
        /// </summary>
        private async Task SetPrimaryAddressWorker(SharingServiceAddress address, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                _log.LogVerbose("[{0}] Ignoring set address request. Operation cancelled.", Name);
                return;
            }

            if (address != null && !IsKnownLocalAddress(address))
            {
                _log.LogError("[{0}] Possible memory leak. Address is not a known local anchor. {1}:{2}", Name, address.Type, address.Data);
            }

            // Cache address locally
            CachePrimaryAddress(address);

            // Attempt to save address to cloud service
            if (address != null)
            {
                try
                {
                    _log.LogVerbose("[{0}] Sharing primary address with service {1}:{2}", Name, address?.Type, address?.Data);
                    await _room.SetAddress(address);
                }
                catch (Exception ex)
                {
                    _log.LogError("[{0}] Failed to set placement address.", Name);
                    CachePrimaryAddress(null);
                    throw ex;
                }
            }
        }

        /// <summary>
        /// Get the first local address that was located.
        /// </summary>d
        private SharingServiceAddress GetDefaultAddress()
        {
            if (_localAddresses == null || _localAddresses.Count == 0)
            {
                return null;
            }
            else
            {
                return _localAddresses[0];
            }
        }

        /// <summary>
        /// Return if address is known
        /// </summary>
        private bool IsKnownLocalAddress(SharingServiceAddress address)
        {
            if (address == null)
            {
                return false;
            }

            if (_localAddresses == null)
            {
                return false;
            }

            bool found = false;
            int count = _localAddresses.Count;
            for (int i = 0; i < count; i++)
            {
                var localAddress = _localAddresses[i];
                if (EqualAddresses(localAddress, address))
                {
                    found = true;
                    break;
                }
            }

            return found;
        }

        /// <summary>
        /// Return the known copy of the given address.
        /// </summary>
        private SharingServiceAddress GetKnownLocalAddress(SharingServiceAddress address)
        {
            if (address == null)
            {
                return null;
            }

            if (_localAddresses == null)
            {
                return null;
            }

            SharingServiceAddress found = null;
            int count = _localAddresses.Count;
            for (int i = 0; i < count; i++)
            {
                var localAddress = _localAddresses[i];
                if (EqualAddresses(localAddress, address))
                {
                    found = localAddress;
                    break;
                }
            }

            return found;
        }

        /// <summary>
        /// Replace the known copy with the given address.
        /// </summary>
        private bool ReplaceKnownLocalAddress(IList<SharingServiceAddress> addresses, SharingServiceAddress address)
        {
            if (address == null)
            {
                _log.LogVerbose("[{0}] Ignoring replace known local address request. Address was null.", Name);
                return false;
            }

            if (addresses == null)
            {
                _log.LogVerbose("[{0}] Ignoring replace known local address request. Addresses were null.", Name);
                return false;
            }

            bool found = false;
            int count = addresses.Count;
            for (int i = 0; i < count; i++)
            {
                var localAddress = addresses[i];
                if (EqualAddresses(localAddress, address))
                {
                    // if same object, don't dispose
                    if (localAddress != address)
                    {
                        _log.LogVerbose("[{0}] Found duplicate address, replacing...{1}->{2}", Name, localAddress, address);
                        localAddress.Dispose();
                        addresses[i] = address;
                    }

                    found = true;
                    break;
                }
            }

            return found;
        }

        /// <summary>
        /// Are the two addresses equal.
        /// </summary>
        private bool EqualAddresses(SharingServiceAddress address1, SharingServiceAddress address2)
        {
            return address1 != null &&
                address2 != null &&
                address1.Data == address2.Data &&
                address1.Type == address2.Type;
        }

        /// <summary>
        /// Cache the primary address locally.
        /// </summary>
        private void CachePrimaryAddress(SharingServiceAddress primaryAddress)
        {
            if (_primaryAddress == primaryAddress)
            {
                _log.LogVerbose("[{0}] Ignoring set primary address. Primary address is already set. ({1})", Name, primaryAddress);
            }
            else
            {
                _log.LogVerbose("[{0}] Setting primary address to {1}:{2}", Name, primaryAddress?.Type, primaryAddress?.Data);
                if (_primaryAddress != null && !IsKnownLocalAddress(_primaryAddress))
                {
                    _primaryAddress.Dispose();
                }

                _primaryAddress = primaryAddress;
                RaisePrimaryAddressesChanged();
            }
        }

        private void RaisePrimaryAddressesChanged()
        {
            try
            {
                PrimaryAddressChanged?.Invoke(this, PrimaryAddress);
            }
            catch (Exception ex)
            {
                _log.LogError("[{0}] Exception occurred while raising PrimaryAddressChanged event. Exception: {1}", Name, ex);
            }
        }

        private void InsertAddress(SharingServiceAddress address)
        {
            if (address == null)
            {
                _log.LogVerbose("[{0}] Ignoring insert request of null address.", Name);
            }
            else if (!_localAddresses.Contains(address))
            {
                _localAddresses.Insert(0, address);
                _log.LogVerbose("[{0}] Added address to local address list, {1}", Name, address);
                RaiseLocalAddressesChanged();
            }
            else
            {
                _log.LogVerbose("[{0}] Ignoring insert a duplicate address.", Name);
            }
        }

        private void SetLocalAddreseses(IList<SharingServiceAddress> addresses)
        {
            _log.LogVerbose("[{0}] Setting local addresses to {1}.", Name, addresses);
            DisposeLocalAddresses();
            _localAddresses = (addresses == null ? new List<SharingServiceAddress>() : new List<SharingServiceAddress>(addresses));
            if (_primaryAddress != null && !ReplaceKnownLocalAddress(_localAddresses, _primaryAddress))
            {
                _localAddresses.Insert(0, _primaryAddress);
            }
            LocalAddressesInitialized = true;
            RaiseLocalAddressesChanged();
        }

        private void RaiseLocalAddressesChanged()
        {
            try
            {
                LocalAddressesChanged?.Invoke(this, LocalAddresses);
            }
            catch (Exception ex)
            {
                _log.LogError("[{0}] Exception occurred while raising LocalAddressesChanged event. Exception: {1}", Name, ex);
            }
        }

        private void DisposeLocalAddresses()
        {
            DisposeLocalAddresses(_localAddresses);
        }

        private void DisposeLocalAddresses(IList<SharingServiceAddress> addresses)
        {
            if (addresses != null)
            {
                foreach (var address in addresses)
                {
                    if (address == _primaryAddress)
                    {
                        _log.LogVerbose("[{0}] Not disposing primary address. Ignore dispose request for this list entry.", Name);
                    }
                    else if (address != null)
                    {
                        address.Dispose();
                    }
                }
                addresses.Clear();
            }
        }

        #endregion Private Functions
    }
}
