// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using UnityEngine;

[Serializable]
[CreateAssetMenu(fileName = "RemoteObjectDataList", menuName = "Remoting/Object List", order = 2)]
public class RemoteObjectDataList : ScriptableObject
{
    public RemoteObjectData[] Objects;
}