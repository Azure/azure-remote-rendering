// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using UnityEngine;

public class ShareRoomMuteButton : MonoBehaviour
{
    ListItemActionData _muteData = null;
    ListItemActionData _unmuteData = null;
    ListItemActionData _data = null;

    #region MonoBehaviour Functions
    private void Awake()
    {
        _muteData = new ListItemActionData(Mute)
        {
            PrimaryLabel = "Mute",
            IconType = FancyIconType.Disconnected
        };

        _unmuteData = new ListItemActionData(Unmute)
        {
            PrimaryLabel = "Unmute",
            IconType = FancyIconType.Connected
        };
    }

    private void OnEnable()
    {
        AppServices.SharingService.AudioSettingsChanged += OnAudioSettingsChanged;
        UpdateButtonLabelAndIcon(AppServices.SharingService.AudioSettings);
    }

    private void OnDisable()
    {
        AppServices.SharingService.AudioSettingsChanged -= OnAudioSettingsChanged;
    }
    #endregion MonoBehaviour Functions

    #region Private Functions
    private void Mute()
    {
        SetMuteMicrophone(true);
    }

    private void Unmute()
    {
        SetMuteMicrophone(false);
    }

    private void SetMuteMicrophone(bool value)
    {
        var settings = AppServices.SharingService.AudioSettings;
        settings.MuteMicrophone = value;
        AppServices.SharingService.AudioSettings = settings;

    }

    private void OnAudioSettingsChanged(ISharingService sender, SharingServiceAudioSettings settings)
    {
        UpdateButtonLabelAndIcon(settings);
    }

    private void UpdateButtonLabelAndIcon(SharingServiceAudioSettings settings)
    {
        var listItem = GetComponent<ListItemWithStaticAction>();
        if (listItem != null)
        {
            var oldData = _data;
            _data = settings.MuteMicrophone ? _unmuteData : _muteData;
            listItem.OnDataSourceChanged(null, oldData, _data);
        }
    }
    #endregion Private Functions
}
