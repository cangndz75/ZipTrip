using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ZipTrip.Domain.Puzzle;
using ZipTrip.Unity;

namespace ZipTrip.Tests.EditMode
{
    // ZT-040C: the golden Cabin Suitcase prefab honours the container presentation contract, and the unchanged Lv1 / Lv2
    // BoardSpecs are seated inside its authored interior (the art adapts to the board, never the reverse).
    public sealed class GoldenContainerTests
    {
        private const string PrefabPath = "Assets/Art/Models/Containers/CabinSuitcase/Prefabs/CabinSuitcase_Golden.prefab";
        private const float AuthoredLidOpenX = -77.08f;

        private static GameObject Prefab() =>
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) ?? throw new AssertionException("Missing " + PrefabPath);

        private static Rect BoardRect(string levelId)
        {
            var json = File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Resources/LevelsV2", levelId + ".json"));
            var compartment = LevelJsonLoaderV2.Load(json, PuzzleItemCatalog.Create()).Spec.Board.Compartments.Single();
            return new Rect(0f, -compartment.Height, compartment.Width, compartment.Height);
        }

        private static ContainerFit Seat(ContainerRig rig, Rect board) =>
            ContainerFit.Seat(board, rig.InteriorMinLocal, rig.InteriorMaxLocal, PuzzleBoardPresenter.ContainerPadding,
                SuitcaseShell.LiningY, PuzzleBoardPresenter.ContainerYaw);

        [Test]
        public void Prefab_HasTheContainerContract_AndIsScriptAndColliderFree()
        {
            var prefab = Prefab();
            var rig = ContainerRig.Bind(prefab.transform);
            Assert.That(rig.Root, Is.SameAs(prefab.transform));
            foreach (var name in ContainerRig.RequiredNodes.Append("Interior"))
                Assert.That(prefab.transform.Find(name), Is.Not.Null, name);
            Assert.That(prefab.GetComponentsInChildren<Component>(true), Has.None.Null, "no missing scripts");
            Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty, "container art is script-free");
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty, "no gameplay colliders");
            Assert.That(prefab.GetComponentsInChildren<Animator>(true), Is.Empty);
        }

        [Test]
        public void Bind_RejectsArtMissingContractNodes()
        {
            var go = new GameObject("Not a container");
            try
            {
                new GameObject(ContainerRig.BaseNode).transform.SetParent(go.transform);
                Assert.That(() => ContainerRig.Bind(go.transform),
                    Throws.InvalidOperationException.With.Message.Contains(ContainerRig.LidNode));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BaseAndLid_AreSeparateRenderedNodes_WithExplicitMaterialSlots()
        {
            var prefab = Prefab();
            var rig = ContainerRig.Bind(prefab.transform);
            Assert.That(rig.Lid.IsChildOf(rig.Base) || rig.Base.IsChildOf(rig.Lid), Is.False);
            // Remapped by authored material name; Unity's submesh order differs per node, so compare as sets.
            string[] Slots(Transform t) => t.GetComponent<MeshRenderer>().sharedMaterials.Select(m => m.name).ToArray();
            Assert.That(Slots(rig.Base), Is.EquivalentTo(new[] { "M_CabinSuitcase_Exterior", "M_CabinSuitcase_Lining" }));
            Assert.That(Slots(rig.Lid), Is.EquivalentTo(new[] { "M_CabinSuitcase_Exterior", "M_CabinSuitcase_Lining" }));
            Assert.That(Slots(prefab.transform.Find("Interior")), Is.EqualTo(new[] { "M_CabinSuitcase_Lining" }));
            var materials = rig.Base.GetComponent<MeshRenderer>().sharedMaterials;
            var exterior = materials.Single(m => m.name == "M_CabinSuitcase_Exterior");
            var liningMaterial = materials.Single(m => m.name == "M_CabinSuitcase_Lining");
            Assert.That(exterior.GetTexture("_BaseMap").name, Is.EqualTo("T_CabinSuitcase_Exterior_BaseColor"));
            Assert.That(liningMaterial.GetTexture("_BaseMap"), Is.Null, "lining has no exterior texture");
            Assert.That(AssetDatabase.GetAssetPath(exterior), Does.EndWith("Materials/M_CabinSuitcase_Exterior.mat"),
                "explicit Unity material, not an FBX-generated one");
            var lining = liningMaterial.GetColor("_BaseColor");
            Assert.That(new[] { lining.r, lining.g, lining.b }, Is.EqualTo(new[] { 0.10f, 0.27f, 0.28f }).Within(0.005f));
        }

        [Test]
        public void Lid_OpensAsAuthored_IdentityClosesFlush_BaseNeverMoves()
        {
            var instance = Object.Instantiate(Prefab());
            try
            {
                var rig = ContainerRig.Bind(instance.transform);
                var open = rig.LidOpenLocalRotation.eulerAngles;
                Assert.That(Mathf.DeltaAngle(open.x, AuthoredLidOpenX), Is.EqualTo(0f).Within(0.1f), "open pose");
                Assert.That(Mathf.DeltaAngle(open.y, 0f) + Mathf.DeltaAngle(open.z, 0f), Is.EqualTo(0f).Within(0.01f));
                Assert.That(ContainerRig.LidClosedLocalRotation, Is.EqualTo(Quaternion.identity));
                Assert.That((rig.Lid.position - rig.HingeAnchor.position).magnitude, Is.LessThan(1e-4f), "lid pivot on hinge");

                var baseBounds = rig.Base.GetComponent<MeshRenderer>().bounds;
                var basePose = (rig.Base.position, rig.Base.rotation);
                var openTop = rig.Lid.GetComponent<MeshRenderer>().bounds.max.y;
                rig.SetLidClosed(true);
                Assert.That(rig.LidClosed, Is.True);
                var lid = rig.Lid.GetComponent<MeshRenderer>().bounds;
                var rim = rig.InteriorMax.position.y;
                Assert.That(lid.min.x, Is.GreaterThanOrEqualTo(baseBounds.min.x - 0.005f));
                Assert.That(lid.max.x, Is.LessThanOrEqualTo(baseBounds.max.x + 0.005f));
                Assert.That(lid.max.y, Is.LessThan(openTop * 0.6f), "closed lid lies down instead of standing open");
                Assert.That(lid.center.y, Is.GreaterThan(rim), "closed lid sits on the rim, not inside the base");
                // Flush: the shells' side-wall bands (hardware excluded) span the same depth and meet at the rim.
                var lidWall = SideWall(rig.Lid);
                var baseWall = SideWall(rig.Base);
                Assert.That(lidWall.min.z, Is.EqualTo(baseWall.min.z).Within(0.003f), "back edges meet");
                Assert.That(lidWall.max.z, Is.EqualTo(baseWall.max.z).Within(0.003f), "front edges meet");
                Assert.That(lidWall.min.y, Is.EqualTo(rim).Within(0.003f), "lid rim rests on the base rim");
                Assert.That((rig.Base.position, rig.Base.rotation), Is.EqualTo(basePose), "base does not move");

                rig.SetLidClosed(false);
                Assert.That(rig.Lid.localRotation, Is.EqualTo(rig.LidOpenLocalRotation));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        // World bounds of a node's shell side walls (|x| 0.40..0.445 m), as in the Blender validation.
        private static Bounds SideWall(Transform node)
        {
            var mesh = node.GetComponent<MeshFilter>().sharedMesh;
            var points = mesh.vertices.Select(v => node.TransformPoint(v)).Where(p => Mathf.Abs(p.x) > 0.40f && Mathf.Abs(p.x) < 0.445f).ToArray();
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var p in points)
                bounds.Encapsulate(p);
            return bounds;
        }

        [TestCase("lv1-fit", 0.3f, 0.6f)]
        [TestCase("lv2-rotate", 0.75f, 1.1f)]
        public void Board_IsSeatedInsideTheAuthoredInterior(string levelId, float minSide, float maxSide)
        {
            var rig = ContainerRig.Bind(Prefab().transform);
            var board = BoardRect(levelId);
            var fit = Seat(rig, board);
            var interior = fit.Interior(rig.InteriorMinLocal, rig.InteriorMaxLocal);
            var pad = PuzzleBoardPresenter.ContainerPadding - 1e-4f;
            Assert.That(interior.xMin, Is.LessThanOrEqualTo(board.xMin - pad), "left");
            Assert.That(interior.xMax, Is.GreaterThanOrEqualTo(board.xMax + pad), "right");
            Assert.That(interior.yMin, Is.LessThanOrEqualTo(board.yMin - pad), "front");
            Assert.That(interior.yMax, Is.GreaterThanOrEqualTo(board.yMax + pad), "back");
            Assert.That(board.xMin - interior.xMin, Is.EqualTo(interior.xMax - board.xMax).Within(1e-4f), "centred");
            Assert.That(board.xMin - interior.xMin, Is.InRange(minSide, maxSide), "side margin (cells)");
            Assert.That(interior.yMax - board.yMax, Is.EqualTo(PuzzleBoardPresenter.ContainerPadding).Within(1e-3f),
                "depth-limited: the 7 rows use the full interior depth");

            var hinge = fit.Position + fit.Rotation * (rig.Root.InverseTransformPoint(rig.HingeAnchor.position) * fit.Scale);
            Assert.That(hinge.z, Is.GreaterThan(board.yMax), "hinge and lid behind the board, handle towards the camera");
            var again = Seat(rig, board);
            Assert.That((again.Scale, again.Position, again.Rotation), Is.EqualTo((fit.Scale, fit.Position, fit.Rotation)), "deterministic");
        }

        [Test]
        public void Lv1AndLv2_ShareOneSuitcaseSize()
        {
            var rig = ContainerRig.Bind(Prefab().transform);
            Assert.That(Seat(rig, BoardRect("lv2-rotate")).Scale, Is.EqualTo(Seat(rig, BoardRect("lv1-fit")).Scale).Within(1e-5f));
        }

        [Test]
        public void Seat_RejectsADegenerateInterior()
        {
            Assert.That(() => ContainerFit.Seat(new Rect(0f, -7f, 5f, 7f), Vector3.zero, new Vector3(1f, 0f, 0f), 0f, 0f, 0f),
                Throws.ArgumentException);
        }
    }
}
