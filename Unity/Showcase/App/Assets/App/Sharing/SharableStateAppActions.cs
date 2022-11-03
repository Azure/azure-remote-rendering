// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// A class for sharing the state of the application's generic actions 
/// </summary>
public class SharableStateAppActions : MonoBehaviour
{
    #region Serialized Fields
    [SerializeField]
    [FormerlySerializedAs("target")]
    [Tooltip("The sharing object used to send properties updates too. If null at Start(), the nearest parent target will be used.")]
    private SharingObjectBase sharingObject;

    /// <summary>
    /// The sharing object used to send properties updates too. If null at Start(), the nearest parent target will be used.
    /// </summary>
    public SharingObjectBase SharingObject
    {
        get => sharingObject;
        set => sharingObject = value;
    }

    [SerializeField]
    [Tooltip("The behavior that will create the debug ruler.")]
    private CreateGameObject debugRulerCreator = null;

    /// <summary>
    /// The behavior that will create the debug ruler.
    /// </summary>
    public CreateGameObject DebugRulerCreator
    {
        get => debugRulerCreator;
        set => debugRulerCreator = value;
    }
    #endregion Serialized Fields

    #region Public Properties
    /// <summary>
    /// Get if the room is showing the ruler
    /// </summary>
    public bool ShouldShowRuler
    {
        get
        {
            bool showRuler;
            if (sharingObject == null || !sharingObject.TryGetProperty(SharableStrings.DebugRuler, out showRuler))
            {
                showRuler = false;
            }
            return showRuler;
        }
    }
    #endregion Public Properties

    #region MonoBehaviour Functions
    private void Start()
    {
        if (sharingObject == null)
        {
            sharingObject = GetComponent<SharingObjectBase>();
        }

        if (sharingObject != null)
        {
            sharingObject.PropertyChanged += OnTargetPropertyChanged;
        }

        if (debugRulerCreator != null)
        {
            debugRulerCreator.ObjectCreated.AddListener(OnDebugRulerShown);
            debugRulerCreator.ObjectDestroyed.AddListener(OnDebugRulerHidden);
        }
    }

    private void OnDestroy()
    {
        if (sharingObject != null)
        {
            sharingObject.PropertyChanged -= OnTargetPropertyChanged;
            sharingObject = null;
        }

        if (debugRulerCreator != null)
        {
            debugRulerCreator.ObjectCreated.RemoveListener(OnDebugRulerShown);
            debugRulerCreator.ObjectDestroyed.RemoveListener(OnDebugRulerHidden);
        }
    }
    #endregion MonoBehaviour Functions

    #region Private Functions
    private void OnTargetPropertyChanged(ISharingServiceObject sender, string property, object input)
    {
        switch (input)
        {
            case bool value when property == SharableStrings.DebugRuler:
                ReceiveDebugRulerVisibility(visible: value);
                break;
        }
    }

    private void OnDebugRulerShown()
    {
        SendDebugRulerVisibility(true);
    }

    private void OnDebugRulerHidden()
    {
        SendDebugRulerVisibility(false);
    }

    private void ReceiveDebugRulerVisibility(bool visible)
    {
        if (debugRulerCreator == null)
        {
            return;
        }

        if (visible)
        {
            debugRulerCreator.Make();
        }
        else
        {
            debugRulerCreator.Clear();
        }
    }

    private void SendDebugRulerVisibility(bool visible)
    {
        if (sharingObject == null)
        {
            return;
        }

        if (ShouldShowRuler != visible)
        {
            sharingObject.SetProperty(SharableStrings.DebugRuler, visible);
        }
    }
    #endregion Private Functions
}
