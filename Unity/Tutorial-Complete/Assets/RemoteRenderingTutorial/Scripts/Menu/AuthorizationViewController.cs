using TMPro;
using UnityEngine;

public class AuthorizationViewController : MonoBehaviour
{
    public GameObject buttonApprove;
    public GameObject authInstructions;
    private bool usesAuthConnection = false;
    BaseARRAuthentication authScript;
    private TextMeshPro textMeshPro;

    private void Start()
    {
        RemoteRenderingCoordinator.CoordinatorStateChange += OnCoordinatorStateChange;
        OnCoordinatorStateChange(RemoteRenderingCoordinator.instance.CurrentCoordinatorState);

        authScript = RemoteRenderingCoordinator.instance.gameObject.GetComponent<BaseARRAuthentication>();
        if (authScript != null)
        {
            authScript.AuthenticationInstructions += AuthScript_AuthenticationInstructions;
        }

        textMeshPro = authInstructions.GetComponentInChildren<TextMeshPro>(true);
    }

    private void OnDestroy()
    {
        RemoteRenderingCoordinator.CoordinatorStateChange -= OnCoordinatorStateChange;
        if (authScript != null)
        {
            authScript.AuthenticationInstructions -= AuthScript_AuthenticationInstructions;
        }
    }

    public void ConnectPressed()
    {
        RemoteRenderingCoordinator.instance.Authorized = true;
    }
    
    private void OnCoordinatorStateChange(RemoteRenderingCoordinator.RemoteRenderingState state)
    {
        // If any of the game objects is not set or already destroyed on shutdown skip update
        if (gameObject == null || buttonApprove == null || authInstructions == null)
        {
            return;
        }
        switch (state)
        {
            case RemoteRenderingCoordinator.RemoteRenderingState.NotSet:
            case RemoteRenderingCoordinator.RemoteRenderingState.NotInitialized:
            case RemoteRenderingCoordinator.RemoteRenderingState.ConnectingToExistingRemoteSession:
            case RemoteRenderingCoordinator.RemoteRenderingState.ConnectingToNewRemoteSession:
            case RemoteRenderingCoordinator.RemoteRenderingState.RemoteSessionReady:
            case RemoteRenderingCoordinator.RemoteRenderingState.ConnectingToRuntime:
            case RemoteRenderingCoordinator.RemoteRenderingState.RuntimeConnected:
                gameObject.SetActive(false);
                break;

            case RemoteRenderingCoordinator.RemoteRenderingState.NoSession:
                gameObject.SetActive(usesAuthConnection);
                break;

            case RemoteRenderingCoordinator.RemoteRenderingState.NotAuthorized:
                gameObject.SetActive(true);
                buttonApprove.SetActive(true);
                authInstructions.SetActive(false);
                break;
        }
    }

    private void AuthScript_AuthenticationInstructions(string instructions)
    {
        gameObject.SetActive(true);
        buttonApprove.SetActive(false);
        authInstructions.SetActive(true);
        textMeshPro.text = instructions;
    }
}
