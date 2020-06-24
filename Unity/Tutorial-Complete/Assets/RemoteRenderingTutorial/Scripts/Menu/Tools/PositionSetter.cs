using UnityEngine;

public class PositionSetter : MonoBehaviour
{
    public float xOffset = -0.15f;
    public float zOffset = 0.3f;
    public void SetPositionInFrontOfCamera()
    {
        this.transform.position = Camera.main.transform.position + (Camera.main.transform.forward * zOffset) + (Camera.main.transform.right * xOffset);
        this.transform.rotation = new Quaternion(0.0f, Camera.main.transform.rotation.y, 0.0f, Camera.main.transform.rotation.w);
    }
}