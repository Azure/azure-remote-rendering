using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tests
{
    public class OpenSceneTest
    {
        [UnityTest]
        [Timeout(40 * 1000)]
        public IEnumerator OpenScene()
        {
            SceneManager.LoadScene(0);
            return null;
        }
    }
}
