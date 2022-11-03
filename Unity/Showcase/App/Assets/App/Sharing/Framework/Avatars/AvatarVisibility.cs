// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication
{
    /// <summary>
    /// A helper to enable and disable game objects and components based on the state of the avatar.
    /// </summary>
    public class AvatarVisibility : AvatarComponent
    {
        private AvatarVisibilityState _state = AvatarVisibilityState.Unknown;
        private RendererVisibility _rendererVisibility = null;

        #region Serialized Field
        [SerializeField]
        [Tooltip("The objects that should be enables for local avatars.")]
        private UnityEngine.Object[] localObjects = null;

        /// <summary>
        /// The objects that should be enables for local avatars.
        /// </summary>
        public UnityEngine.Object[] LocalObjects
        {
            get => localObjects;
            set => localObjects = value;
        }

        [SerializeField]
        [Tooltip("The objects that should be enables for remote avatars.")]
        private UnityEngine.Object[] remoteObjects = null;

        /// <summary>
        /// The objects that should be enables for local avatars.
        /// </summary>
        public UnityEngine.Object[] RemoteObjects
        {
            get => remoteObjects;
            set => remoteObjects = value;
        }

        [SerializeField]
        [Tooltip("The objects that should be enables for co-located avatars.")]
        private UnityEngine.Object[] colocatedObjects = null;

        /// <summary>
        /// The objects that should be enables for co-located  avatars.
        /// </summary>
        public UnityEngine.Object[] ColocatedObjects
        {
            get => colocatedObjects;
            set => colocatedObjects = value;
        }
        #endregion Serialized Field

        #region Public Fields
        public AvatarVisibilityState State
        {
            get => _state;

            private set
            {
                if (_state != value)
                {
                    ExitState(_state);
                    EnterState(value);
                }
            }
        }
        #endregion Public Fields 

        #region MonoBehaviour Functions
        private void Awake()
        {
            // disable all states
            SetEnablement(GetObjects(AvatarVisibilityState.Local), enable: false);
            SetEnablement(GetObjects(AvatarVisibilityState.Remote), enable: false);
            SetEnablement(GetObjects(AvatarVisibilityState.Colocated), enable: false);
        }

        protected override void OnDestroy()
        {
            if (Service != null)
            {
                Service.AddressUsersChanged -= OnAddressUsersChanged;
                Service.AvatarSettingsChanged -= OnAvatarSettingsChanged;
            }

            base.OnDestroy();
        }
        #endregion MonoBehaviour Functions

        #region Protected Functions
        protected override void OnInitialized()
        {
            base.OnInitialized();

            if (Service != null)
            {
                Service.AddressUsersChanged += OnAddressUsersChanged;
                Service.AvatarSettingsChanged += OnAvatarSettingsChanged;
            }

            UpdateState();
        }
        #endregion Protected Functions

        #region Private Functions
        private void UpdateState()
        {
            if (Service == null)
            {
                return;
            }

            if (PlayerData.IsLocal)
            {
                State = AvatarVisibilityState.Local;
            }
            else if (Service.Colocated(PlayerData.PlayerId))
            {
                State = AvatarVisibilityState.Colocated;
            }
            else
            {
                State = AvatarVisibilityState.Remote;
            }
        }
        private void ExitState(AvatarVisibilityState value)
        {
            SetEnablement(GetObjects(value), enable: false);
        }

        private void EnterState(AvatarVisibilityState value)
        {
            _state = value;
            SetEnablement(GetObjects(value), enable: true);
            UpdateRendererSettings();
        }

        public UnityEngine.Object[] GetObjects(AvatarVisibilityState value)
        {
            switch (value)
            {
                case AvatarVisibilityState.Local:
                    return localObjects;

                case AvatarVisibilityState.Remote:
                    return remoteObjects;

                case AvatarVisibilityState.Colocated:
                    return colocatedObjects;

                default:
                    return null;
            }
        }

        private void SetEnablement(UnityEngine.Object[] objects, bool enable)
        {
            if (objects != null)
            {
                int length = objects.Length;
                for (int i = 0; i < length; i++)
                {
                    var obj = objects[i];
                    if (obj is Behaviour)
                    {
                        ((Behaviour)obj).enabled = enable;
                    }
                    else if (obj is GameObject)
                    {
                        ((GameObject)obj).SetActive(enable);
                    }
                }
            }
        }

        private void UpdateRendererSettings()
        {
            if (Service == null)
            {
                return;
            }

            if (_rendererVisibility == null)
            {
                _rendererVisibility = GetComponent<RendererVisibility>();
            }

            if (_rendererVisibility == null)
            {
                return;
            }

            var settings = Service.AvatarSettings;
            _rendererVisibility.TextVisibleAlways = settings.ShowNamePlates;

            switch (State)
            {
                case AvatarVisibilityState.Local:
                    _rendererVisibility.enabled = settings.ShowCurrent;
                    break;

                case AvatarVisibilityState.Remote:
                    _rendererVisibility.enabled = settings.ShowRemote;
                    break;

                case AvatarVisibilityState.Colocated:
                    _rendererVisibility.enabled = settings.ShowCoLocated;
                    break;
            }
        }

        private void OnAvatarSettingsChanged(ISharingService sender, SharingServiceAvatarSettings settings)
        {
            UpdateRendererSettings();
        }

        private void OnAddressUsersChanged(ISharingService obj)
        {
            UpdateState();
        }
        #endregion Private Function 
    }

    /// <summary>
    /// The visibility state of the avatar.
    /// </summary>
    [Serializable]
    public enum AvatarVisibilityState
    {
        [Tooltip("The ownership is not known.")]
        Unknown,

        [Tooltip("The avatar is the local user.")]
        Local,

        [Tooltip("The avatar is belongs to a remote user.")]
        Remote,

        [Tooltip("The avatar is belongs to a co-located user.")]
        Colocated,
    }
}
