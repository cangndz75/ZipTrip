using System.Collections;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ZipTrip.Tests.PlayMode
{
    public class BootSceneTests
    {
        [UnityTest]
        public IEnumerator BootScene_CanLoad()
        {
            yield return SceneManager.LoadSceneAsync("Boot");

            Assert.AreEqual("Boot", SceneManager.GetActiveScene().name);
        }
    }
}