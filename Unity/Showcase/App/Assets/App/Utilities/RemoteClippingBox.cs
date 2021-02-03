// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Azure.RemoteRendering;
using Microsoft.Azure.RemoteRendering.Unity;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines potential faces of the clipping box.
/// </summary>
[Flags]
public enum ClippingBoxFaces
{
    /// <summary>
    /// Right facing side (+x).
    /// </summary>
    PositiveX = 1,
    /// <summary>
    /// Left facing side (-x).
    /// </summary>
    NegativeX = 2,
    /// <summary>
    /// Upwards facing side (+y).
    /// </summary>
    PositiveY = 4,
    /// <summary>
    /// Downward facing side (-y).
    /// </summary>
    NegativeY = 8,
    /// <summary>
    /// Forward facing side (+z).
    /// </summary>
    PositiveZ = 16,
    /// <summary>
    /// Backward facing side (-z).
    /// </summary>
    NegativeZ = 32,
}

/// <summary>
/// Defines groups of faces of a clipping box.
/// </summary>
[Flags]
public enum ClippingBoxFaceGroups
{
    /// <summary>
    /// No faces specified.
    /// </summary>
    None = 0,
    /// <summary>
    /// All side faces.
    /// </summary>
    Sides = (ClippingBoxFaces.PositiveX | ClippingBoxFaces.NegativeX | ClippingBoxFaces.PositiveZ | ClippingBoxFaces.NegativeZ),

    /// <summary>
    /// Top and bottom faces.
    /// </summary>
    TopBottom = (ClippingBoxFaces.PositiveY | ClippingBoxFaces.NegativeY),

    /// <summary>
    /// All faces.
    /// </summary>
    All = (Sides | TopBottom),
}


/// <summary>
/// Creates and manages an ARR remote clipping box.
/// </summary>
public class RemoteClippingBox : MonoBehaviour
{
    #region Constants
    private const int MAX_CUTPLANES = 4;
    #endregion // Constants

    #region Member Variables
    private ClippingBoxFaces lastFaces = (ClippingBoxFaces)ClippingBoxFaceGroups.None;
    private Dictionary<ClippingBoxFaces, GameObject> faceObjects = new Dictionary<ClippingBoxFaces, GameObject>();
    #endregion // Member Variables

    #region Unity Inspector Variables
    [Tooltip("The transform that is used to calculate the bounds of the clipping box. If none is specified, the bounds of the attached GameObject will be used.")]
    [SerializeField]
    private Transform boundsOverride;

    [Tooltip("The faces if the clipping box which will be used.")]
    [SerializeField]
    [EnumFlag]
    private ClippingBoxFaces faces = (ClippingBoxFaces)ClippingBoxFaceGroups.Sides;
    #endregion // Unity Inspector Variables

    #region Internal Methods
    /// <summary>
    /// Creates the specified faces.
    /// </summary>
    /// <param name="faces">
    /// The faces to create.
    /// </param>
    private void CreateFaces(ClippingBoxFaces faces)
    {
        // Loop through all values
        foreach (ClippingBoxFaces face in Enum.GetValues(typeof(ClippingBoxFaces)))
        {
            // Is this face being passed in?
            if (faces.HasFlag(face))
            {
                // Only create if not already alive
                if (!faceObjects.ContainsKey(face))
                {
                    // Create the object
                    GameObject faceObject = new GameObject(Enum.GetName(typeof(ClippingBoxFaces), face));

                    // Parent it
                    faceObject.transform.SetParent(transform, worldPositionStays: false);

                    // Create the component
                    ARRCutPlaneComponent cutPlane = faceObject.CreateArrComponent<ARRCutPlaneComponent>(RemoteManagerUnity.CurrentSession);

                    // Configure it
                    if (cutPlane.RemoteComponent != null)
                    {
                        cutPlane.RemoteComponent.Normal = FaceToNormal(face);
                        cutPlane.RemoteComponent.FadeLength = .0000025f;
                        cutPlane.RemoteComponent.FadeColor = new Color4Ub(0, 0, 75, 255);
                    }

                    RemoteEntitySyncObject syncObject = faceObject.GetComponent<RemoteEntitySyncObject>();
                    if (syncObject != null)
                    {
                        syncObject.SyncEveryFrame = true;
                    }

                    // Add it to the active list
                    faceObjects[face] = faceObject;
                }
            }
        }
    }

    /// <summary>
    /// Destroys the specified faces.
    /// </summary>
    /// <param name="faces">
    /// The faces to destroy.
    /// </param>
    private void DestroyFaces(ClippingBoxFaces faces)
    {
        // Loop through specified faces
        ForSpecificFaces(faces, (face, faceObject) =>
        {
            // Get the cut plane component
            ARRCutPlaneComponent cutPlane = faceObject.GetComponent<ARRCutPlaneComponent>();

            // If connected, disable the cut plane component
            if (RemoteManagerUnity.IsConnected)
            {
                try
                {
                    cutPlane.RemoteComponent.Enabled = false;
                }
                catch (Exception ex)
                {
                    Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}",  $"Could not disable cut plane: {ex.Message}");
                }
            }

            // Remove the face object from the active list
            faceObjects.Remove(face);

            // Mark it for destruction
            GameObject.Destroy(faceObject);
        });
    }

    /// <summary>
    /// Enables or disables all faces.
    /// </summary>
    /// <param name="enabled">
    /// <c>true</c> if faces should be enabled; otherwise <c>false</c>.
    /// </param>
    private void EnableFaces(bool enabled)
    {
        // Loop through active faces
        ForAllFaces((face, faceObject) =>
        {
            // Get the cut plane component
            ARRCutPlaneComponent cutPlane = faceObject.GetComponent<ARRCutPlaneComponent>();

            // Update enabled
            if ((cutPlane != null) && (cutPlane.RemoteComponent != null))
            {
                cutPlane.RemoteComponent.Enabled = enabled;
            }
        });
    }

    /// <summary>
    /// Converts a <see cref="ClippingBoxFaces"/> enum value to an ARR direction.
    /// </summary>
    /// <param name="face">
    /// The face to convert.
    /// </param>
    /// <returns>
    /// The corresponding axis.
    /// </returns>
    static private Axis FaceToNormal(ClippingBoxFaces face)
    {
        switch (face)
        {
            case ClippingBoxFaces.PositiveX:
                return Axis.X;

            case ClippingBoxFaces.NegativeX:
                return Axis.NegativeX;

            case ClippingBoxFaces.PositiveY:
                return Axis.Y;

            case ClippingBoxFaces.NegativeY:
                return Axis.NegativeY;

            case ClippingBoxFaces.PositiveZ:
                return Axis.Z;

            case ClippingBoxFaces.NegativeZ:
                return Axis.NegativeZ;

            default:
                // Catch all
                throw new InvalidOperationException("Unknown face");
        }
    }

    /// <summary>
    /// Converts a <see cref="ClippingBoxFaces"/> enum value to child position.
    /// </summary>
    /// <param name="face">
    /// The face to convert.
    /// </param>
    /// <param name="parent">
    /// The parent transform.
    /// </param>
    /// <returns>
    /// The corresponding position.
    /// </returns>
    static private Vector3 FaceToPosition(ClippingBoxFaces face, Transform parent)
    {
        switch (face)
        {
            case ClippingBoxFaces.PositiveX:
                return new Vector3(parent.localScale.x / 2, 0, 0);

            case ClippingBoxFaces.NegativeX:
                return new Vector3(-parent.localScale.x / 2, 0, 0);

            case ClippingBoxFaces.PositiveY:
                return new Vector3(0, parent.localScale.y / 2, 0);

            case ClippingBoxFaces.NegativeY:
                return new Vector3(0, -parent.localScale.y / 2, 0);

            case ClippingBoxFaces.PositiveZ:
                return new Vector3(0, 0, parent.localScale.z / 2);

            case ClippingBoxFaces.NegativeZ:
                return new Vector3(0, 0, -parent.localScale.z / 2);

            default:
                throw new InvalidOperationException("Unknown face");
        }
    }

    /// <summary>
    /// Performs the specified action for all active faces.
    /// </summary>
    /// <param name="action">
    /// The action to perform.
    /// </param>
    private void ForAllFaces(Action<ClippingBoxFaces, GameObject> action)
    {
        // Loop through ALL potential faces
        foreach (ClippingBoxFaces face in Enum.GetValues(typeof(ClippingBoxFaces)))
        {
            // Is the face alive?
            if (faceObjects.ContainsKey(face))
            {
                // Perform the action
                action(face, faceObjects[face]);
            }
        }
    }

    /// <summary>
    /// Performs the specified action for the specific faces.
    /// </summary>
    /// <param name="faces">
    /// The faces to match.
    /// </param>
    /// <param name="action">
    /// The action to perform.
    /// </param>
    private void ForSpecificFaces(ClippingBoxFaces faces, Action<ClippingBoxFaces, GameObject> action)
    {
        // Loop through all enum values
        foreach (ClippingBoxFaces face in Enum.GetValues(typeof(ClippingBoxFaces)))
        {
            // But only if this flag was passed in
            if (faces.HasFlag(face))
            {
                // And only if it's alive
                if (faceObjects.ContainsKey(face))
                {
                    // Perform the action
                    action(face, faceObjects[face]);
                }
            }
        }
    }

    /// <summary>
    /// Gets the transform to use as the bounds of the clipping box.
    /// </summary>
    /// <returns>
    /// The transform to use as the bounds of the clipping box.
    /// </returns>
    protected virtual Transform GetBounds()
    {
        return (boundsOverride != null ? boundsOverride : transform);
    }

    /// <summary>
    /// Updates which faces are active.
    /// </summary>
    /// <remarks>
    /// This works by destroying faces that are no longer needed and creating faces which are new.
    /// </remarks>
    private void UpdateActiveFaces()
    {
        // If were not connected we can't create or update any cut planes
        if (!RemoteManagerUnity.IsConnected) { return; }

        // Which faces were added and removed?
        ClippingBoxFaces removed = (lastFaces & ~faces);
        ClippingBoxFaces added = (faces & ~lastFaces);

        // Create and destroy as necessary
        DestroyFaces(removed);
        CreateFaces(added);

        // Sync
        lastFaces = faces;

        // Did we exceed the maximum number of cut planes?
        if (faceObjects.Count > MAX_CUTPLANES)
        {
            Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}",  $"There are {faceObjects.Count} cut planes in use but some servers may only support {MAX_CUTPLANES}. Some cut planes may be ignored.");
        }
    }

    /// <summary>
    /// Updates the active faces to match the specified bounds.
    /// </summary>
    /// <param name="bounds">
    /// The bounds to match.
    /// </param>
    private void UpdateBounds(Transform bounds)
    {
        // Loop through active faces
        ForAllFaces((face, faceObject) =>
        {
            // Update its position
            faceObject.transform.localPosition = FaceToPosition(face, bounds);
        });

        // No longer changed
        bounds.hasChanged = false;
    }
    #endregion // Internal Methods

    #region Unity Overrides
    /// <summary>
    /// Start is called before the first frame update
    /// </summary>
    protected virtual void Start()
    {
        // If no transform specified, use our own
        if (boundsOverride == null) { boundsOverride = transform; }
    }

    /// <summary>
    /// Update is called once per frame
    /// </summary>
    protected virtual void Update()
    {
        // Can only do the following if connected
        if (RemoteManagerUnity.IsConnected)
        {
            // Did the faces change since last update?
            bool facesUpdated = false;
            if (faces != lastFaces)
            {
                // If so, create and destroy face objects.
                UpdateActiveFaces();
                facesUpdated = true;
            }

            // Get the bounds
            Transform bounds = GetBounds();

            // Were faces updated or have the bounds changed?
            if ((facesUpdated || bounds.hasChanged))
            {
                // If so, we need to update face positions to match bounds
                UpdateBounds(bounds);
            }
        }
    }

    /// <summary>
    /// Called when the behavior becomes disabled. Also called when the GameObject is destroyed.
    /// </summary>
    protected virtual void OnDisable()
    {
        // Disable all active faces
        EnableFaces(false);

        // Unsubscribe from connection changed events
        RemoteManagerUnity.OnSessionUpdate -= RemoteManagerUnity_OnSessionUpdate;
    }

    /// <summary>
    /// Called to draw a gizmo.
    /// </summary>
    protected virtual void OnDrawGizmos()
    {
        Transform bounds = GetBounds();

        if ((enabled) && (bounds != null))
        {
            Gizmos.matrix = bounds.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }
    }

    /// <summary>
    /// Called when the GameObject becomes enabled and active.
    /// </summary>
    protected virtual void OnEnable()
    {
        // Subscribe to connection changed events
        RemoteManagerUnity.OnSessionUpdate += RemoteManagerUnity_OnSessionUpdate;

        // Enable all active faces
        EnableFaces(true);
    }

    #endregion // Unity Overrides

    #region Overrides / Event Handlers

    private void RemoteManagerUnity_OnSessionUpdate(RemoteManagerUnity.SessionUpdate update)
    {
        // Were we just disconnected?
        if (update == RemoteManagerUnity.SessionUpdate.SessionDisconnected)
        {
            // Destroy all faces
            DestroyFaces((ClippingBoxFaces)ClippingBoxFaceGroups.All);

            // Set last faces to none, so on the next connect the right ones will be created
            lastFaces = (ClippingBoxFaces)ClippingBoxFaceGroups.None;
        }

        // Note: Connect and creation is handled as part of the update loop
    }

    private void RemoteManager_ConnectionStatusChanged(ConnectionStatus status, Result error)
    {
        // Were we just disconnected?
        if (status == ConnectionStatus.Disconnected)
        {
            // Destroy all faces
            DestroyFaces((ClippingBoxFaces)ClippingBoxFaceGroups.All);

            // Set last faces to none, so on the next connect the right ones will be created
            lastFaces = (ClippingBoxFaces)ClippingBoxFaceGroups.None;
        }

        // Note: Connect and creation is handled as part of the update loop
    }
    #endregion // Overrides / Event Handlers

    #region Public Properties
    /// <summary>
    /// Gets or sets the transform that is used to calculate the bounds of the clipping box. If none is specified, the bounds of the attached GameObject will be used.
    /// </summary>
    public Transform BoundsOverride { get => boundsOverride; set => boundsOverride = value; }
    #endregion // Public Properties
}
