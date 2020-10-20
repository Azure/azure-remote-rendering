using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;

public class AlignToViewport : MonoBehaviour
{
    int width, height;
    public Vector2 viewportPos;
    public Vector3 offset;

    private float menuDepth;

    private void Awake()
    {
        menuDepth = CameraCache.Main.transform.InverseTransformPoint(transform.position).z;
    }

    private void Start()
    {
        ConfigureAlignment();
    }

    void ConfigureAlignment()
    {
        Vector3 viewport = viewportPos;
        viewport.z = menuDepth;
        transform.position = CameraCache.Main.ViewportToWorldPoint(viewport);
        transform.localPosition += offset;
        width = Screen.width;
        height = Screen.height;
    }

    private void Update()
    {
        if (width != Screen.width || height != Screen.height)
            ConfigureAlignment();
    }
}
