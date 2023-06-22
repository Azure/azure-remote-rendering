using UnityEngine;

using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit;


/// <summary>
/// Helper class to enable the MRTK articulated hand mesh on certain platforms, i.e. Quest.
/// </summary>
public class HandMeshEnabler : MonoBehaviour
{
    void Start()
    {
#if UNITY_EDITOR || !UNITY_ANDROID
        return;
#endif

        MixedRealityInputSystemProfile inputSystemProfile = CoreServices.InputSystem?.InputSystemProfile;
        if (inputSystemProfile == null)
        {
            return;
        }

        MixedRealityHandTrackingProfile handTrackingProfile = inputSystemProfile.HandTrackingProfile;
        if (handTrackingProfile != null)
        {
            handTrackingProfile.EnableHandMeshVisualization = true;
        }
    }
}
