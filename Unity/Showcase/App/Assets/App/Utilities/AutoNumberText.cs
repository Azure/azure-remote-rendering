// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using TMPro;
using UnityEngine;

public class AutoNumberText : MonoBehaviour
{
    private int currentNumber = -1;

    #region Serialized Fields
    [SerializeField]
    [Tooltip("Should the materials be updated in the editor. If false, colors will only be update at runtime.")]
    private bool updateInEditor = false;

    /// <summary>
    /// Should the materials be updated in the editor. If false, colors will only be update at runtime.
    /// </summary>
    public bool UpdateInEditor
    {
        get => updateInEditor;
        set => updateInEditor = value;
    }

    [SerializeField]
    [Tooltip("The frequency at which the number should be changed.")]
    private uint updateFrequency = 1;

    /// <summary>
    /// The frequency at which the number should be changed.
    /// </summary>
    public uint UpdateFrequency
    {
        get => updateFrequency;
        set => updateFrequency = value;
    }

    [SerializeField]
    [Tooltip("The amount by which the auto numbered value will be increased.")]
    public int updateAmount = 1;

    /// <summary>
    /// The amount by which the auto numbered value will be increased.
    /// </summary>
    public int UpdateAmount
    {
        get => updateAmount;
        set => updateAmount = value;
    }


    [SerializeField]
    [Tooltip("The first number in the auto numbered sequence.")]
    private int startNumber = 1;

    /// <summary>
    /// The first number in the auto numbered sequence.
    /// </summary>
    public int StartNumber
    {
        get => startNumber;
        set => startNumber = value;
    }
    #endregion Serialized Fields

    #region MonoBehavior Functions
    private void OnValidate()
    {
        if (updateInEditor && !Application.isPlaying)
        {
            UpdateAllTextNumbers();
        }
    }

    private void Start()
    {
        UpdateAllTextNumbers();
    }
    #endregion MonoBehavior Functions

    #region Private Functions
    private void UpdateAllTextNumbers()
    {
        if (updateFrequency == 0)
        {
            return;
        }

        ResetNumber();
        var children = GetComponentsInChildren<TextMeshPro>();
        if (children != null && children.Length > 0)
        {
            int length = children.Length;
            int number = -1;
            for (int i = 0; i < length; i++)
            {
                if (i % updateFrequency == 0)
                {
                    number = NextNumber();
                }
                UpdateTextNumber(children[i], number);
            }
        }
    }

    private void ResetNumber()
    {
        currentNumber = 0;
    }

    private int NextNumber()
    {
        currentNumber = currentNumber + updateAmount;
        return currentNumber;
    }

    private void UpdateTextNumber(TextMeshPro textMesh, int number)
    {
        textMesh.text = number.ToString();
    }
    #endregion Private Functions
}
