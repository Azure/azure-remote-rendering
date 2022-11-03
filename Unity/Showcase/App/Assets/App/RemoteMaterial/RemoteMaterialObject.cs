// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

[CreateAssetMenu(fileName = "RemoteMaterial", menuName = "Remoting/Material", order = 1)]
public class RemoteMaterialObject : ScriptableObject
{
    private void OnEnable()
    {
        if (Data != null &&
            string.IsNullOrEmpty(Data.Name))
        {
            Data.Name = name;
        }
    }

    [Tooltip("The material data")]
    public RemoteMaterial Data = new RemoteMaterial();
}
