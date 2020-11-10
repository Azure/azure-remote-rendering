// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

[CreateAssetMenu(fileName = "RemoteObject", menuName = "Remoting/Object", order = 1)]
public class RemoteObjectData : ScriptableObject
{
    public RemoteModel Model = new RemoteModel();
}
