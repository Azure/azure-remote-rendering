// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions;
using Microsoft.MixedReality.Toolkit.Extensions.Sharing.Communication;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SharingRoomListLoader : MonoBehaviour
{
    #region Serialized Fields
    [Header("Parts")]

    [SerializeField]
    [Tooltip("The list target for the loaded data.")]
    private ListItemRepeater target = null;

    [SerializeField]
    [Tooltip("Text mesh pro text showing no rooms are available.")]
    private TextMeshPro noRoomsText = null;
    
    /// <summary>
    /// The list target for the loaded data.
    /// </summary>
    public ListItemRepeater Target
    {
        get => target;
        set => target = value;
    }
    #endregion Serialized Fields

    #region Public Properties
    #endregion Public Properties

    #region MonoBehaviour Functions
    private void Start()
    {
        AppServices.SharingService.RoomsChanged += OnRoomsChanged;
        LoadData(AppServices.SharingService.Rooms);
    }

    private void OnDestroy()
    {
        AppServices.SharingService.RoomsChanged -= OnRoomsChanged;
    }
    #endregion MonoBehaviour Functions

    #region Public Functions
    #endregion Public Functions

    #region Private Functions
    private void OnRoomsChanged(ISharingService sender, IReadOnlyCollection<ISharingServiceRoom> rooms)
    {
        LoadData(rooms);
    }

    private void LoadData(IReadOnlyCollection<ISharingServiceRoom> rooms)
    {
        int count = rooms?.Count ?? 0;
        List<object> objectData = new List<object>(count + 1);

        objectData.Add("Back");

        if (count > 0)
        {
            foreach (var room in rooms)
            {
                if (room != null)
                {
                    objectData.Add(room);
                }
            }
        }

        ApplyData(objectData);
        noRoomsText.gameObject.SetActive(count == 0);
    }

    private void ApplyData(List<object> objectData)
    {
        if (objectData == null)
        {
            return;
        }

        if (target != null)
        {
            target.DataSource = objectData;
        }
    }
    #endregion Private Functions

    #region Private Class
    private class TempSharingServiceRoom : ISharingServiceRoom
    {
        public string Name { get; set; }
    }
    #endregion Private Class
}

