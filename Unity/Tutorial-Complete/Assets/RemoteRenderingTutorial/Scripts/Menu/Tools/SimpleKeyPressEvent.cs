using UnityEngine;
using UnityEngine.Events;

public class SimpleKeyPressEvent : MonoBehaviour
{
    public KeyCode keyCode;
    public UnityEvent keyEvent;

    public void Update()
    {
        if (Input.GetKeyDown(keyCode))
            keyEvent?.Invoke();
    }
}

