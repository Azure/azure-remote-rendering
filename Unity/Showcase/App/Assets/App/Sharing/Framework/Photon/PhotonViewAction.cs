// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if PHOTON_INSTALLED
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    /// <summary>
    /// A class to help executing an action on a PhotonView that requires ownership
    /// </summary>
    public class PhotonViewAction : IOnPhotonViewOwnerChange
    {
        private Action<PhotonView> _action = null;
        private LogHelper<PhotonViewAction> _logger = new LogHelper<PhotonViewAction>();

        #region Constructor
        private PhotonViewAction(PhotonView view, int requiredOwnerId, Action<PhotonView> action)
        {
            View = view;
            RequiredOwnerId = requiredOwnerId;
            _action = action;

            RegisterCallback();
            TryExecution();
        }
        #endregion Constructor

        #region Public Properties
        /// <summary>
        /// The photon view being tracked.
        /// </summary>
        public PhotonView View { get; }

        /// <summary>
        /// The owner id that is required before the action is executed.
        /// </summary>
        public int RequiredOwnerId { get; }

        /// <summary>
        /// Has this action been executed.
        /// </summary>
        public bool HasExecuted { get; private set; }
        #endregion Public Properties

        #region Public Events
        /// <summary>
        /// Event fired when action is executed
        /// </summary>
        public event Action<PhotonViewAction> Executed;
        #endregion Public Events

        #region IOnPhotonViewOwnerChange
        public async void OnOwnerChange(Player newOwner, Player previousOwner)
        {
            if (newOwner.ActorNumber == RequiredOwnerId)
            {
                await Task.Delay(1);
                TryExecution();
            }
        }
        #endregion IOnPhotonViewOwnerChange

        #region Public Methods
        public static PhotonViewAction Create(GameObject gameObject, PhotonParticipant participant, Action<PhotonView> action)
        {
            PhotonView view = null;
            PhotonViewAction result = null;
            if (gameObject != null)
            {
                view = gameObject.GetComponent<PhotonView>();
            }

            int actorNumber = -1;
            if (participant.Inner != null)
            {
                actorNumber = participant.Inner.ActorNumber;
            }

            bool canHaveOwnership = false;
            if (view != null && actorNumber > 0)
            {
                canHaveOwnership =
                    (view.Owner != null && view.Owner.ActorNumber == actorNumber) ||
                    (view.OwnershipTransfer != OwnershipOption.Fixed);
            }

            if (canHaveOwnership)
            {
                result = new PhotonViewAction(view, participant.Inner.ActorNumber, action);
            }

            return result;
        }
        #endregion Public Methods

        #region Private Methods
        private void RegisterCallback()
        {
            if (View != null)
            {
                View.AddCallback<IOnPhotonViewOwnerChange>(this);
            }
        }

        private void UnregisterCallback()
        {
            if (View != null)
            {
                View.RemoveCallback<IOnPhotonViewOwnerChange>(this);
            }
        }

        private void TryExecution()
        {
            if (View != null && View.Owner != null && View.Owner.ActorNumber == RequiredOwnerId)
            {
                UnregisterCallback();

                try
                {
                    _action?.Invoke(View);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Exception occurred when firing action. {0}", ex);
                }

                HasExecuted = true;
                Executed?.Invoke(this);
            }

            if (!HasExecuted && View != null)
            {
                View.RequestOwnership();
            }
        }
        #endregion Private Methods
    }
}

#endif // PHOTON_INSTALLED
