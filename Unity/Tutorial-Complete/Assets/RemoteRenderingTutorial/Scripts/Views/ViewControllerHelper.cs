using UnityEngine;

public abstract class ViewControllerHelper<T> : MonoBehaviour where T : Object
{
    public abstract void Initialize(T source);
}

