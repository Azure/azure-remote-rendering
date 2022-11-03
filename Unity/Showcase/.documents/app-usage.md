# App Usage

## Starting & configuring sessions in the app
To use this application, you'll have to start a new Azure Remote Rendering session. To start a session click the *session* button, and then click *start session*. Starting a session will take a few minutes. However once the session is ready, the app will automatically connect to it. The app will also remember the created last session, and will automatically connect to this session at start-up.

![Menu Image](.images/starting-session.gif)

Within the app, you can also change the [session's size](https://docs.microsoft.com/azure/remote-rendering/reference/limits#overall-number-of-polygons)
 (*Standard* or *Premium*) and [region](https://docs.microsoft.com/azure/remote-rendering/reference/regions). Before starting a session, click the *configure* button. You'll be presented with size and region options. After selecting the desired configuration, click *back* and start a new session.

## App features
This sample app has the following features:

### HoloLens

| <div style="width:190px">Feature</div> | Description |
| :-------------| :----------- |
| Session Management | Start, stop, and configure ARR sessions. |
| Session Status | View the session's lifetime, and various other performance statistics |
| Search Azure Containers | Search Azure containers for arrAsset models, and display the model names within the app's model menu. From the model menu, you can load these models. |
| Multiple Models | View multiple models at once.
| Manipulate Whole Model | Use MRTK's near and far interactions to move, rotate, and scale whole models.
| Manipulate Model Pieces | Use MRTK's near and far interactions to move, rotate, and scale model pieces.
| Slice Tool | Turn on a clipping plane that can be moved and rotated with MRTK's near and far interactions.
| Model Explosion | For easier access of pieces, explode a model.
| Change Model Materials | Change model pieces' materials.
| Reset Models | Reset a model so its pieces return to their original positions and materials.
| Erase Models | Erase whole models from the scene.
| Change Sky Map | Change the scene's sky map to a predefined set of cube maps.
| Add light sources | Add point lights, directional lights, and spotlights to the scene.

### Desktop

| <div style="width:190px">Feature</div> | Description |
| :-------------| :----------- |
| Session Management | Start, stop, and configure ARR sessions. |
| Session Status | View the session's lifetime, and various other performance statistics |
| Search Azure Containers | Search Azure containers for arrAsset models, and display the model names within the app's model menu. From the model menu, you can load these models. |
| Multiple Models | View multiple models at once.
| Manipulate Whole Model | Use MRTK's far interactions to move models.
| Manipulate Model Pieces | Use MRTK's far interactions to move model pieces.
| Slice Tool | Turn on a clipping plane that can be moved and rotated with MRTK's far interactions.
| Model Explosion | For easier access of pieces, explode a model.
| Change Model Materials | Change model pieces' materials.
| Reset Models | Reset a model so its pieces return to their original positions and materials.
| Erase Models | Erase whole models from the scene.
| Change Sky Map | Change the scene's sky map to a predefined set of cube maps.
| Add light sources | Add point lights, directional lights, and spotlights to the scene.
| Upload Models | Select supported source model to upload and convert it into an ARR model.
| Reset Camera | Resets the camera to the initial position.

### General Voice Commands

| <div style="width:190px">Command</div> | Description |
| :------------------------- | :------------------------- |
| Showcase, Erase All | Deletes all models from the scene |
| Showcase, Show Hands | Shows hand mesh |
| Showcase, Hide Hands | Hides hand mesh |
| Showcase, Show Local Stats | Shows the MRTK diag window| 
| Showcase, Hide Local Stats | Hides the MRTK diag window |
| Showcase, Show Stage | Show and move the local stage visual. Once visible, all new models will be loaded on the stage. |
| Showcase, Hide Stage | Hides the local stage visual. Once hidden, all new models will have to be placed individually. |
| Showcase, Move Stage | Show and move the local stage visual. Once visible, all new models will be loaded on the stage. This stage is automatically shown when creating a new collaboration session, as it serves as the shared collaboration origin. |
| Showcase, Show Ruler | Show a debugging ruler at the center of the stage. This will also show the stage. |
| Showcase, Hide Ruler | Hide the debugging ruler. |
| Showcase, Show Menu | Show the main menu |
| Showcase, Hide Menu | Hide the main menu | 
| Showcase, Quit | Quits the application |
| Showcase, Toggle Profiler | Show or hide the Mixed Reality Toolkit performance profiler window. |
| Showcase, Tools | Show or hide the tools menu. |
| Showcase, Models | Show or hide the models menu. |
| Showcase, Session | Show or hide the session menu. |
| Showcase, Stats | Show or hide the stats menu. |
| Showcase, Show Anchors | Show all the currently created spatial anchor on the HoloLens device. This is useful when debugging anchor placement. |
| Showcase, Hide Anchors | Hide all the visible spatial anchors. |
| Showcase, Set Pose Mode | Shows a dialog for controlling how local content should be stabilized, either using the local or remote pose space. This is known as remote rendering's [Pose Mode](https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.remoterendering.posemode?view=remoterendering). The default pose mode is set to use the local pose space, which consumes slightly more CPU cycles. | 
| Showcase, Change Pose Mode | See *Showcase, Set Pose Mode* |


### Pointer Tool Voice Commands

Here are all the tool 'mode' voice commands supported by the app. These change the mode of the pointer.

| <div style="width:190px">Command</div> | Description |
| :------------------------- | :------------------------- |
| Showcase, No Tool | Unselects the current pointer tool, so you can no longer interact with the models. |
| Showcase, Clip | Puts the pointer into clip or slice mode. This also turns on a single clipping plane. |
| Showcase, Erase | Puts the pointer into erase mode. When a model is clicked, the entire model will be deleted from the scene. |
| Showcase, Explode | Puts the pointer into explode mode. When a model is clicked, its pieces will explode outward from the center. |
| Showcase, Move All | Puts the pointer into move mode. When a model is clicked and held, the entire model will be moved, scaled, or rotated. |
| Showcase, Move Piece | Puts the pointer into move piece mode. When a model piece is clicked and held, the piece will be moved, scaled, or rotated. |
| Showcase, Revert | Puts the pointer into revert mode. When a model is clicked, the model pieces will return to the original position relative to the model's root. |
| Showcase, Reset | Puts the pointer into revert mode. When a model is clicked, the model pieces will return to the original position relative to the model's root. |
| Showcase, Slice | Puts the pointer into clip or slice mode. This also turns on a single clipping plane. |

### Collaboration Voice Commands

These commands are available when in a collaboration sharing session (or room).


| <div style="width:190px">Command</div> | Description |
| :------------------------- | :------------------------- |
| Showcase, Start Presenting | Make the local user a presenter, if there no an active presentation. The app menu is hidden for all non-presenters, and all non-presenters cannot interact with the app's models. |
| Showcase, Start Collaboration | Make the local user a presenter, if there no active presentation. The app menu is hidden for all non-presenters. All non-presenters can interact with the app's models, but the tool selection is controlled by the presenter. |
| Showcase, Stop Presenting | Stop all presenting and collaborations. The app menu is made visible to all users, and all users can interact with the app's models. |
| Showcase, All Players Move Stage | Makes the stage visible, and forces all other users to place their stage visual. This command is useful when hosting a presentation, and all users are in different locations. |
| Showcase, Show My Avatar | Show your own collaboration avatar on your device |
| Showcase, Hide My Avatar | Hide your own collaboration avatar on your device. This is the default state. |
| Showcase, Show Co-located Avatars | Show co-located users' avatars on your device. |
| Showcase, Hide Co-located Avatars | Hide co-located users' avatars on your device. This is the default state. |
| Showcase, Show Avatar Joints | Show joints on top of all avatar hand joints. |
| Showcase, Hide Avatar Joints | Hide joints on top of all avatar hand joints. This is the default state. |
| Showcase, Debug Avatars | Show all avatars and all avatar hand joints. |
| Showcase, Reset Avatars | Reset avatar and avatar hand joint visibilities to the default states. |
| Showcase, Ping User | Send a debugging ping to all collaboration users. |
| Showcase, Show Names | Show all avatar names above users' heads. This is the default state. |
| Showcase, Hide Names | Hide all avatar names above users' heads. |

## ARR session control in play mode
When in 'play' mode, the inspector view of the RemoteRenderingSessionInfo game object shows various buttons to interact with the session, including extend, stop, and forget the current session. Note that "forget" won't stop the remote session, but it'll prevent the app from using this session again.

![Inspector buttons in play mode](.images/editor-arr-service.png)

The *Temporary Overrides* settings apply to the currently playing app session, and are only used to when playing within the editor. The settings are:

| <div style="width:190px">Temporary Setting</div> | Description |
| :------------------ | :------------------ |
| Preferred Domain | If changed, this will be the remote rendering domain used when creating a new ARR session. By default this is the first domain in the *Remote Rendering Domains* list. |
| Session ID | If specified, the app will use this ARR session ID when connecting to the ARR service. This allows for starting a session using some other method, and then connecting to it within ARR Showcase. |

> **IMPORTANT**
> It is not possible to create or interact with sessions outside the editor's play mode.
