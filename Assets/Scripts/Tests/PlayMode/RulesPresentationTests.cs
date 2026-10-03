#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZipTrip.Domain;
using ZipTrip.Domain.Puzzle;
using ZipTrip.Unity;
using static ZipTrip.Tests.PlayMode.StagingInteractionTests;

namespace ZipTrip.Tests.PlayMode
{
    // ZT-042 live rules UI on the shipped PuzzleGameplay scene with a test-only fixture. Statuses are compared against
    // RuleEvaluator itself; the presenter must never disagree with the Domain.
    public sealed class RulesPresentationTests
    {
        private static IEnumerator Fixture(string json = null)
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/PuzzleGameplay.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            if (json != "lv1")
            {
                Object.FindFirstObjectByType<PuzzleGameplayScene>().LoadLevel(RulesFixture.Load(json), "Rules");
                yield return null;
            }
        }

        private static PuzzleGameplayScene Scene() => Object.FindFirstObjectByType<PuzzleGameplayScene>();

        private static void AssertMatchesDomain(PuzzleGameplayScene scene)
        {
            foreach (var result in RuleEvaluator.Evaluate(scene.Session.CurrentState, scene.Level.Rules))
            {
                var expected = result.SubjectIds.Count == 0 ? RuleTagStatus.Inactive
                    : result.IsSatisfied ? RuleTagStatus.Satisfied : RuleTagStatus.Violated;
                Assert.That(scene.Rules.StatusOf(result.RuleId), Is.EqualTo(expected), result.RuleId + " follows RuleEvaluator");
            }
        }

        // Drags an item so that its footprint anchors at `anchor` (first footprint cell under the pointer).
        private static void DragTo(PuzzleGameplayScene scene, string id, Cell anchor)
        {
            var view = scene.Board.ItemViews.TryGetValue(id, out var onBoard) ? onBoard : scene.Tray.ItemViews[id];
            Assert.That(scene.Drag.BeginDrag(id, Grab(view)), Is.EqualTo(DragBeginResult.Started), id);
            var first = scene.Drag.IsDragging ? view.Footprint.OccupiedCells[0] : default;
            scene.Drag.UpdateDrag(scene.Board.Compartments["main"].transform.position
                + new Vector3(anchor.X + first.X + 0.5f, 0f, -(anchor.Y + first.Y + 0.5f)));
        }

        private static void Place(PuzzleGameplayScene scene, string id, Cell anchor)
        {
            DragTo(scene, id, anchor);
            Assert.That(scene.Drag.Drop().Move.IsAccepted, Is.True, id + " placed");
        }

        [UnityTest]
        public IEnumerator Lv1AndLv2_AuthorNoRules_ShowNoRuleUi()
        {
            yield return Fixture("lv1");
            var scene = Scene();
            for (var level = 0; level < 2; level++)
            {
                Assert.That(scene.Level.Rules.Rules, Is.Empty);
                Assert.That(scene.Rules.StripVisible, Is.False, scene.LevelId);
                Assert.That(scene.Rules.RuleIds, Is.Empty);
                Assert.That(scene.Rules.VisibleCues, Is.Empty);
                scene.NextLevel();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Fixture_ShowsExactlyTheAuthoredRules_WithDomainStatus_AndKeepsTheInteriorClear()
        {
            yield return Fixture();
            var scene = Scene();
            Assert.That(scene.Rules.RuleIds, Is.EqualTo(new[] { "r1-zone", "r2-adjacent", "r3-apart", "r4-access" }));
            Assert.That(scene.Rules.LabelOf("r1-zone"), Is.EqualTo("Book → Left"));
            Assert.That(scene.Rules.LabelOf("r3-apart"), Is.EqualTo("Clothes ≠ Tech"));
            Assert.That(scene.Rules.StatusOf("r2-adjacent"), Is.EqualTo(RuleTagStatus.Violated));
            AssertMatchesDomain(scene);
            Assert.That(scene.Rules.Strip.GetComponentsInChildren<Component>(true), Has.None.Null, "no missing scripts");
            Assert.That(scene.Rules.VisibleCues, Is.Empty, "idle: nothing violated that needs an in-suitcase cue");

            // Layout at the Huawei portrait resolution: the strip (canvas scales with width, 1080 reference) ends above the
            // suitcase interior's back row.
            var camera = scene.Camera;
            var target = new RenderTexture(1080, 2340, 24);
            camera.targetTexture = target;
            try
            {
                scene.FrameCamera();
                var stripBottomFromTop = (PuzzleRulesPresenter.StripTop + scene.Rules.StripHeight + 6f) * camera.pixelWidth / PuzzleHud.ReferenceWidth;
                var backRowFromTop = camera.pixelHeight - camera.WorldToScreenPoint(scene.Board.CompartmentFrames().Single().Origin).y;
                Assert.That(backRowFromTop, Is.GreaterThan(stripBottomFromTop), "rule strip does not cover the packing interior");
            }
            finally
            {
                camera.targetTexture = null;
                Object.Destroy(target);
            }
        }

        [UnityTest]
        public IEnumerator Zone_DragShowsTheZoneOnlyForItsSubject_ViolationAndUndoUpdateLive()
        {
            yield return Fixture();
            var scene = Scene();
            DragTo(scene, "laptop-1", new Cell(2, 3));
            yield return null;
            Assert.That(scene.Rules.VisibleCues, Is.Empty, "unrelated drag: no zone, no targets");
            scene.Drag.Cancel();

            DragTo(scene, "book-1", new Cell(0, 4));
            yield return null;
            Assert.That(scene.Rules.VisibleCues, Does.Contain("zone:r1-zone:main"), "zone subject drag shows its zone");
            scene.Drag.UpdateDrag(scene.Board.Compartments["main"].transform.position + new Vector3(3.5f, 0f, -4.5f));
            Assert.That(scene.Drag.Drop().Move.IsAccepted, Is.True);
            yield return null;
            Assert.That(scene.Rules.VisibleCues, Is.Empty, "zone overlay ends with the drag");
            Assert.That(scene.Rules.StatusOf("r1-zone"), Is.EqualTo(RuleTagStatus.Violated));
            Assert.That(scene.Rules.IsPulsing("r1-zone"), Is.True, "status change pulses once");
            AssertMatchesDomain(scene);

            Assert.That(scene.Undo(), Is.True);
            Assert.That(scene.Rules.StatusOf("r1-zone"), Is.EqualTo(RuleTagStatus.Satisfied), "undo restores rule status");
            AssertMatchesDomain(scene);
        }

        [UnityTest]
        public IEnumerator AdjacencyRequired_TargetsGlowForTheSubject_SatisfiedOnCommit()
        {
            yield return Fixture();
            var scene = Scene();
            DragTo(scene, "sneaker-1", new Cell(3, 4));
            yield return null;
            Assert.That(scene.Rules.VisibleCues, Does.Contain("target:book-1"));
            Assert.That(scene.Rules.VisibleCues.Count(c => c.StartsWith("target:")), Is.EqualTo(1), "the subject never targets itself");
            scene.Drag.UpdateDrag(scene.Board.Compartments["main"].transform.position + new Vector3(0.5f, 0f, -3.5f));
            Assert.That(scene.Drag.Drop().Move.IsAccepted, Is.True);
            yield return null;
            Assert.That(scene.Rules.StatusOf("r2-adjacent"), Is.EqualTo(RuleTagStatus.Satisfied));
            AssertMatchesDomain(scene);
        }

        [UnityTest]
        public IEnumerator AdjacencyForbidden_ViolationShowsRestrainedPairCue_UndoClearsIt()
        {
            yield return Fixture();
            var scene = Scene();
            Place(scene, "sweater-1", new Cell(2, 4));
            yield return null;
            Assert.That(scene.Rules.StatusOf("r3-apart"), Is.EqualTo(RuleTagStatus.Violated));
            Assert.That(scene.Rules.VisibleCues, Is.EquivalentTo(new[] { "warn:sweater-1", "warn:laptop-1" }));
            AssertMatchesDomain(scene);
            scene.Undo();
            yield return null;
            Assert.That(scene.Rules.StatusOf("r3-apart"), Is.EqualTo(RuleTagStatus.Satisfied));
            Assert.That(scene.Rules.VisibleCues, Is.Empty, "no stale cue after undo");
        }

        [UnityTest]
        public IEnumerator Access_BlockedSubjectGetsASubtleCue_UndoRestores()
        {
            yield return Fixture();
            var scene = Scene();
            Place(scene, "sneaker-1", new Cell(2, 0));
            yield return null;
            Assert.That(scene.Board.ItemViews["sneaker-1"].Placement.Layer, Is.EqualTo(1), "stacked on the laptop");
            Assert.That(scene.Rules.StatusOf("r4-access"), Is.EqualTo(RuleTagStatus.Violated));
            Assert.That(scene.Rules.VisibleCues, Does.Contain("access:laptop-1"));
            AssertMatchesDomain(scene);
            scene.Undo();
            yield return null;
            Assert.That(scene.Rules.StatusOf("r4-access"), Is.EqualTo(RuleTagStatus.Satisfied));
            Assert.That(scene.Rules.VisibleCues, Is.Empty);
        }

        [UnityTest]
        public IEnumerator StagingAndActiveDomain_UpdateRuleStatus()
        {
            yield return Fixture();
            var scene = Scene();
            Assert.That(Stage(scene, "book-1", 0).Step.Move.IsAccepted, Is.True);
            Assert.That(scene.Rules.StatusOf("r1-zone"), Is.EqualTo(RuleTagStatus.Violated), "a staged book is in no zone");
            AssertMatchesDomain(scene);
            scene.Undo();
            Assert.That(scene.Rules.StatusOf("r1-zone"), Is.EqualTo(RuleTagStatus.Satisfied));

            scene.LoadLevel(RulesFixture.Load(RulesFixture.LaptopExtracted), "Rules");
            yield return null;
            Assert.That(scene.Rules.StatusOf("r4-access"), Is.EqualTo(RuleTagStatus.Inactive), "laptop left the active domain");
            AssertMatchesDomain(scene);
        }

        [UnityTest, Explicit("Writes ZT-042 screenshots")]
        public IEnumerator CaptureRulesScreenshots()
        {
            var quality = QualitySettings.GetQualityLevel();
            QualitySettings.SetQualityLevel(System.Array.IndexOf(QualitySettings.names, "Mobile"), true);
            try
            {
                var folder = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Builds/zt042-screens"));
                Directory.CreateDirectory(folder);
                yield return Fixture("lv1");
                var scene = Scene();
                scene.Hud.RenderThrough(scene.Camera);
                yield return Shot(scene, folder, "00-lv1-no-rules");
                scene.LoadLevel(RulesFixture.Load(), "Rules");
                yield return null;
                var main = scene.Board.Compartments["main"].transform.position;
                yield return Shot(scene, folder, "A-zone-fixture-idle");
                DragTo(scene, "book-1", new Cell(2, 4));
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Shot(scene, folder, "B-zone-subject-dragging");
                scene.Drag.UpdateDrag(main + new Vector3(3.5f, 0f, -4.5f));
                scene.Drag.Drop();
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Shot(scene, folder, "C-zone-violated");
                scene.Undo();
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Shot(scene, folder, "D-zone-satisfied");
                DragTo(scene, "sneaker-1", new Cell(3, 4));
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Shot(scene, folder, "E-adjacency-required-violated-dragging");
                scene.Drag.UpdateDrag(main + new Vector3(0.5f, 0f, -3.5f));
                scene.Drag.Drop();
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Shot(scene, folder, "F-adjacency-required-satisfied");
                scene.Undo();
                Place(scene, "sweater-1", new Cell(2, 4));
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Shot(scene, folder, "G-adjacency-forbidden-violation");
                scene.Undo();
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Shot(scene, folder, "H-access-satisfied");
                Place(scene, "sneaker-1", new Cell(2, 0));
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Shot(scene, folder, "I-access-violated");
                scene.Undo();
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Shot(scene, folder, "J-undo-restores-access");
                Debug.Log("[zt042-screens] " + folder);
            }
            finally
            {
                QualitySettings.SetQualityLevel(quality, true);
            }
        }

        private static IEnumerator Shot(PuzzleGameplayScene scene, string folder, string name)
        {
            var camera = scene.Camera;
            var target = new RenderTexture(1080, 2340, 24);
            camera.targetTexture = target;
            if (!scene.Drag.IsDragging)
                scene.FrameCamera();
            yield return null;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(1080, 2340, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1080, 2340), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.Destroy(target);
            Object.Destroy(image);
        }
    }
}
#endif
