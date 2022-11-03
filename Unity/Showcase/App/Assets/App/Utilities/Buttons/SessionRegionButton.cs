// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.Utilities;
using System.Linq;
using UnityEngine;

/// <summary>
/// A button class for setting the Remote Rendering Service's location setting
/// </summary>
[RequireComponent(typeof(Interactable))]
public class SessionRegionButton : ClickableButton
{
    private const string _domainFormat = "{0}.mixedreality.azure.com";
    private string _domain;

    #region Serialized Fields
    [Header("Region Settings")]

    [SerializeField]
    [Tooltip("The location of the session server to set when the button is clicked.")]
    public RemoteRenderingServiceRegionValue location;

    /// <summary>
    /// The location of the session server to set when the button is clicked.
    /// </summary>
    public RemoteRenderingServiceRegionValue Location
    {
        get => location;
        set
        {
            if (location != value)
            {
                location = value;

                // invalidate the domain
                _domain = null;
            }
        }
    }

    [SerializeField]
    [Tooltip("The location label that represents the 'Location' field.")]
    public string locationName;

    /// <summary>
    /// The location label that represents the 'Location' field.
    /// </summary>
    public string LocationName
    {
        get => locationName;
        set => locationName = value;
    }
    #endregion Serialized Fields

    #region Public Properties
    /// <summary>
    /// Get the current domain for this location.
    /// </summary>
    public string Domain
    {
        get
        {
            if (!string.IsNullOrEmpty(_domain))
            {
                return _domain;
            }

            _domain = string.Format(_domainFormat, location);
            return _domain;
        }
    }
    #endregion Public Properties

    #region MonoBehavior Methods
    private void OnValidate()
    {
        UpdateDomainAndLocation();
        UpdateLabelText();
    }

    protected override void Start()
    {
        base.Start();
    }

    protected void OnEnable()
    {
        UpdateDomainAndLocation();
        UpdateLabelText();
    }

    private void Update()
    {
        Selected = AppServices.RemoteRendering.LoadedProfile.PreferredDomain == Domain;
    }
    #endregion MonoBehavior Methods

    #region Protected Methods
    protected override void OnClicked()
    {
        AppServices.RemoteRendering.LoadedProfile.PreferredDomain = Domain;
    }
    #endregion Protected Methods

    #region Private Methods
    private void UpdateLabelText()
    {
        LabelText = LocationName;
    }

    private void UpdateDomainAndLocation()
    {
        var loadedProfile = AppServices.RemoteRendering?.LoadedProfile;
        if (loadedProfile != null)
        {
            RemoteRenderingServiceRegion region = loadedProfile.RemoteRenderingDomains.FirstOrDefault(entry => entry.ValueEnum == location);
            if (region.ValueEnum != RemoteRenderingServiceRegionValue.error)
            {
                _domain = region.Domain ?? _domain;
                LocationName = region.Label ?? LocationName;
            }
            else if (Application.isPlaying)
            {
                // Disable this button.
                gameObject.SetActive(false);

                // Force parent to re-layout
                var grid = GetComponentInParent<GridObjectCollection>();
                if (grid != null)
                {
                    grid.UpdateCollection();
                }
            }
        }
    }
    #endregion Private Methods
}
