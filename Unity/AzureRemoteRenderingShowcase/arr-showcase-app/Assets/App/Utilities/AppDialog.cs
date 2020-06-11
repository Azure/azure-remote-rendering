// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.Utilities;
using System.Threading.Tasks;
using UnityEngine;

public class AppDialog : MonoBehaviour
{
    private TaskCompletionSource<bool> _taskSource = new TaskCompletionSource<bool>();

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
    [Tooltip("The button clicked for cancel the dialog request.")]
    public Interactable cancelButton;

    /// <summary>
    /// The button clicked for cancel the dialog request.
    /// </summary>
    public Interactable CancelButton
    {
        get => cancelButton;
        set => cancelButton = value;
    }
    #endregion Serialized Fields

    #region Public Properties
    /// <summary>
    /// The task completed once the dialog closes
    /// </summary>
    public Task<bool> DiaglogTask => _taskSource.Task;
    #endregion Public Properties

    #region MonoBehavior Functions
    private void Start()
    {
        if (okButton != null)
        {
            okButton.OnClick.AddListener(ClickedOk);
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
        _taskSource.TrySetResult(false);
    }
    #endregion MonoBehavior Functions

    #region Public Functions
    /// <summary>
    /// Open the dialog
    /// </summary>
    public Task<bool> Open()
    {
        if (gameObject != null)
        {
            gameObject.SetActive(true);
        }

        return DiaglogTask;
    }

    /// <summary>
    /// Close the dialog control
    /// </summary>
    public void Close(bool allowDestroy = true)
    {
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
        _taskSource.TrySetResult(true);
        Close();
    }

    /// <summary>
    /// The cancel button was clicked
    /// </summary>
    private void ClickedCanceled()
    {
        _taskSource.TrySetResult(false);
        Close();
    }
    #endregion Private Functions
}
