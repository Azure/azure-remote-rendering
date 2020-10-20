// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Utilities.Solvers
{
    /// <summary>
    /// Provides a solver that overlaps with the tracked object and hides it by scaling it to zero when the tracked
    /// object isn't present.
    /// </summary>
    public class OverlapHide : Solver
    {
        /// <inheritdoc />
        public override void SolverUpdate()
        {
            var target = SolverHandler.TransformTarget;
            if (target != null)
            {
                GoalPosition = target.position;
                GoalRotation = target.rotation;
                GoalScale = Vector3.one;
            }
            else
            {
                GoalScale = Vector3.zero;
            }
        }
    }
}