using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ZipTrip.Tests.EditMode
{
    public sealed class ArtGate02AIntegrationTests
    {
        [Test]
        public void ApprovedInterior_IsLinkedInsideCabinWithItsFabricMaterial()
        {
            var cabin = Load("Assets/Art/Models/Containers/CabinSuitcase/Prefabs/CabinSuitcase_Golden.prefab");
            var interior = cabin.transform.Find("CabinInterior_Review");
            Assert.That(interior, Is.Not.Null);
            var renderers = interior.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(renderers, Has.Length.EqualTo(3));
            Assert.That(renderers.Select(r => r.sharedMaterial.name),
                Is.All.EqualTo("M_CabinInterior_Review"));
            Assert.That(renderers[0].sharedMaterial.GetTexture("_BaseMap").name,
                Is.EqualTo("T_CabinInterior_Review_BaseColor"));
            Assert.That(renderers[0].sharedMaterial.GetTexture("_BumpMap").name,
                Is.EqualTo("T_CabinInterior_Review_Normal"));
            AssertVisualOnly(interior.gameObject);
        }

        [TestCase("SweaterOpen", 1)]
        [TestCase("SweaterFolded", 4)]
        public void ApprovedSweater_UsesThePromotedMeshAndExistingTexture(string state, int partCount)
        {
            var prefab = Load("Assets/Art/Prefabs/Items/PF_Item_" + state + ".prefab");
            var model = prefab.transform.Find("M_Item_" + state);
            Assert.That(model, Is.Not.Null);
            var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(renderers, Has.Length.EqualTo(partCount));
            Assert.That(renderers.Select(r => r.sharedMaterial.name),
                Is.All.EqualTo("Cabin_Static_" + state));
            Assert.That(renderers[0].sharedMaterial.GetTexture("_BaseMap"), Is.Not.Null);
            AssertVisualOnly(prefab);
        }

        private static GameObject Load(string path) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new AssertionException("Missing " + path);

        private static void AssertVisualOnly(GameObject root)
        {
            Assert.That(root.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
        }
    }
}
