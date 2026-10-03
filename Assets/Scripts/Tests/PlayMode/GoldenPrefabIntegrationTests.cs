using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZipTrip.Unity;

namespace ZipTrip.Tests.PlayMode
{
    public sealed class GoldenPrefabIntegrationTests
    {
        [UnityTest]
        public IEnumerator CatalogMapsAllGoldenStatesAndKeepsPrefabsScriptFree()
        {
            yield return SceneManager.LoadSceneAsync("PuzzleGameplay");
            yield return null;

            var catalog = UnityEngine.Object.FindFirstObjectByType<GoldenItemPrefabCatalog>();
            Assert.IsNotNull(catalog);
            AssertMapping(catalog, "PF_Item_Laptop", "open", "PF_Item_Laptop");
            AssertMapping(catalog, "PF_Item_SneakerPair", "open", "PF_Item_SneakerPair");
            AssertMapping(catalog, "PF_Item_Sweater", "open", "PF_Item_SweaterOpen");
            AssertMapping(catalog, "PF_Item_Sweater", "folded", "PF_Item_SweaterFolded");
            Assert.That(() => catalog.Resolve("PF_Item_Sweater", "unknown"),
                Throws.ArgumentException);
        }

        private static void AssertMapping(GoldenItemPrefabCatalog catalog, string visualId,
            string state, string expectedName)
        {
            var prefab = catalog.Resolve(visualId, state);
            Assert.AreEqual(expectedName, prefab.name);
            Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.IsEmpty(prefab.GetComponentsInChildren<MonoBehaviour>(true));
            Assert.IsNotEmpty(prefab.GetComponentsInChildren<MeshRenderer>(true));
        }
    }
}
