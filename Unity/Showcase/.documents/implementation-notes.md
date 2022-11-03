# Implementation notes
This page contains notes on various aspects of the application's implementation.

## Mixed Reality Toolkit Integration
This application has a strong dependency on the [Mixed Reality Toolkit](https://docs.microsoft.com/en-us/windows/mixed-reality/mrtk-unity/?view=mrtkunity-2021-05). Much of the application code has taken dependencies on the the Mixed Reality Toolkit for things like user interfaces, materials, spatial awareness, interactions, and extension services. This document will discus a two of these integration points, [Mixed Reality Toolkit Service Extensions](#mixed-reality-toolkit-service-extensions) and [Custom Input Handling](#custom-input-handling).

## Mixed Reality Toolkit Service Extensions
The application uses the [Mixed Reality Toolkit's service extensions](https://docs.microsoft.com/en-us/windows/mixed-reality/mrtk-unity/features/extensions/extension-services?view=mrtkunity-2021-05) for integrating with [Azure Remote Rendering](https://docs.microsoft.com/en-us/azure/remote-rendering/), [Azure Storage](https://docs.microsoft.com/en-us/azure/storage/), [Azure Spatial Anchors](https://docs.microsoft.com/en-us/azure/spatial-anchors/), and collaboration services like Exit Game's [Photon Voice 2](https://www.photonengine.com/en-US/Voice). 

The service extensions that need to be configured before launching the application are:

* [`RemoteRenderingService`](#remote-rendering-service-extension)
* [`SharingService`](#sharing-service-extension-aka-collaboration-service)
* [`AnchoringService`](#azure-spatial-anchor-service-extension)

## Remote Rendering Service Extension

The [`RemoteRenderingService`](#remote-rendering-service-extension) component is a custom Mixed Reality Toolkit [extension service](https://microsoft.github.io/MixedRealityToolkit-Unity/Documentation/Extensions/ExtensionServices.html) that wraps the functionality provided by the [Azure Remote Rendering Unity Package](https://docs.microsoft.com/en-us/azure/remote-rendering/how-tos/unity/install-remote-rendering-unity-package). The [`RemoteRenderingService`](#remote-rendering-service-extension) helps manage the lifecycle of Azure Remote Rendering sessions, as well as helps manage the lifetime of remote rendered assets within these sessions. For example, the [`RemoteRenderingService`](#remote-rendering-service-extension) can help start a remote rendering session, load a remote rendered asset, unload a remote rendered asset, and then stop a remote rendering session. To use the [`RemoteRenderingService`](#remote-rendering-service-extension), an Azure Remote Rendering account and an Azure Storage account must first be created. 

To create an Azure Remote Rendering account follow the [Create an Azure Remote Rendering account](https://docs.microsoft.com/en-us/azure/remote-rendering/how-tos/create-an-account) manual. Once you have created an Azure Remote Rendering account, you will be provided with an Azure Remote Rendering account ID, account domain, and account authentication settings.

To create an Azure Storage account, with a blob container containing Azure Remote Rendering assets, follow the [Convert a model for rendering](https://docs.microsoft.com/en-us/azure/remote-rendering/quickstarts/convert-model) manual. The Azure blob container should store remote rendering assets ([`.arrAsset`](https://docs.microsoft.com/en-us/azure/remote-rendering/concepts/models)) that are able to be viewed within a remote rendering session. Once a storage account is created you will be provided with a storage account name, blob container name, and if needed, an account key.

Finally, as part of the setup, you must decide which authentication method to use by following the [Choosing the Authentication Method for Azure Remote Rendering](#choosing-the-authentication-method-for-azure-remote-rendering) instructions.

### Remote Rendering Configuration Profiles

A variety of remote rendering and storage settings can be changed with the [`RemoteRenderingService`](#remote-rendering-service-extension) configuration profile. The type of settings differ based on the type of profile that is [created](#choosing-the-authentication-method-for-azure-remote-rendering). However, there is a set of base settings that are common across all profile types, these are as follows:
 
| <div style="width:190px">Property</div> | Description                                                                                                                     |
| :------------------ | :------------------ |
| Size | The Azure Remote Rendering session size, either `Standard` or `Premium`. See [limitations of session sizes](https://docs.microsoft.com/azure/remote-rendering/reference/limits#overall-number-of-polygons).    |
| Session Override | Either a session guid or a session host name. If specified, the app will attempt to connect to this session. |
| Max Lease Time | The default lease time, in seconds, of the Azure Remote Rendering session. If `AutoRenewLease` is `false` or the application is disconnected, the session will expire after this time. |
| Auto Renew Lease | If `true` and the application is connected to a remote rendering session, the application will attempt to extend the remote rendering session lease before it expires. |
| Auto Reconnect | If true, the app will attempt to auto reconnect after a disconnection. |
| Auto Reconnect Rate | The rate, in seconds, in which the application will attempt to reconnect after a disconnection. |
| Always Include Default Models | If `true`, the model list will contain the default models, even when using an override file or a storage account. |
| Remote Rendering Domains | A list of [Remote rendering domains](https://docs.microsoft.com/azure/remote-rendering/reference/regions) (e.g. westus2.mixedreality.azure.com) and associated labels. The list order defines the order in which the domains are tested. |
| Storage Model Container  | The Azure Storage blob container. This blob container should include a set of remote [`.arrAsset`](https://docs.microsoft.com/en-us/azure/remote-rendering/concepts/models) blobs. The [`.arrAsset`](https://docs.microsoft.com/en-us/azure/remote-rendering/concepts/models) blobs will be ingested into the application's menu, allowing the users to pick which [`.arrAsset`](https://docs.microsoft.com/en-us/azure/remote-rendering/concepts/models) to load. For more information on how to add model assets to the menu, see the [Adding 3D Model Assets to ARR Showcase](adding-3d-model-assets-to-application.md) document. |

> **IMPORTANT**\
> To be able to load models from the storage account, you must link it to your Azure Remote Rendering account. This applies to both profile types. The necessary steps are described on the [<u>account creation page</u>](https://docs.microsoft.com/azure/remote-rendering/how-tos/create-an-account#link-storage-accounts) of the Remote Rendering documentation. Please note that this is only necessary for loading models.

### Choosing the Authentication Method for Azure Remote Rendering
This application exposes two authentication methods for accessing Azure Remote Rendering and Azure Storage services, authenticating either using an account secret or an Azure Active Directory (AAD) account. The type of authentication used is defined by the type of [`RemoteRenderingService`](#remote-rendering-service-extension) configuration profile that is applied, [development](#remote-renderings-development-configuration-profile) or [production](#remote-renderings-production-configuration-profile). 

### Remote Rendering's Development Configuration Profile
A [development](#remote-renderings-development-configuration-profile) profile is the quickest way to get started because it uses account keys, without additional configuration. 

To create a development profile right-click Unity's Asset window and select *Create > ARR Showcase > Configuration Profile > Remote Rendering Service > Development*. Then apply this profile to the [`RemoteRenderingService`](#remote-rendering-service-extension) configuration under the Mixed Reality Toolkit's inspector window.

![Remote Rendering Development Configuration](.images/editor-arr-service-config-development.png)

| <div style="width:190px">Property</div> | Description                                         |
| :----------------------- | :------------------------------------------------------------------------- |
| Account Id               | The [Azure Remote Rendering account ID](https://docs.microsoft.com/azure/remote-rendering/how-tos/create-an-account#retrieve-the-account-information). |
| Account Key              | The [Azure Remote Rendering account key](https://docs.microsoft.com/azure/remote-rendering/how-tos/create-an-account#retrieve-the-account-information). |
| Storage Account Name     | The [Azure Storage account name](https://docs.microsoft.com/azure/remote-rendering/how-tos/create-an-account#link-storage-accounts). This account owns the *model container*. |
| Storage Account Key      | The Azure Storage account key. Needed if the *model container* is private. |

### Remote Rendering's Production Configuration Profile
A [production](#remote-renderings-production-configuration-profile) profile is the most secure way to utilize Azure Remote Rendering and Azure Blob Storage because it uses Azure Active Directory (AAD) to authenticate users. The production profile will require additional Azure configuration, described in the Azure Remote Rendering tutorial for [security](https://docs.microsoft.com/azure/remote-rendering/tutorials/unity/security/security).

To create a production profile right-click Unity's Asset window and select *Create > ARR Showcase > Configuration Profile > Remote Rendering Service > Production*. Then apply this profile to the [`RemoteRenderingService`](#remote-rendering-service-extension) configuration under the Mixed Reality Toolkit's inspector window.

![Remote Rendering Production Configuration](.images/editor-arr-service-config-production.png)

| <div style="width:190px">Property</div> | Description                                         |
| :------------------------------ | :------------------------------------------------------------------------- |
| Account Id                      | The [Azure Remote Rendering account ID](https://docs.microsoft.com/azure/remote-rendering/how-tos/create-an-account#retrieve-the-account-information). |
| App Id                          | The [Azure Active Directory Application ID](https://docs.microsoft.com/azure/remote-rendering/how-tos/authentication#authentication-for-deployed-applications) |
| Tenant Id                       | The [Tenant ID of the Azure Active Directory Application](https://docs.microsoft.com/azure/remote-rendering/how-tos/authentication#authentication-for-deployed-applications) |
| Storage Account Name            | The [Azure Storage account name](https://docs.microsoft.com/azure/remote-rendering/how-tos/create-an-account#link-storage-accounts). This account owns the *model container*. |
| Storage Model Path By Username  | If checked, models uploaded through the desktop application will be stored in a sub directory matching the user name. |

### Configuring Remote Rendering and Azure Storage with XML File
 
Mostly all setting of the [`RemoteRenderingService`](#remote-rendering-service-extension) settings are optional if their values are set in the [`arr.account.xml`](../App/Assets/StreamingAssets/arr.account.xml) file. The [`arr.account.xml`](../App/Assets/StreamingAssets/arr.account.xml) file can be placed under Unity's [`StreamingAssets`](../App/Assets/StreamingAssets) directory. If this file exists, the app will use the file's settings, instead of those within the configuration profile.
 
The [`arr.account.xml`](../App/Assets/StreamingAssets/arr.account.xml) file is not tracked by *Git*, as defined in the repository's [`.gitignore`](../.gitignore), preventing accidental commits of private/secret information. So it is preferred to use [`arr.account.xml`](../App/Assets/StreamingAssets/arr.account.xml) instead of adding your private account settings to the MRTK's configuration profiles.

Also, the [`arr.overrides.xml`](.samples/arr.overrides.xml) can be used to override settings even if the app has already been packaged and deployed. For Unity editor overrides, [`arr.overrides.xml`](.samples/arr.overrides.xml) needs to be placed in the [%USERPROFILE%/AppData/LocalLow/Microsoft/ARR Showcase]() folder. For HoloLens overrides, [`arr.overrides.xml`](.samples/arr.overrides.xml) needs to be placed in the app's [LocalState](https://docs.microsoft.com/en-us/windows/mixed-reality/using-the-windows-device-portal#file-explorer) folder. The file should only contain the settings you want to override.

The schema for [`arr.account.xml`](../App/Assets/StreamingAssets/arr.account.xml) and [`arr.overrides.xml`](.samples/arr.overrides.xml) can be found [here](.schemas/arr.overrides.schema.xds).

## Sharing Service Extension (aka Collaboration Service)

The [`SharingService`](#sharing-service-extension-aka-collaboration-service) object enables collaboration between multiple HoloLens and desktop devices. These collaboration features are sometimes referred to as sharing or multi-user components within application's source code. The sharing service synchronizes application state, co-locate users within the same physical space, host avatars, and powers voice communication. To enable these collaboration features, install [Photon](#using-photon-voice-2-for-collaboration) and configure these sharing service settings.

![Sharing Service Configuration](.images/editor-sharing-service-config.png)

| <div style="width:190px">Property</div> | Description |
| :---------------------- | :---------------------- |
| Provider | The [`SharingService`](#sharing-service-extension-aka-collaboration-service) is an abstraction around cloud service platforms. This enumeration defines which cloud service to use for the collaboration features.  Currently only [Photon Voice 2](https://www.photonengine.com/en-US/Voice) is supported. If collaboration features are not desired, set this value to *offline*. |
| Auto Connect | Set true to automatically login to cloud services on app startup. Set to false to prevent logging at app launch, instead login will occur when the user selects the *sharing* menu. |
| Room Name Format | The format string used when naming public collaboration sessions (or rooms). This format string must have a `{0}` index somewhere, for example `"Room {0}"`. The `{0}` will be filed with a unique integer value. |
| Private Room Name Format | The format string used when naming private collaboration sessions (or rooms). This format string must have a `{0}` index somewhere, for example `"Private Room {0}"`. The `{0}` will be filed with a unique integer value. |
| Verbose Logging | When set to true, turns on verbose logging messages for the sharing service. This is useful when trying to diagnose sharing failures.  |
| Photon Realtime ID | The Photon Realtime (PUN) ID used when connecting to Photon realtime services. Since Photon is the only supported sharing service, currently, this value must be set for a functioning collaboration experience. |
| Photon Voice ID | The Photon Voice ID used when connecting to Photon voice services. Since Photon is the only support sharing service, currently, this value must be set for functioning voice communications. |
| Photo Avatar Prefab | The avatar prefab to spawn for each new Photon user. By default this is `PhotonAvatar.prefab`. |
| Photon Player Colors | A list of colors to apply to each new Photon user's avatar, in a round-robin fashion. |

### Configuring Sharing Service with XML File

The [`SharingService`](#sharing-service-extension-aka-collaboration-service) may also be configured using the [`arr.account.xml`](../App/Assets/StreamingAssets/arr.account.xml) file. The [`arr.account.xml`](../App/Assets/StreamingAssets) file can be placed under Unity's [`StreamingAssets`](../App/Assets/StreamingAssets) directory. If this file exists, the app will use the file's settings, instead of those within the configuration profiles.

The [`arr.account.xml`](../App/Assets/StreamingAssets/arr.account.xml) file is not tracked by *Git*, as defined in the repository's [`.gitignore`](../.gitignore),, preventing accidental commits of private/secret information. So it is preferred to use [`arr.account.xml`](../App/Assets/StreamingAssets/arr.account.xml) instead of adding your private account settings to the service extension's configuration profile.

Also, the [`arr.overrides.xml`](.samples/arr.overrides.xml) can be used to override settings even if the app has already been packaged and deployed. For Unity editor and Desktop overrides, [`arr.overrides.xml`](.samples/arr.overrides.xml) needs to be placed in the [%USERPROFILE%/AppData/LocalLow/Microsoft/ARR Showcase]() folder. For HoloLens overrides, [`arr.overrides.xml`](.samples/arr.overrides.xml) needs to be placed in the app's [LocalState](https://docs.microsoft.com/en-us/windows/mixed-reality/using-the-windows-device-portal#file-explorer) folder. The file should only contain the settings you want to override.

The schema for [`arr.account.xml`](../App/Assets/StreamingAssets/arr.account.xml) and [`arr.overrides.xml`](.samples/arr.overrides.xml) can be found at [arr.account.overrides.schema.xsd](.schemas/arr.account.overrides.schema.xsd).

### Using Photon Voice 2 for Collaboration

The application can be configured to share app state and voice using [Photon Voice 2](https://www.photonengine.com/en-US/Voice) from Exit Games. Photon Voice version 2.31.0 has been verified to work with this application, however it is possible that later versions will also work. 

The Photon Voice 2 asset package is not included by default. To install the Photon Voice 2 assets, go to the Unity Asset Store and search for *Photon Voice 2*.  Once Photon Voice 2 is found, click through to download and add the assets to your application. At the time of writing this document, this involved selecting *Add To My Assets*, signing in with a Unity Account, selecting *Open in Unity*, and importing assets into the Azure Remote Rendering Showcase Unity project. After adding Photon Voice to the project, there might be compile errors. Photon contains Android components that will not compile if the application is missing some dependencies. The Android components can be removed safely, with no impact to the HoloLens or Window PC application. 

> The Photon Voice 2 binaries from the Unity Asset Store do not support ARM64. If Photon voice communications on ARM64 is needed, contact [Exit Games](https://www.photonengine.com/) for an ARM64 version of Photon Voice.
> 
> State sharing will work on all platforms, even on ARM64. So if voice communication is not required, ARM64 will be functional. Errors will be logged when the application attempts to start voice communication. Nevertheless, these errors can be ignored if the application does not require voice communication.

Once the Photon Voice 2 assets have been added to the project, ensure that you have also created a Photon Realtime (PUN) ID and Photon Voice ID. These IDs can be created on your [Photon Engine Dashboard](https://www.photonengine.com/) from Exit Games. Once you have created these IDs, you can configure the [Sharing Service](#sharing-service-extension-aka-collaboration-service). Photon might display a dialog, prompting for Photon to be configured. If Photon has been configured via the [Sharing Service](#sharing-service-extension-aka-collaboration-service), these Photon dialogs can be safely ignored.

After the Photon Voice 2 plug-in has been added and configured, the application can create sharing sessions (or rooms). Other users can then join a room via the lobby by clicking the menu's *Join Room* button. A basic avatar will be created for each remote user that joins a  room. The application developer can modify this avatar to fit their scenario. The default avatar can be found at the `PhotonGenericAvatar.prefab`.

![Application's default Photon avatar](.images/avatar.2.png)

## Azure Spatial Anchor Service Extension

his application uses [Azure Spatial Anchors](https://azure.microsoft.com/en-us/services/spatial-anchors/) to ensure multiple users in the same space see holograms in the same position. We call this co-localization. 

The [`AnchoringService`](#azure-spatial-anchor-service-extension) is a custom Mixed Reality Toolkit [extension service](https://docs.microsoft.com/en-us/windows/mixed-reality/mrtk-unity/features/extensions/extension-services?view=mrtkunity-2021-05) that wraps the [Azure Spatial Anchor Unity SDK](https://docs.microsoft.com/en-us/azure/spatial-anchors/tutorials/tutorial-new-unity-hololens-app?tabs=azure-portal). The [`AnchoringService`](#azure-spatial-anchor-service-extension) helps ensure multiple users in the same physical space see holograms at same position. This is called co-localization. This [`AnchoringService`](#azure-spatial-anchor-service-extension) is used to co-locate user in the same physical space, who are also in the same collaboration session (or sharing room). If the application has been configured to use the [`SharingService`](#sharing-service-extension-aka-collaboration-service), then the [`AnchoringService`](#azure-spatial-anchor-service-extension) should also be configured. If the application does not require the [`SharingService`](#sharing-service-extension-aka-collaboration-service) or does not require users to be co-located, then the [`AnchoringService`](#azure-spatial-anchor-service-extension) does not need to be configured. 

>Note, that the [`SharingService`](#sharing-service-extension-aka-collaboration-service) also uses local spatial anchors, in addition to Azure Spatial Anchors. The [`SharingService`](#sharing-service-extension-aka-collaboration-service) saves local spatial anchors to the device, so to recall the user's previous stage placement, when not in a collaboration session.

To configured the [`AnchoringService`](#azure-spatial-anchor-service-extension) an `AnchoringServiceProfile` must to edited and applied. For anchors to be shared with other users, the `AnchorAccountID` and `AnchorAccountKey` must be set. 

![Anchoring Service Configuration](.images/editor-anchoring-service-config.png)

 The description of [`AnchoringService`](#azure-spatial-anchor-service-extension) properties are as follows:

| <div style="width:190px">Property</div> | Description |
| :---------------------- | :---------------------- |
| Anchor Account ID | This is the Azure Spatial Anchor account ID. This must be set for the multi-user experience to work. |
| Anchor Account Key | This is the Azure Spatial Anchor account key. This must be set for the multi-user experience to work. |
| Search Timeout | The time, in seconds, to stop searching for a particular Azure Spatial Anchor. If negative, the app will never stop searching for the shared Azure Spatial Anchor. It's a good idea to set to a positive value, so to prevent users at different physical locations from endlessly searching for undiscoverable anchors. |
| Verbose Logging | When set to true, turns on verbose logging messages for the anchoring service. This is useful when trying to diagnose anchoring failures. |

### Configuring Azure Spatial Anchor Service with XML File

The `Anchor Account ID` and `Anchor Account Key` profiles settings are optional if these values are set in the [`arr.account.xml`](../App/Assets/StreamingAssets/arr.account.xml) file. The [`arr.account.xml`](../App/Assets/StreamingAssets/arr.account.xml) file can be placed under Unity's [`StreamingAssets`](../App/Assets/StreamingAssets) directory. If this file exists, the app will use the file's settings, instead of those within the configuration profiles.
 
The [`arr.account.xml`](../App/Assets/StreamingAssets/arr.account.xml) file is not tracked by *Git*, as defined in the repository's [`.gitignore`](../.gitignore), preventing accidental commits of private/secret information. So it is preferred to use [`arr.account.xml`](../App/Assets/StreamingAssets/arr.account.xml) instead of adding your private account settings to the service extension's configuration profile.

Also, the [`arr.overrides.xml`](.samples/arr.overrides.xml) can be used to override settings even if the app has already been packaged and deployed. For Unity editor overrides, [`arr.overrides.xml`](.samples/arr.overrides.xml) needs to be placed in the [%USERPROFILE%/AppData/LocalLow/Microsoft/ARR Showcase]() folder. For HoloLens overrides, [`arr.overrides.xml`](.samples/arr.overrides.xml) needs to be placed in the app's [LocalState](https://docs.microsoft.com/en-us/windows/mixed-reality/using-the-windows-device-portal#file-explorer) folder. The file should only contain the settings you want to override.

The schema for [`arr.account.xml`](../App/Assets/StreamingAssets/arr.account.xml) and [`arr.overrides.xml`](.samples/arr.overrides.xml) can be found at [arr.account.overrides.schema.xsd](.schemas/arr.account.overrides.schema.xsd).

## Custom Input Handling
The application uses the [Mixed Reality Toolkit's input system](https://docs.microsoft.com/en-us/windows/mixed-reality/mrtk-unity/features/input/overview?view=mrtkunity-2021-05) for interacting with remote rendered entities, without relying on Unity colliders. This was accomplished by creating a custom [Mixed Reality Toolkit focus provider](https://docs.microsoft.com/en-us/dotnet/api/microsoft.mixedreality.toolkit.input.focusprovider?view=mixed-reality-toolkit-unity-2020-dotnet-2.7.0), [`RemoteFocusProviderNoCollider`](../App/Assets/App/Focus/RemoteFocusProviderNoColliders.cs). This focus provider executes remote ray casts for each of the active Mixed Reality Toolkit pointers, at a rate of 30 Hz, and determines if a remote or local object should receive a pointer's focus.

For the [`RemoteFocusProviderNoCollider`](../App/Assets/App/Focus/RemoteFocusProviderNoColliders.cs)  focus provider to work with hand grabs, a custom [sphere pointer](https://docs.microsoft.com/en-us/windows/mixed-reality/mrtk-unity/features/input/pointers?view=mrtkunity-2021-05#spherepointer) was also created. This custom sphere pointer is called [`RemoteSpherePointer`](../App/Assets/App/Focus/RemoteSpherePointer.cs), and performs ray-casts a bit differently than the default [`SpherePointer`](https://docs.microsoft.com/en-us/dotnet/api/microsoft.mixedreality.toolkit.input.spherepointer?view=mixed-reality-toolkit-unity-2020-dotnet-2.7.0). The default [`SpherePointer`](https://docs.microsoft.com/en-us/dotnet/api/microsoft.mixedreality.toolkit.input.spherepointer?view=mixed-reality-toolkit-unity-2020-dotnet-2.7.0) performs a sphere cast to determine which Unity colliders are within focus of the hands (i.e. can be grabbed). The Azure Remote Rendering service does not support sphere casts, so instead the [`RemoteSpherePointer`](../App/Assets/App/Focus/RemoteSpherePointer.cs) casts linear rays between each fingertip and the corresponding thumb tip, capped at some max length. The [`RemoteSpherePointer`](../App/Assets/App/Focus/RemoteSpherePointer.cs) might also cast a linear ray between the thumb tip and the last known focus point of the [`RemoteSpherePointer`](../App/Assets/App/Focus/RemoteSpherePointer.cs). If any of these linear rays intersect with remote or local geometry, the closest hit point becomes the [`RemoteSpherePointer's`](../App/Assets/App/Focus/RemoteSpherePointer.cs) focus point.

The [Mixed Reality Toolkit's input system](https://docs.microsoft.com/en-us/windows/mixed-reality/mrtk-unity/features/input/overview?view=mrtkunity-2021-05) is also used to limit the number of game objects needed for interacting with remote entities. This is accomplished by dynamically creating game objects for remote entities when they receive [pointer focus](https://docs.microsoft.com/en-us/windows/mixed-reality/mrtk-unity/features/input/pointers?view=mrtkunity-2021-05). Once a remote entity loses focus, its associated game objects are destroyed. For more information about this behavior see summary comment section of the [`RemoteObjectExpander.cs`](../App/Assets/App/RemoteObject/RemoteObjectExpander.cs) file. 