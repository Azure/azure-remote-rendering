// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#if PHOTON_INSTALLED
using System;
using System.Threading.Tasks;

#if WINDOWS_UWP
using System.Collections.Generic;
using System.Linq;
using Windows.System;
#endif

#if UNITY_EDITOR_WIN
using System.Security.Principal;
#endif

namespace Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication.Photon
{
    /// <summary>
    /// A helper to load in local user's data.
    /// </summary>
    public class PhotonLocalUser
    {
        private static LogHelper<PhotonLocalUser> _logger = new LogHelper<PhotonLocalUser>();
        private const string _unknownUser = "Unknown User";

        public static async Task<string> GetUserName()
        {
            string displayName = null;
            try
            {
                displayName = await GetUserNameWorker();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load user's name. {0}", ex);
            }

            if (string.IsNullOrEmpty(displayName))
            {
                displayName = _unknownUser;
            }

            return displayName;
        }

#if UNITY_EDITOR_WIN && WINDOWS_UWP
        private static Task<string> GetUserNameWorker()
        {
            var user = WindowsIdentity.GetCurrent();
            string displayName = user?.Name;
            return Task.FromResult(displayName);
        }
#elif WINDOWS_UWP
        private static async Task<string> GetUserNameWorker()
        {
            IReadOnlyList<User> users = await User.FindAllAsync();
            if (users == null)
            {
                _logger.LogError("Unable to get user name. Users was null.");
                return null;
            }

            var current = users.FirstOrDefault();
            if (current == null)
            {
                _logger.LogError("Unable to get user name. No current user found.");
                return null;
            }

            // Try find first and last name
            string displayName = null;
            string first = await current.GetPropertyAsync(KnownUserProperties.FirstName) as string;
            string last = await current.GetPropertyAsync(KnownUserProperties.LastName) as string;

            bool emptyFirst = string.IsNullOrEmpty(first);
            bool emptyLast = string.IsNullOrEmpty(last);

            if (!emptyFirst && !emptyLast)
            {
                displayName = string.Format("{0} {1}", first, last);
            }
            else if (emptyFirst && emptyLast)
            {
                displayName = _unknownUser;
            }
            else if (!emptyFirst)
            {
                displayName = first;
            }
            else if (!emptyLast)
            {
                displayName = last;
            }

            if (string.IsNullOrEmpty(displayName))
            {
                // User may have username
                _logger.LogVerbose("Failed to find first and/or last name property.");
                var data = await current.GetPropertyAsync(KnownUserProperties.AccountName);
                displayName = data as string;
            }

            if (string.IsNullOrEmpty(displayName))
            {
                _logger.LogError("Failed to find user display name.");
            }
            else
            {
                _logger.LogVerbose("Display name found. {0}", displayName);
            }

            return displayName;
        }
#else
        private static Task<string> GetUserNameWorker()
        {
            return Task.FromResult(string.Empty);
        }
#endif 
    }
}
#endif // PHOTON_INSTALLED
