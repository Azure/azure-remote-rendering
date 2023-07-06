using UnityEngine;

using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit;


/// <summary>
/// Helper class to enable the MRTK articulated hand mesh on certain platforms, i.e. Quest.
/// </summary>
public class HandMeshEnabler : MonoBehaviour
{
    /// <summary>
    /// Whether the hand mesh should be enabled on this platform.
    /// </summary>
    private bool EnableHandMesh
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }

    private void Start()
    {
        if (this.EnableHandMesh)
        {
            MixedRealityInputSystemProfile inputSystemProfile = CoreServices.InputSystem?.InputSystemProfile;
            if (inputSystemProfile != null)
            {
                MixedRealityHandTrackingProfile handTrackingProfile = inputSystemProfile.HandTrackingProfile;
                if (handTrackingProfile != null)
                {
                    handTrackingProfile.EnableHandMeshVisualization = true;
                }
            }
        }

        this.enabled = false;
        Destroy(this);
    }
}
