// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

/// <summary>
/// Represents a list containing remote objects. Currently this is only used to pass along the stage object to the list items.
/// </summary>
public class RemoteObjectsList : MonoBehaviour
{
    [Tooltip("The stage this list should load items into")]
    public RemoteObjectStage Stage;
}
