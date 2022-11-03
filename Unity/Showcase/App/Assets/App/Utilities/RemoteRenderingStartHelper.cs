// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.MixedReality.Toolkit.Extensions;

/// <summary>
/// A helper for starting the remote rendering service.
/// </summary>
public static class RemoteRenderingStartHelper
{
    public static async void StartWithPrompt(AppDialog.AppDialogLocation promptLocation = AppDialog.AppDialogLocation.Default)
    {
        AppDialog.AppDialogResult startSession = await AppServices.AppNotificationService.ShowDialog(new DialogOptions()
        {
            Title = "Create Session",
            Message = "This app uses Azure Remote Rendering to display holograms.\n\nWould you like to create a Standard or Premium Azure Remote Rendering Session?",
            OKLabel = "Standard",
            NoLabel = "Premium",
            CancelLabel = "Cancel",
            Buttons = AppDialog.AppDialogButtons.All,
            Location = promptLocation
        });

        // Map buttons to session size
        RenderingSessionVmSize sessionSize = RenderingSessionVmSize.None;
        if (startSession == AppDialog.AppDialogResult.Ok)
        {
            sessionSize = RenderingSessionVmSize.Standard;
        }
        else if (startSession == AppDialog.AppDialogResult.No)
        {
            sessionSize = RenderingSessionVmSize.Premium;
        }

        if (sessionSize != RenderingSessionVmSize.None)
        {
            CreateAndConnect(sessionSize);
        }
    }

    private static async void CreateAndConnect(RenderingSessionVmSize sessionSize)
    {
        if (AppServices.RemoteRendering.LoadedProfile != null)
        {
            AppServices.RemoteRendering.LoadedProfile.Size = sessionSize;
        }

        await AppServices.RemoteRendering.AutoConnect();
    }
}
