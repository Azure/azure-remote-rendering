// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

/// <summary>
/// A set of strings used for property names and event 
/// </summary>
public static class SharableStrings
{
    /// <summary>
    /// A command notifying all other players to move their stages.
    /// </summary>
    public const string CommandPlayersMoveStage = "moveStage";

    /// <summary>
    /// A command notifying that a object should delete itself.
    /// </summary>
    public const string CommandObjectDelete = "deleted";

    /// <summary>
    /// A command notifying that a object should reset to its original state.
    /// </summary>
    public const string CommandObjectReset = "reset";

    /// <summary>
    /// A command notifying that a object should start exploding into pieces.
    /// </summary>
    public const string CommandObjectStartExploding = "explode";

    /// <summary>
    /// A property name for sharing an anchor id.
    /// </summary>
    public const string AnchorId = "anchor";

    /// <summary>
    /// A property name for sharing an anchor id and a fallback pose.
    /// </summary>
    public const string AnchorIdAndFallback = "anchorBack";

    /// <summary>
    /// A property name for the location when an anchor can be used.
    /// </summary>
    public const string AnchorLocationId = "anchorLocation";

    /// <summary>
    /// A property name for sharing an anchor's fallback pose.
    /// </summary>
    public const string AnchorFallbackPose = "anchorPose";

    /// <summary>
    /// A property name for sharing if a menu is sharing its tool selection with all players.
    /// </summary>
    public const string MenuIsSharingTools = "sharedTools";

    /// <summary>
    /// A property name for sharing if a menu is visible.
    /// </summary>
    public const string MenuIsVisible = "menu";

    /// <summary>
    /// A property name for sharing the menu's tool selection.
    /// </summary>
    public const string MenuToolMode = "mode";

    /// <summary>
    /// A property name for sharing the object's serialized model data.
    /// </summary>
    public const string ObjectData = "data";

    /// <summary>
    /// A property name for sharing if an object is in the process of being deleted.
    /// </summary>
    public const string ObjectIsDeleting = "deleting";

    /// <summary>
    /// A property name for sharing if an object is enabled and visible.
    /// </summary>
    public const string ObjectIsEnabled = "enabled";

    /// <summary>
    /// A property name for sharing if an object is exploded into pieces.
    /// </summary>
    public const string ObjectIsExploded = "exploded";

    /// <summary>
    /// A property name for sharing an object's material data.
    /// </summary>
    public const string ObjectMaterial = "material";

    /// <summary>
    /// A property name for sharing an object's local transform.
    /// </summary>
    public const string ObjectTransform = "transform";

    /// <summary>
    /// A property name for sharing the presenter's player id.
    /// </summary>
    public const string PresenterId = "presenter";

    /// <summary>
    /// A property name for sharing if a player has the clipping tool active.
    /// </summary>
    public const string PlayerIsClipping = "clipping";

    /// <summary>
    /// A property name for sharing if a player's scene is still loading (e.g. rendering services still starting, looking for anchors, loading a new model, ect.).
    /// </summary>
    public const string PlayerIsLoading = "loading";

    /// <summary>
    /// A property name for sharing the currently select cube map for the sky box.
    /// </summary>
    public const string SkyCubeMap = "sky";

    /// <summary>
    /// A property name for sharing if the stage is visible.
    /// </summary>
    public const string StageIsVisible = "visible";

    /// <summary>
    /// A property name for sharing the id of the object that is currently on the app's main stage.
    /// </summary>
    public const string StageObjectId = "staged";
    
    /// <summary>
    /// A property name for sharing the player's username for display.
    /// </summary>
    public const string PlayerName = "playername";

    /// <summary>
    /// A property name for sharing if the local player is currently speaking
    /// </summary>
    public const string PlayerSpeaking = "speaking";

    /// <summary>
    /// A property name for sharing if the local player is currently speaking, but the voice is not being transmitted.
    /// </summary>
    public const string PlayerMuted = "muted";

    /// <summary>
    /// A property name for sharing if the player's primary avatar color.
    /// </summary>
    public const string PlayerPrimaryColor = "primarycolor";

    /// <summary>
    /// A property name for sharing if the debug ruler should be shown.
    /// </summary>
    public const string DebugRuler = "debugruler";

    /// <summary>
    /// a property name for sharing latency to another player
    /// </summary>
    public const string PlayerLatency = "latency";

    /// <summary>
    /// a property name for sharing when user camera is capturing
    /// </summary>
    public const string PlayerCapturing = "capture";
}
