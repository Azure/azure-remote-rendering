using Microsoft.Azure.RemoteRendering;
using System;
using UnityEngine.Events;

[Serializable] public class UnityRemoteEntityEvent : UnityEvent<Entity> { }
[Serializable] public class UnityBoolEvent : UnityEvent<bool> { }
[Serializable] public class UnityFloatEvent : UnityEvent<float> { }
[Serializable] public class UnityStringEvent : UnityEvent<string> { }