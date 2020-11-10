// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

public static class QuaternionStatics
{
    #region Public Properties
    /// <summary>
    /// Represents an invalid rotation.
    /// </summary>
    public static Quaternion PositiveInfinity { get; } = new Quaternion(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);

    /// <summary>
    /// Represents an invalid rotation.
    /// </summary>
    public static Quaternion NegativeInfinity { get; } = new Quaternion(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
    #endregion Public Properties
}

