using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Small utility script to additively load a scene, e.g. as background environment for VR.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    [SerializeField] private string scenePath = string.Empty;
    [SerializeField] private bool loadInEditor = false;
    [SerializeField] private bool loadOnVR = false;


    /// <remarks>
    /// This must be run in 'Start'. Otherwise during 'Awake' and 'OnEnable' 'Scene.isLoaded' returns 'false'.
    /// </remarks>
    private void Start()
    {
        bool doLoadScene = (XRUtility.IsOnVR && this.loadOnVR) || (Application.isEditor && this.loadInEditor);
        Scene scene = SceneManager.GetSceneByPath(this.scenePath);

        if (doLoadScene && (scene != null) && !scene.isLoaded)
        {
            SceneManager.LoadScene(this.scenePath, LoadSceneMode.Additive);
        }

        this.enabled = false;
        this.gameObject.SetActive(false);
        Destroy(this.gameObject);
    }
}

