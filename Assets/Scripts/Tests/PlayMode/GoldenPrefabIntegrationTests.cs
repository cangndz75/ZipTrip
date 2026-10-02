using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZipTrip.Domain;
using ZipTrip.Unity;

namespace ZipTrip.Tests.PlayMode
{
    public sealed class GoldenPrefabIntegrationTests
    {
        [UnityTest]
        public IEnumerator CatalogMapsAllGoldenStatesAndKeepsPrefabsScriptFree()
        {
            yield return SceneManager.LoadSceneAsync("GameplaySandbox");
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

        [UnityTest]
        public IEnumerator GoldenMeshesStayInsideHiddenFootprintForEveryAllowedRotation()
        {
            yield return SceneManager.LoadSceneAsync("GameplaySandbox");
            yield return null;

            var catalog = UnityEngine.Object.FindFirstObjectByType<GoldenItemPrefabCatalog>();
            var items = PhaseAItemCatalog.Create(true).ToDictionary(item => item.Id);
            foreach (var sample in new[]
            {
                (Item: items["laptop"], State: "open"),
                (Item: items["sneaker"], State: "open"),
                (Item: items["sweater"], State: "open"),
                (Item: items["sweater"], State: "folded")
            })
            {
                foreach (var rotation in sample.Item.AllowedRotations)
                {
                    var root = new GameObject(sample.Item.Id + " " + sample.State + " " + rotation);
                    var view = root.AddComponent<ItemView>();
                    var prefab = catalog.Resolve(sample.Item.VisualPrefabId, sample.State);
                    view.Present(sample.Item, sample.State, rotation, false, Vector3.zero,
                        1f, Color.white, prefab);
                    AssertVisualInsideFootprint(view, 0.001f);
                    UnityEngine.Object.Destroy(root);
                }
            }
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

        private static void AssertVisualInsideFootprint(ItemView view, float tolerance)
        {
            var renderers = view.VisualRoot.GetComponentsInChildren<Renderer>();
            Assert.IsNotEmpty(renderers);
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            var width = view.Footprint.OccupiedCells.Max(cell => cell.X) + 1;
            var height = view.Footprint.OccupiedCells.Max(cell => cell.Y) + 1;
            Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(-tolerance));
            Assert.That(bounds.max.x, Is.LessThanOrEqualTo(width + tolerance));
            Assert.That(bounds.max.z, Is.LessThanOrEqualTo(tolerance));
            Assert.That(bounds.min.z, Is.GreaterThanOrEqualTo(-height - tolerance));
            Assert.That(bounds.min.y, Is.EqualTo(0f).Within(tolerance));
            Assert.IsEmpty(view.VisualRoot.GetComponentsInChildren<MonoBehaviour>(true));
        }
    }
}
