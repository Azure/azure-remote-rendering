// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;
using System;
using System.Xml.Serialization;

/// <summary>
/// The settings for the focus highlighting.
/// </summary>
[Serializable]
[XmlRoot(ElementName = "FocusHighlightSettings")]
public struct RemoteFocusHighlightSettings
{
    #region Serialized Fields
    [SerializeField]
    [Tooltip("The settings to use when the whole object is being targeted.")]
    private Settings whole;

    /// <summary>
    /// The settings to use when the whole object is to being targeted.
    /// </summary>
    public Settings Whole
    {
        get => whole;
        set => whole = value;
    }

    [SerializeField]
    [Tooltip("The settings to use when an object piece is being targeted.")]
    private Settings piece;

    /// <summary>
    /// The settings to use when an object piece is being targeted.
    /// </summary>
    public Settings Piece
    {
        get => piece;
        set => piece = value;
    }
    #endregion

    #region Public Properties
    /// <summary>
    /// The default values for focus highlighting.
    /// </summary>
    public static RemoteFocusHighlightSettings Default = new RemoteFocusHighlightSettings()
    {
        whole = Settings.WholeDefault,
        piece = Settings.PieceDefault
    };

    #endregion Public Properties

    #region Public Methods
    /// <summary>
    /// Get the settings  for the corresponding selection state, whole object or object piece.
    /// </summary>
    public Settings GetSettings(bool wholeObject)
    {
        return wholeObject ? whole : piece;
    }
    #endregion Public Methods

    #region Public Structs
    [Serializable]
    public struct Settings
    {
        /// <summary>
        /// The default settings for selecting a whole model.
        /// </summary>
        /// <remarks>
        /// Until the performance of whole object "hierarchical state overrides" is improved, the default is disable 
        /// selection/highlighting for the whole model. Once performance is improved, re-enable the edge selection.
        /// </remarks>
        public static Settings WholeDefault = new Settings()
        {
            Edges = RemoteFocusHighlightType.None,
            Tint = RemoteFocusHighlightType.None,
            TintColor = new Color(0.0823529411764706f, 0.3882352941176471f, 0.8862745098039216f, 0.2f)
        };

        public static Settings PieceDefault = new Settings()
        {
            Edges = RemoteFocusHighlightType.Piece,
            Tint = RemoteFocusHighlightType.Piece,
            TintColor = new Color(0.0823529411764706f, 0.3882352941176471f, 0.8862745098039216f, 0.2f)
        };

        public RemoteFocusHighlightType Edges;
        public RemoteFocusHighlightType Tint;
        public Color TintColor;
    }
    #endregion Public Structs
}
