﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

public class EnumFlagAttribute : PropertyAttribute
{
    public string name;

    public EnumFlagAttribute() { }

    public EnumFlagAttribute(string name)
    {
        this.name = name;
    }
}