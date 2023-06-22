using UnityEngine;

/// <summary>
/// Small utility script to scale an object on specific platforms when app is runnning in VR.
/// </summary>
public class ObjectScaler : MonoBehaviour
{
    [SerializeField] private float scale = 1f;
    [SerializeField] private bool runInEditor = false;
    [SerializeField] private bool runOnVR = false;


    private void OnEnable()
    {
        if ((XRUtility.IsOnVR && this.runOnVR) || (Application.isEditor && this.runInEditor))
        {
            this.transform.localScale *= this.scale;
        }

        this.enabled = false;
        Destroy(this);
    }
}
