// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class AppDialog : MonoBehaviour
{
    private TaskCompletionSource<AppDialogResult> _taskSource = new TaskCompletionSource<AppDialogResult>();

    public enum AppDialogResult
    {
        Ok,
        No,
        Cancel
    }

    [Flags]
    public enum AppDialogButtons
    {
        Ok = 0x01,
        No = 0x02,
        Cancel = 0x04,

        None = 0x0,
        All = Ok | No | Cancel,        
    }

    public enum AppDialogLocation
    {
        /// <summary>
        /// The dialog will be placed in front of the user.
        /// </summary>
        Default,

        /// <summary>
        /// The dialog will be placed in the app's menu
        /// </summary>
        Menu
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

    [EnumFlag]
    [SerializeField]
    [Tooltip("The buttons to show in the dialog.")]
    private AppDialogButtons buttons = AppDialogButtons.All;

    /// <summary>
    /// The buttons to show in the dialog.
    /// </summary>
    public AppDialogButtons Buttons
    {
        get => buttons;
        set => buttons = value;
    }

    [Header("Object Parts")]

    [SerializeField]
    [Tooltip("The button container that layouts buttons.")]
    private GridObjectCollection buttonContainer;

    /// <summary>
    /// The button container that layouts buttons.
    /// </summary>
    public GridObjectCollection ButtonContainer
    {
        get => buttonContainer;
        set => buttonContainer = value;
    }

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
    private TextMeshPro dialogHeaderText;

    /// <summary>
    /// The dialog of the text
    /// </summary>
    public TextMeshPro DialogHeaderText
    {
        get => dialogHeaderText;
        set => dialogHeaderText = value;
    }

    [SerializeField]
    [Tooltip("The text of the dialog.")]
    private TextMeshPro dialogText;

    /// <summary>
    /// The dialog of the text
    /// </summary>
    public TextMeshPro DialogText
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


    [Header("Events")]

    [SerializeField]
    [Tooltip("Event raised when the dialog is opened.")]
    private UnityEvent onOpened = new UnityEvent();

    /// <summary>
    /// Event raised when the dialog is opened.
    /// </summary>
    public UnityEvent OnOpened
    {
        get => onOpened;
        set => onOpened = value;
    }

    [SerializeField]
    [Tooltip("Event raised when the dialog is closed.")]
    private UnityEvent onClosed = new UnityEvent();

    /// <summary>
    /// Event raised when the dialog is closed.
    /// </summary>
    public UnityEvent OnClose
    {
        get => onClosed;
        set => onClosed = value;
    }
    #endregion Serialized Fields

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

        SetButtonActive(okButton, AppDialogButtons.Ok);
        SetButtonActive(noButton, AppDialogButtons.No);
        SetButtonActive(cancelButton, AppDialogButtons.Cancel);

        if (buttonContainer != null)
        {
            buttonContainer.UpdateCollection();
        }
    }

    /// <summary>
    /// If destroyed before user selected an option, return false.
    /// </summary>
    private void OnDestroy()
    {
        _taskSource.TrySetResult(AppDialogResult.Cancel);
    }
    #endregion MonoBehavior Functions

    #region Public Functions
    /// <summary>
    /// Open the dialog
    /// </summary>
    public async Task<AppDialogResult> Open()
    {
        if (_taskSource == null ||
            _taskSource.Task.IsCompleted)
        {
            _taskSource = new TaskCompletionSource<AppDialogResult>();
        }

        onOpened?.Invoke();

        if (gameObject != null)
        {
            gameObject.SetActive(true);
        }

        return await _taskSource.Task;
    }

    /// <summary>
    /// Close the dialog control
    /// </summary>
    public void Close(bool allowDestroy = true)
    {
        onClosed?.Invoke();

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
    /// Safely set active state of button
    /// </summary>
    private void SetButtonActive(Interactable button, AppDialogButtons type)
    {
        if (button != null)
        {
            button.gameObject.SetActive((buttons & type) != 0);
        }
    }

    /// <summary>
    /// Place dialog in front of the user.
    /// </summary>
    private void PlaceInFront()
    {
        if (startDistance > 0.0f)
        {
            Transform cameraTransform = CameraCache.Main.transform;
            transform.position = cameraTransform.position + (cameraTransform.forward.normalized * startDistance);
            transform.rotation = Quaternion.LookRotation((transform.position - cameraTransform.position).normalized, Vector3.up);
        }
    }

    /// <summary>
    /// The old button was clicked
    /// </summary>
    private void ClickedOk()
    {
        Close();
        _taskSource.TrySetResult(AppDialogResult.Ok);
    }
    
    /// <summary>
    /// The old button was clicked
    /// </summary>
    private void ClickedNo()
    {
        Close();
        _taskSource.TrySetResult(AppDialogResult.No);
    }

    /// <summary>
    /// The cancel button was clicked
    /// </summary>
    private void ClickedCanceled()
    {
        Close();
        _taskSource.TrySetResult(AppDialogResult.Cancel);
    }
    #endregion Private Functions
}
