using System.Collections.Generic;

using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Small helper functions for dealing with XR in Unity.
/// </summary>
public static class XRUtility
{
    private static bool? isOnVR = null;

    /// <summary>
    /// Is the app running on a VR system.
    /// Checks for an opaque display.
    /// </summary>
    public static bool IsOnVR
    {
        get
        {
            if (!isOnVR.HasValue)
            {
                List<XRDisplaySubsystem> displaySubsystems = new List<XRDisplaySubsystem>();
                SubsystemManager.GetInstances<XRDisplaySubsystem>(displaySubsystems);

                isOnVR = false;
                foreach (XRDisplaySubsystem subsystem in displaySubsystems)
                {
                    if (subsystem.displayOpaque)
                    {
                        isOnVR = true;
                        break;
                    }
                }

            }

            return isOnVR.Value;
        }
    }
}
