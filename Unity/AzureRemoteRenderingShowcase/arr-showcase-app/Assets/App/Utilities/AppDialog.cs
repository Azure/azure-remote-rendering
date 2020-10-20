// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.Utilities;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public class AppDialog : MonoBehaviour
{
    private TaskCompletionSource<AppDialogResult> _taskSource = new TaskCompletionSource<AppDialogResult>();

    public static bool DialogOpen = false;

    public enum AppDialogResult
    {
        Ok,
        No,
        Cancel
    }
    
    #region Serialized Fields
    [Header("General Settings")]

    [SerializeField]
    [Tooltip("The distance, from the main camera, at which to start displaying the dialog.")]
    private float startDistance = 0.6f;

    /// <summary>
    /// The distance, from the main camera, at which to start displaying the dialog.
    /// </summary>
    public float StartDistance
    {
        get => startDistance;
        set => startDistance = value;
    }

    [SerializeField]
    [Tooltip("Should this behavior destory the game object on close.")]
    private bool destroyOnClose = false;

    /// <summary>
    /// Should this behavior destory the game object on close.
    /// </summary>
    public bool DestroyOnClose
    {
        get => destroyOnClose;
        set => destroyOnClose = value;
    }

    [Header("Object Parts")]

    [SerializeField]
    [Tooltip("The button clicked for confirming the dialog request.")]
    private Interactable okButton;

    /// <summary>
    /// The button clicked for confirming the dialog request.
    /// </summary>
    public Interactable OkButton
    {
        get => okButton;
        set => okButton = value;
    }
    
    [SerializeField]
    [Tooltip("The button clicked for an optional alternate confirmation of the dialog request.")]
    private Interactable noButton;

    /// <summary>
    /// The button clicked for an optional alternate confirmation of the dialog request.
    /// </summary>
    public Interactable NoButton
    {
        get => noButton;
        set => noButton = value;
    }

    [SerializeField]
    [Tooltip("The button clicked for cancel the dialog request.")]
    private Interactable cancelButton;

    /// <summary>
    /// The button clicked for cancel the dialog request.
    /// </summary>
    public Interactable CancelButton
    {
        get => cancelButton;
        set => cancelButton = value;
    }

    [SerializeField]
    [Tooltip("The text of the dialog header.")]
    private TextMesh dialogHeaderText;

    /// <summary>
    /// The dialog of the text
    /// </summary>
    public TextMesh DialogHeaderText
    {
        get => dialogHeaderText;
        set => dialogHeaderText = value;
    }

    [SerializeField]
    [Tooltip("The text of the dialog.")]
    private TextMesh dialogText;

    /// <summary>
    /// The dialog of the text
    /// </summary>
    public TextMesh DialogText
    {
        get => dialogText;
        set => dialogText = value;
    }

    [SerializeField]
    [Tooltip("The text OK button.")]
    private TextMeshPro okButtonText;

    /// <summary>
    /// The dialog of the text
    /// </summary>
    public TextMeshPro OkButtonText
    {
        get => okButtonText;
        set => okButtonText = value;
    }
    
    [SerializeField]
    [Tooltip("The text NO button.")]
    private TextMeshPro noButtonText;

    /// <summary>
    /// The dialog of the text
    /// </summary>
    public TextMeshPro NoButtonText
    {
        get => noButtonText;
        set => noButtonText = value;
    }

    [SerializeField]
    [Tooltip("The text cancel button.")]
    private TextMeshPro cancelButtonText;

    /// <summary>
    /// The dialog of the text
    /// </summary>
    public TextMeshPro CancelButtonText
    {
        get => cancelButtonText;
        set => cancelButtonText = value;
    }

    #endregion Serialized Fields

    #region Public Properties
    /// <summary>
    /// The task completed once the dialog closes
    /// </summary>
    public Task<AppDialogResult> DialogTask => _taskSource.Task;
    #endregion Public Properties

    #region MonoBehavior Functions
    private void Start()
    {
        if (okButton != null)
        {
            okButton.OnClick.AddListener(ClickedOk);
        }
        
        if (noButton != null)
        {
            noButton.OnClick.AddListener(ClickedNo);
        }

        if (cancelButton != null)
        {
            cancelButton.OnClick.AddListener(ClickedCanceled);
        }
    }

    /// <summary>
    /// Place dialog in front of user
    /// </summary>
    private void OnEnable()
    {
        PlaceInFront();
    }

    /// <summary>
    /// If destroyed before user selected an option, return false.
    /// </summary>
    private void OnDestroy()
    {
        DialogOpen = false;
        _taskSource.TrySetResult(AppDialogResult.Cancel);
    }
    #endregion MonoBehavior Functions

    #region Public Functions
    /// <summary>
    /// Open the dialog
    /// </summary>
    public async Task<AppDialogResult> Open()
    {
        while(DialogOpen) //Prevent multiple dialogs
        {
            await Task.Delay(200);
        }
        if (gameObject != null)
        {
            gameObject.SetActive(true);
        }
        DialogOpen = true;

        return await DialogTask;
    }

    /// <summary>
    /// Close the dialog control
    /// </summary>
    public void Close(bool allowDestroy = true)
    {
        DialogOpen = false;
        if (gameObject != null)
        {
            gameObject.SetActive(false);
            if (destroyOnClose && allowDestroy)
            {
                GameObject.Destroy(gameObject);
            }
        }
    }
    #endregion Public Functions

    #region Private Functions
    /// <summary>
    /// Place dialog in front of the user.
    /// </summary>
    private void PlaceInFront()
    {
        Transform cameraTransform = CameraCache.Main.transform;
        transform.position = cameraTransform.position + (cameraTransform.forward.normalized * startDistance);
        transform.rotation = Quaternion.LookRotation((transform.position - cameraTransform.position).normalized, Vector3.up);
    }

    /// <summary>
    /// The old button was clicked
    /// </summary>
    private void ClickedOk()
    {
        _taskSource.TrySetResult(AppDialogResult.Ok);
        Close();
    }
    
    /// <summary>
    /// The old button was clicked
    /// </summary>
    private void ClickedNo()
    {
        _taskSource.TrySetResult(AppDialogResult.No);
        Close();
    }

    /// <summary>
    /// The cancel button was clicked
    /// </summary>
    private void ClickedCanceled()
    {
        _taskSource.TrySetResult(AppDialogResult.Cancel);
        Close();
    }
    #endregion Private Functions
}
