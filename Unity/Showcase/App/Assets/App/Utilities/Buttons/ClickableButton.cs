// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(Interactable))]
public abstract class ClickableButton : MonoBehaviour
{
    private string _label = string.Empty;
    private bool _selected = false;

    #region Serialized Fields
    [Header("Clickable Button Settings")]

    [SerializeField]
    [Tooltip("The interaction that will be clicked.")]
    private Interactable clickable;

    /// <summary>
    /// The interaction that will be clicked.
    /// </summary>
    public Interactable Clickable
    {
        get => clickable;
        set => clickable = value;
    }

    [SerializeField]
    [Tooltip("The preview label that show's label text.")]
    private TextMeshPro previewLabel;

    /// <summary>
    /// The preview label that show's label text.
    /// </summary>
    public TextMeshPro PreviewLabel
    {
        get => previewLabel;
        set => previewLabel = value;
    }

    [SerializeField]
    [Tooltip("The background that shows selection state.")]
    private GameObject selectionHighlight;

    /// <summary>
    /// The background that shows selection state.
    /// </summary>
    public GameObject SelectionHighlight
    {
        get => selectionHighlight;
        set => selectionHighlight = value;
    }
    #endregion Serialized Fields

    #region Public Properties
    /// <summary>
    /// Get or set the label text.
    /// </summary>
    public string LabelText
    {
        get => _label;

        set
        {
            if (_label != value)
            {
                _label = value;
                UpdateLabel();
            }
        }
    }

    /// <summary>
    /// Get or set the selected status.
    /// </summary>
    public bool Selected
    {
        get => _selected;

        set
        {
            if (_selected != value)
            {
                _selected = value;
                UpdateHighlight();
            }
        }
    }
    #endregion Public Properties

    #region MonoBehavior Methods
    protected virtual void Start()
    {
        if (clickable == null)
        {
            clickable = GetComponent<Interactable>();
        }
        clickable.OnClick.AddListener(OnClicked);

        UpdateLabel();
        UpdateHighlight();
    }

    protected virtual void OnDestroy()
    {
        clickable?.OnClick.RemoveListener(OnClicked);

    }
    #endregion MonoBehavior Methods

    #region Protected Methods
    protected virtual void OnClicked()
    {
    }
    #endregion Protected Methods

    #region Private Methods
    private void UpdateLabel()
    {
        if (previewLabel != null)
        {
            previewLabel.text = _label;
        }

        name = $"{_label} Button";
    }

    private void UpdateHighlight()
    {
        if (selectionHighlight != null)
        {
            selectionHighlight.SetActive(_selected);
        }
    }
    #endregion Private Methods
}
