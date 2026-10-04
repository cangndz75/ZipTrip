using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZipTrip.Unity;

namespace ZipTrip.Tests.PlayMode
{
    public class BootSceneTests
    {
        [UnityTest]
        public IEnumerator FirstBuildScene_StartsGameplay()
        {
            Assert.AreEqual("Assets/Scenes/PuzzleGameplay.unity", SceneUtility.GetScenePathByBuildIndex(0));

            yield return SceneManager.LoadSceneAsync(0);
            yield return null;

            Assert.AreEqual("PuzzleGameplay", SceneManager.GetActiveScene().name);
            Assert.IsNotNull(Object.FindFirstObjectByType<PuzzleGameplayScene>()?.Session);
        }

        [UnityTest]
        public IEnumerator BootScene_CanLoad()
        {
            yield return SceneManager.LoadSceneAsync("Boot");

            Assert.AreEqual("Boot", SceneManager.GetActiveScene().name);
        }
    }
}