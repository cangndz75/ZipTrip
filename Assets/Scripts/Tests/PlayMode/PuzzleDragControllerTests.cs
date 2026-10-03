using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZipTrip.Domain;
using ZipTrip.Domain.Puzzle;
using ZipTrip.Unity;

namespace ZipTrip.Tests.PlayMode
{
    public sealed class PuzzleDragControllerTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.Destroy(_root);
        }

        // Harness scenario: main 5x4 L2 ((4,0) masked) with "base" 2x2 at (0,0); pocket 2x2 L1.
        // Source Tray: book 2x1, dot-a / dot-b 1x1 twins, rod 3x1, sweater 3x3.
        private PuzzleHarness Harness()
        {
            _root = new GameObject("Harness");
            var harness = _root.AddComponent<PuzzleHarness>();
            harness.Build();
            return harness;
        }

        // Grabs a tray item by the centre of its first cell.
        private static Vector3 GrabPoint(PuzzleHarness harness, string id, bool fromBoard = false)
        {
            var view = fromBoard ? harness.Board.ItemViews[id] : harness.Tray.ItemViews[id];
            var cell = view.Footprint.OccupiedCells[0];
            return view.transform.position + new Vector3(cell.X + 0.5f, 0f, -(cell.Y + 0.5f));
        }

        // Pointer position that puts the grabbed first cell's anchor on (x, y) of a compartment.
        private static Vector3 PointerFor(PuzzleHarness harness, string compartment, int x, int y) =>
            harness.Board.Compartments[compartment].transform.position + new Vector3(x + 0.5f, 0f, -(y + 0.5f));

        private static PuzzleItem Item(PuzzleHarness harness, string id)
        {
            harness.Session.CurrentState.TryGetItem(id, out var item);
            return item;
        }

        [UnityTest]
        public IEnumerator TrayItem_ValidPreview_IsPure_ThenCommitsOneMatchingMove()
        {
            var harness = Harness();
            yield return null;
            var session = harness.Session;
            var before = session.CurrentState;

            Assert.That(harness.Drag.BeginDrag("book", GrabPoint(harness, "book")), Is.EqualTo(DragBeginResult.Started));
            harness.Drag.UpdateDrag(PointerFor(harness, "main", 2, 0));
            Assert.That(harness.Drag.HasCandidate, Is.True);
            Assert.That(harness.Drag.CandidateCompartment, Is.EqualTo("main"));
            Assert.That(harness.Drag.CandidateAnchor, Is.EqualTo(new Cell(2, 0)));
            Assert.That(harness.Drag.PreviewValid, Is.True);
            Assert.That(harness.Drag.PreviewLayer, Is.EqualTo(0));
            Assert.That(harness.Drag.GhostCellCount, Is.EqualTo(2));
            Assert.That(session.CurrentState, Is.SameAs(before), "preview never mutates state");
            Assert.That(session.MoveCount, Is.Zero);
            Assert.That(session.UndoDepth, Is.Zero);

            var preview = harness.Drag.Preview;
            var step = harness.Drag.Drop();
            Assert.That(step.Move.IsAccepted, Is.True);
            Assert.That(session.MoveCount, Is.EqualTo(1));
            Assert.That(session.UndoDepth, Is.EqualTo(1));
            Assert.That(session.CurrentState.Hash, Is.EqualTo(preview.State.Hash), "preview / commit parity");
            var placement = Item(harness, "book").Location.Placement;
            Assert.That((placement.Compartment, placement.Anchor, placement.Rotation, placement.Layer),
                Is.EqualTo(("main", new Cell(2, 0), Rotation.Degrees0, 0)));
            Assert.That(harness.Board.ItemViews.ContainsKey("book"), Is.True);
            Assert.That(harness.Tray.ItemViews.ContainsKey("book"), Is.False);
            Assert.That(harness.Drag.IsDragging, Is.False);
            Assert.That(harness.Drag.GhostCellCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator LayerResolution_PreviewShowsLayer1AndCommitMatches()
        {
            var harness = Harness();
            yield return null;
            harness.Drag.BeginDrag("dot-a", GrabPoint(harness, "dot-a"));
            harness.Drag.UpdateDrag(PointerFor(harness, "main", 1, 1));
            Assert.That(harness.Drag.PreviewLayer, Is.EqualTo(1), "layer 0 occupied by the base, supported above");
            harness.Drag.Drop();
            Assert.That(Item(harness, "dot-a").Location.Placement.Layer, Is.EqualTo(1));
            Assert.That(harness.Board.ItemViews["dot-a"].transform.localPosition.y, Is.EqualTo(PuzzleBoardLayout.LayerHeight).Within(1e-5f));
        }

        [UnityTest]
        public IEnumerator InvalidPreviews_MarkDomainCells_AndInvalidDropChangesNothing()
        {
            var harness = Harness();
            yield return null;
            var session = harness.Session;
            var hash = session.CurrentState.Hash;
            harness.Drag.BeginDrag("book", GrabPoint(harness, "book"));

            harness.Drag.UpdateDrag(PointerFor(harness, "main", 1, 1));
            Assert.That(harness.Drag.PreviewValid, Is.False, "overlaps the base on layer 0 and is half unsupported on layer 1");
            Assert.That(harness.Drag.Preview.Rejection, Is.EqualTo(MoveRejection.NoLegalLayer));
            Assert.That(harness.Drag.MarkedCells, Does.Contain(new Cell(1, 1)));

            harness.Drag.UpdateDrag(PointerFor(harness, "main", 3, 0));
            Assert.That(harness.Drag.PreviewValid, Is.False, "masked cell");
            Assert.That(harness.Drag.MarkedCells, Does.Contain(new Cell(4, 0)));

            harness.Drag.UpdateDrag(PointerFor(harness, "main", 3, 2));
            harness.Drag.UpdateDrag(PointerFor(harness, "main", 4, 3));
            Assert.That(harness.Drag.HasCandidate, Is.False, "footprint centre past the right edge: no compartment");

            harness.Drag.UpdateDrag(PointerFor(harness, "main", 1, 1));
            var step = harness.Drag.Drop();
            Assert.That(step.Move.Rejection, Is.EqualTo(MoveRejection.NoLegalLayer), "same rejection as the preview");
            Assert.That(step.StateChanged, Is.False);
            Assert.That(session.CurrentState.Hash, Is.EqualTo(hash));
            Assert.That(session.MoveCount, Is.Zero);
            Assert.That(session.UndoDepth, Is.Zero);
            Assert.That(harness.Tray.ItemViews["book"].transform.localPosition.y, Is.EqualTo(PuzzleTrayPresenter.SelectedLift),
                "visual returned to the tray (and stays selected)");
        }

        [UnityTest]
        public IEnumerator CancelAndDropOutsideTheBoard_LeaveStateUntouched()
        {
            var harness = Harness();
            yield return null;
            var session = harness.Session;
            var state = session.CurrentState;
            var rest = harness.Tray.ItemViews["rod"].transform.position;

            harness.Drag.BeginDrag("rod", GrabPoint(harness, "rod"));
            harness.Drag.UpdateDrag(PointerFor(harness, "main", 0, 3));
            harness.Drag.Cancel();
            Assert.That(session.CurrentState, Is.SameAs(state));
            var back = harness.Tray.ItemViews["rod"].transform.position;
            Assert.That((back.x, back.z), Is.EqualTo((rest.x, rest.z)), "returned to its tray slot");
            Assert.That(back.y - rest.y, Is.EqualTo(PuzzleTrayPresenter.SelectedLift).Within(1e-5f), "selected after the gesture");

            harness.Drag.BeginDrag("rod", GrabPoint(harness, "rod"));
            harness.Drag.UpdateDrag(new Vector3(40f, 0f, 40f));
            Assert.That(harness.Drag.HasCandidate, Is.False);
            Assert.That(harness.Drag.Drop(), Is.Null, "released away from every compartment = cancel");
            Assert.That(session.CurrentState, Is.SameAs(state));
            Assert.That(session.MoveCount, Is.Zero);
            Assert.That(session.UndoDepth, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Rotation_IsCandidateOnlyUntilCommit()
        {
            var harness = Harness();
            yield return null;
            var state = harness.Session.CurrentState;
            harness.Drag.BeginDrag("rod", GrabPoint(harness, "rod"));
            harness.Drag.UpdateDrag(PointerFor(harness, "main", 4, 1));
            Assert.That(harness.Drag.PreviewValid, Is.False, "horizontal rod does not fit at x = 4");

            harness.Drag.RotateCandidate();
            Assert.That(harness.Drag.CandidateRotation, Is.EqualTo(Rotation.Degrees90));
            Assert.That(harness.Drag.PreviewValid, Is.True);
            Assert.That(harness.Tray.ItemViews["rod"].Footprint.CellCount, Is.EqualTo(3));
            Assert.That(harness.Session.CurrentState, Is.SameAs(state), "rotating changes no state");
            Assert.That(harness.Session.MoveCount, Is.Zero);

            harness.Drag.Drop();
            Assert.That(Item(harness, "rod").Location.Placement.Rotation, Is.EqualTo(Rotation.Degrees90));
            Assert.That(harness.Session.MoveCount, Is.EqualTo(1), "rotation is part of the one move");
        }

        [UnityTest]
        public IEnumerator BoardItems_RelocateWhenAccessible_AndBlockedItemsCannotStart()
        {
            var harness = Harness();
            yield return null;
            harness.Drag.BeginDrag("book", GrabPoint(harness, "book"));
            harness.Drag.UpdateDrag(PointerFor(harness, "main", 2, 0));
            harness.Drag.Drop();

            Assert.That(harness.Drag.BeginDrag("book", GrabPoint(harness, "book", true)), Is.EqualTo(DragBeginResult.Started));
            harness.Drag.UpdateDrag(PointerFor(harness, "main", 2, 3));
            Assert.That(harness.Drag.PreviewValid, Is.True);
            harness.Drag.Drop();
            Assert.That(Item(harness, "book").Location.Placement.Anchor, Is.EqualTo(new Cell(2, 3)));
            Assert.That(harness.Session.MoveCount, Is.EqualTo(2));

            harness.Drag.BeginDrag("dot-a", GrabPoint(harness, "dot-a"));
            harness.Drag.UpdateDrag(PointerFor(harness, "main", 0, 0));
            harness.Drag.Drop();
            var state = harness.Session.CurrentState;
            Assert.That(harness.Drag.BeginDrag("base", GrabPoint(harness, "base", true)), Is.EqualTo(DragBeginResult.NotAccessible));
            Assert.That(harness.Drag.IsDragging, Is.False);
            Assert.That(harness.Session.CurrentState, Is.SameAs(state));
        }

        [UnityTest]
        public IEnumerator TwinInstances_AreIndependent_AndPocketIsHittable()
        {
            var harness = Harness();
            yield return null;
            Assert.That(harness.Drag.TryPickItem(GrabPoint(harness, "dot-b"), out var picked), Is.True);
            Assert.That(picked, Is.EqualTo("dot-b"));

            harness.Drag.BeginDrag("dot-b", GrabPoint(harness, "dot-b"));
            harness.Drag.UpdateDrag(PointerFor(harness, "pocket", 1, 1));
            Assert.That(harness.Drag.CandidateCompartment, Is.EqualTo("pocket"));
            Assert.That(harness.Drag.CandidateAnchor, Is.EqualTo(new Cell(1, 1)));
            harness.Drag.Drop();

            Assert.That(Item(harness, "dot-b").Location.Placement.Compartment, Is.EqualTo("pocket"));
            Assert.That(Item(harness, "dot-a").Location.Kind, Is.EqualTo(ItemLocationKind.SourceTray));
            Assert.That(harness.Tray.ItemViews.ContainsKey("dot-a"), Is.True);
            Assert.That(harness.Board.ItemViews.ContainsKey("dot-b"), Is.True);
            Assert.That(harness.Board.ItemViews.ContainsKey("dot-a"), Is.False);
        }

        [UnityTest]
        public IEnumerator XRay_DoesNotChangeInteractionLegality()
        {
            var harness = Harness();
            yield return null;
            harness.Drag.BeginDrag("dot-a", GrabPoint(harness, "dot-a"));
            harness.Drag.UpdateDrag(PointerFor(harness, "main", 0, 0));
            harness.Drag.Drop();

            harness.Drag.BeginDrag("book", GrabPoint(harness, "book"));
            harness.Drag.UpdateDrag(PointerFor(harness, "main", 0, 1));
            var withoutXRay = (harness.Drag.PreviewValid, harness.Drag.PreviewLayer, harness.Drag.Preview.Rejection);
            harness.Board.SetXRayEnabled(true);
            harness.Drag.UpdateDrag(PointerFor(harness, "main", 0, 1));
            Assert.That((harness.Drag.PreviewValid, harness.Drag.PreviewLayer, harness.Drag.Preview.Rejection), Is.EqualTo(withoutXRay));
            harness.Drag.Cancel();

            Assert.That(harness.Drag.BeginDrag("base", GrabPoint(harness, "base", true)), Is.EqualTo(DragBeginResult.NotAccessible),
                "the ghosted dot still blocks the base");
        }

        // Manual visual smoke check: renders the harness (with the real template and golden sweater) to PNG files.
        [UnityTest, Explicit("Writes screenshots for the ZT-039 visual smoke check")]
        public IEnumerator CaptureHarnessScreenshots()
        {
            yield return SceneManager.LoadSceneAsync("GameplaySandbox");
            yield return null;
            var template = Object.FindFirstObjectByType<BoardPresenter>().RuntimeMaterialTemplate;
            var catalog = Object.FindFirstObjectByType<GoldenItemPrefabCatalog>();
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.GetComponent<Light>() == null)
                    root.SetActive(false);

            _root = new GameObject("Harness");
            var harness = _root.AddComponent<PuzzleHarness>();
            harness.Configure(template, catalog);
            harness.Build();
            yield return null;
            var folder = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Builds/zt039-screens"));
            Directory.CreateDirectory(folder);

            Capture(harness, Path.Combine(folder, "1-initial.png"));
            harness.Drag.BeginDrag("dot-a", GrabPoint(harness, "dot-a"));
            harness.Drag.UpdateDrag(PointerFor(harness, "main", 1, 1));
            yield return null;
            Capture(harness, Path.Combine(folder, "2-valid-layer1-preview.png"));
            harness.Drag.Drop();
            harness.Drag.BeginDrag("book", GrabPoint(harness, "book"));
            harness.Drag.UpdateDrag(PointerFor(harness, "main", 3, 0));
            yield return null;
            Capture(harness, Path.Combine(folder, "3-invalid-masked-preview.png"));
            harness.Drag.Cancel();
            harness.Drag.BeginDrag("sweater", GrabPoint(harness, "sweater"));
            harness.Drag.UpdateDrag(PointerFor(harness, "main", 2, 1));
            harness.Drag.Drop();
            harness.Drag.BeginDrag("rod", GrabPoint(harness, "rod"));
            harness.Drag.RotateCandidate();
            harness.Drag.UpdateDrag(PointerFor(harness, "pocket", 0, 0));
            yield return null;
            Capture(harness, Path.Combine(folder, "4-golden-sweater-rotated-rod-invalid.png"));
            harness.Drag.Cancel();
            harness.Board.SetXRayEnabled(true);
            yield return null;
            Capture(harness, Path.Combine(folder, "5-xray.png"));
            Debug.Log("[zt039-screens] " + folder);
        }

        private static void Capture(PuzzleHarness harness, string path)
        {
            const int width = 1080, height = 1920;
            var camera = harness.Camera;
            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            camera.aspect = (float)width / height;
            harness.FrameCamera();
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.Destroy(target);
            Object.Destroy(image);
        }
    }
}
