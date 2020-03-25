using System;
using UnityEngine;
using UnityEngine.Events;

#if ENABLE_WINMD_SUPPORT
using Windows.UI.Input.Spatial;
#endif

public class AirTapDetector : MonoBehaviour
{
    public UnityEvent OnPressed = new UnityEvent();

#if ENABLE_WINMD_SUPPORT
    SpatialInteractionManager spatialInteractionManager = null;
#endif

    private void Awake()
    {
#if ENABLE_WINMD_SUPPORT
        if (!UnityEngine.WSA.Application.RunningOnUIThread())
        {
            UnityEngine.WSA.Application.InvokeOnUIThread(() =>
            {
                spatialInteractionManager = SpatialInteractionManager.GetForCurrentView();
                spatialInteractionManager.SourcePressed += SpatialInteractionManager_SourcePressed;
            }, false);
        }
#endif
    }

    private void OnDestroy()
    {
#if ENABLE_WINMD_SUPPORT
        spatialInteractionManager.SourcePressed -= SpatialInteractionManager_SourcePressed;
#endif
    }

#if ENABLE_WINMD_SUPPORT
    private void SpatialInteractionManager_SourcePressed(SpatialInteractionManager sender, SpatialInteractionSourceEventArgs args)
    {
        if (!UnityEngine.WSA.Application.RunningOnAppThread())
        {
            UnityEngine.WSA.Application.InvokeOnAppThread(() =>
            {
                TriggerEvent();
            }, false);
        }
        else
        {
            TriggerEvent();
        }
    }
#endif

#if UNITY_EDITOR
    private void Update()
    {
        // simulated airtap
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TriggerEvent();
        }
    }
#endif

    public void TriggerEvent()
    {
        if (OnPressed != null)
        {
            OnPressed.Invoke();
        }
    }
}
