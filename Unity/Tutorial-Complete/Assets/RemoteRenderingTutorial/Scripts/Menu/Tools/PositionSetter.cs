using UnityEngine;

public class PositionSetter : MonoBehaviour
{
    public float xOffset = 0.0f;
    public float yOffset = 0.0f;
    public float zOffset = 0.0f;

    public void SetPositionInFrontOfCamera()
    {
        this.transform.position = Camera.main.transform.position + (Camera.main.transform.right * xOffset) + (Camera.main.transform.up * yOffset) + (Camera.main.transform.forward * zOffset);
        this.transform.rotation = new Quaternion(0.0f, Camera.main.transform.rotation.y, 0.0f, Camera.main.transform.rotation.w);
    }
}
