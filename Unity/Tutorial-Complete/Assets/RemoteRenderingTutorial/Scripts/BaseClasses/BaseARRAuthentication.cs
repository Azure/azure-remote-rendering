using Microsoft.Azure.RemoteRendering;
using Microsoft.Identity.Client;
using System;
using System.Threading.Tasks;
using UnityEngine;

public abstract class BaseARRAuthentication : MonoBehaviour
{
    public abstract event Action<string> AuthenticationInstructions;

    public abstract Task<SessionConfiguration> GetAARCredentials();
    public abstract Task<AuthenticationResult> TryLogin();
}
