using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using ZipTrip.Domain;
using ZipTrip.Unity;

namespace ZipTrip.Tests.PlayMode
{
    // ADR-0010 production camera (orthographic, 65° pitch / 25° tilt) on the shipped Lv1, at both supported phone
    // aspects: picking, ghost and commit stay exact, near and far rows keep one cell scale, tray and HUD stay clear,
    // and ZIP-02 still reads.
    public sealed class GameplayCameraContractTests
    {
        private const string ScenePath = "Assets/Scenes/PuzzleGameplay.unity";
        private static readonly (int Width, int Height)[] Screens = { (1080, 2340), (1080, 1920) };

        private static IEnumerator Open(int width, int height, System.Action<PuzzleGameplayScene, RenderTexture> ready)
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
            Assert.That(scene.Level.Id, Is.EqualTo("lv1-fit"));
            scene.Completion.AutoAdvance = false;
            var target = new RenderTexture(width, height, 24);
            scene.Camera.targetTexture = target;
            scene.FrameCamera();
            yield return null;
            ready(scene, target);
        }

        private static void Close(PuzzleGameplayScene scene, RenderTexture target)
        {
            scene.Camera.targetTexture = null;
            Object.Destroy(target);
        }

        [Test]
        public void Contract_IsOrthographicAt65DegreePitch()
        {
            Assert.That(PuzzleCameraFraming.Pitch, Is.EqualTo(65f));
            var probe = new GameObject("ADR-0010 probe").AddComponent<Camera>();
            try
            {
                PuzzleCameraFraming.Frame(probe, new Bounds(new Vector3(3f, 0f, -4f), new Vector3(7f, 1f, 9f)), 0.1f, 0.2f);
                Assert.That(probe.orthographic, Is.True, "no perspective");
                Assert.That(probe.transform.eulerAngles, Is.EqualTo(new Vector3(65f, 0f, 0f)).Using(Vector3EqualityComparer.Instance));
                Assert.That(probe.transform.eulerAngles.x, Is.Not.EqualTo(75f).Within(0.5f), "negative: the ADR-0002 pitch is retired");
            }
            finally { Object.DestroyImmediate(probe.gameObject); }
        }

        [UnityTest]
        public IEnumerator ShippedScene_FramesWithTheProductionCamera_AtBothAspects()
        {
            foreach (var (width, height) in Screens)
            {
                PuzzleGameplayScene scene = null;
                RenderTexture target = null;
                yield return Open(width, height, (s, t) => { scene = s; target = t; });
                Assert.That(scene.Camera.orthographic, Is.True, width + "x" + height);
                Assert.That(scene.Camera.transform.eulerAngles.x, Is.EqualTo(PuzzleCameraFraming.Pitch).Within(0.01f));
                Assert.That(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c => c.enabled), Is.EqualTo(1),
                    "one fixed gameplay camera, no alternate review camera");
                Close(scene, target);
            }
        }

        // Every anchor of the grid (far row = row 0) through the real camera: resolved anchor and footprint glow match
        // the cell under the finger, then a drop commits where the glow was.
        [UnityTest]
        public IEnumerator Picking_Ghost_AndCommit_AlignAcrossTheWholeGrid()
        {
            foreach (var (width, height) in Screens)
            {
                PuzzleGameplayScene scene = null;
                RenderTexture target = null;
                yield return Open(width, height, (s, t) => { scene = s; target = t; });
                var camera = scene.Camera;
                var frame = scene.Board.CompartmentFrames().Single(f => f.Id == "main");
                var view = scene.Tray.ItemViews["sunglasses-1"];
                var grab = view.transform.position + view.transform.lossyScale.x * new Vector3(0.5f, 0f, -0.5f);
                scene.HandlePointer(new PointerSignal(PointerPhase.Down, camera.WorldToScreenPoint(grab)));
                Assert.That(scene.Drag.DraggedInstanceId, Is.EqualTo("sunglasses-1"), "tray pick through the camera");
                var misses = new List<string>();
                var worstGlow = 0f;
                var anchors = 0;
                for (var y = 0; y < frame.Height; y++)
                    for (var x = 0; x + 1 < frame.Width; x++)
                    {
                        var cell = frame.Origin + new Vector3(x + 0.5f, 0f, -(y + 0.5f));
                        scene.HandlePointer(new PointerSignal(PointerPhase.Move, camera.WorldToScreenPoint(cell)));
                        if (scene.Drag.CandidateNestParent != null)
                            continue; // over a nest parent the drag targets nesting, not a board anchor
                        anchors++;
                        if (scene.Drag.CandidateCompartment != "main" || scene.Drag.CandidateAnchor != new Cell(x, y))
                        {
                            misses.Add("(" + x + "," + y + ")->" + scene.Drag.CandidateAnchor);
                            continue;
                        }
                        var glow = scene.Drag.FootprintPreview.bounds.center;
                        var expected = frame.Origin + new Vector3(x + 1f, 0f, -(y + 0.5f));
                        worstGlow = Mathf.Max(worstGlow, new Vector2(glow.x - expected.x, glow.z - expected.z).magnitude);
                    }
                var drop = camera.WorldToScreenPoint(frame.Origin + new Vector3(3.5f, 0f, -6.5f));
                scene.HandlePointer(new PointerSignal(PointerPhase.Move, drop));
                var glowAtDrop = scene.Drag.FootprintPreview.bounds.center;
                Assert.That(scene.Drag.PreviewValid, Is.True, "(3,6) is legal");
                scene.HandlePointer(new PointerSignal(PointerPhase.Up, drop));
                var placed = scene.Board.ItemViews["sunglasses-1"];
                Debug.Log("[ADR-0010] " + width + "x" + height + " anchors=" + anchors + " misses=" + misses.Count
                    + " worstGlowOffset=" + worstGlow.ToString("F4"));
                Assert.That(anchors, Is.GreaterThanOrEqualTo(frame.Height * (frame.Width - 1) - 4), "whole grid sampled");
                Assert.That(misses, Is.Empty, string.Join(", ", misses));
                Assert.That(worstGlow, Is.LessThan(0.02f), "glow under the finger");
                Assert.That(placed.Placement.Anchor, Is.EqualTo(new Cell(3, 6)), "commit where dropped");
                Assert.That(placed.transform.position.x + 1f, Is.EqualTo(glowAtDrop.x).Within(0.02f), "commit = glow");
                Assert.That(placed.transform.position.z - 0.5f, Is.EqualTo(glowAtDrop.z).Within(0.02f));
                Close(scene, target);
            }
        }

        // Same composition criteria as the shipped GoldenPortrait guard, plus equal near/far cell scale.
        [UnityTest]
        public IEnumerator Framing_FarRowReadable_TrayAndHudClear_AtBothAspects()
        {
            foreach (var (width, height) in Screens)
            {
                PuzzleGameplayScene scene = null;
                RenderTexture target = null;
                yield return Open(width, height, (s, t) => { scene = s; target = t; });
                var camera = scene.Camera;
                var board = scene.Board;
                var frame = board.CompartmentFrames().Single(f => f.Id == "main");
                float CellHeight(int row) => Mathf.Abs(camera.WorldToScreenPoint(frame.Origin + new Vector3(2.5f, 0f, -row)).y
                    - camera.WorldToScreenPoint(frame.Origin + new Vector3(2.5f, 0f, -row - 1f)).y);
                var cellWidth = Mathf.Abs(camera.WorldToScreenPoint(frame.Origin + Vector3.right).x
                    - camera.WorldToScreenPoint(frame.Origin).x);
                var far = CellHeight(0);
                var near = CellHeight(frame.Height - 1);
                var body = board.ContainerFootprint;
                var left = camera.WorldToViewportPoint(new Vector3(body.xMin, board.ContainerBottomY, body.center.y)).x;
                var right = camera.WorldToViewportPoint(new Vector3(body.xMax, board.ContainerBottomY, body.center.y)).x;
                var topHud = PuzzleHud.TopFraction(width, height);
                var bottomHud = PuzzleHud.BottomFraction(width, height);
                Debug.Log("[ADR-0010] " + width + "x" + height + " cell=" + cellWidth.ToString("F0") + "x" + far.ToString("F0")
                    + "px far/near=" + (far / near).ToString("F3") + " bodyWidth=" + (right - left).ToString("P0"));
                Assert.That(far / near, Is.EqualTo(1f).Within(0.01f), "orthographic: far row keeps the near-row scale");
                Assert.That(far, Is.GreaterThanOrEqualTo(100f), "far-row cell readable");
                Assert.That(right - left, Is.GreaterThanOrEqualTo(0.7f), "suitcase width on screen");
                Assert.That(left >= 0f && right <= 1f, Is.True, "no horizontal clipping");
                Assert.That(camera.WorldToViewportPoint(frame.Origin).y, Is.LessThanOrEqualTo(1f - topHud), "back row below the top HUD");
                Assert.That(camera.WorldToViewportPoint(board.Lid.position).y, Is.LessThan(1f - topHud), "hinge visible");
                foreach (var view in scene.Tray.ItemViews.Values)
                {
                    var depth = view.Footprint.OccupiedCells.Max(c => c.Y) + 1;
                    var lowest = camera.WorldToViewportPoint(view.transform.position + new Vector3(0f, 0f, -depth * scene.Tray.Scale));
                    Assert.That(lowest.y, Is.GreaterThanOrEqualTo(bottomHud - 0.001f), view.InstanceId + " above the controls");
                    var x = camera.WorldToViewportPoint(view.transform.position).x;
                    Assert.That(x, Is.InRange(0f, 1f), view.InstanceId + " inside the width");
                }
                Close(scene, target);
            }
        }

        [UnityTest]
        public IEnumerator Zip02_LidMidCloseAndFinal_StayOnScreen_AndStateIsUntouched()
        {
            PuzzleGameplayScene scene = null;
            RenderTexture target = null;
            yield return Open(1080, 2340, (s, t) => { scene = s; target = t; });
            var rig = scene.Board.Container;
            Place(scene, "travel-pouch-1", 1, 3);
            Place(scene, "sunglasses-1", 3, 6);
            Place(scene, "shampoo-1", 3, 2);
            Assert.That(scene.Completion.PlayCount, Is.EqualTo(1));
            var hash = scene.Session.CurrentState.Hash;
            var pose = (scene.Camera.transform.position, scene.Camera.transform.rotation);
            scene.Completion.Advance(PuzzleCompletionPresenter.SettleDuration + PuzzleCompletionPresenter.AnticipationDuration
                + PuzzleCompletionPresenter.RuleCascadeDuration + PuzzleCompletionPresenter.StrapsDuration
                + PuzzleCompletionPresenter.LidDuration * 0.5f);
            Assert.That(scene.Completion.CurrentPhase, Is.EqualTo(PuzzleCompletionPresenter.Phase.Lid));
            var mid = scene.Camera.WorldToViewportPoint(Bounds(rig.Lid.GetComponentsInChildren<Renderer>()).center);
            Assert.That(mid.x, Is.InRange(0f, 1f), "lid on screen at mid-close");
            Assert.That(mid.y, Is.InRange(0f, 1f));
            scene.FrameCamera();
            Assert.That(scene.Camera.transform.position, Is.EqualTo(pose.position), "camera fixed during ZIP-02");
            scene.Completion.Advance(PuzzleCompletionPresenter.FullDuration);
            Assert.That(rig.Lid.localRotation, Is.EqualTo(Quaternion.identity));
            var closed = Bounds(rig.Lid.GetComponentsInChildren<Renderer>());
            for (var i = 0; i < 8; i++)
            {
                var p = scene.Camera.WorldToViewportPoint(new Vector3((i & 1) == 0 ? closed.min.x : closed.max.x,
                    (i & 2) == 0 ? closed.min.y : closed.max.y, (i & 4) == 0 ? closed.min.z : closed.max.z));
                Assert.That(p.x >= -0.001f && p.x <= 1.001f && p.y >= -0.001f && p.y <= 1.001f, Is.True, "closed lid on screen");
            }
            Assert.That(scene.Hud.CompletionVisible, Is.True);
            Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(hash));
            Assert.That(scene.Camera.transform.rotation, Is.EqualTo(pose.rotation));
            Close(scene, target);
        }

        private static void Place(PuzzleGameplayScene scene, string id, int x, int y)
        {
            var view = scene.Tray.ItemViews[id];
            var first = view.Footprint.OccupiedCells[0];
            var grab = view.transform.position + view.transform.lossyScale.x * new Vector3(first.X + 0.5f, 0f, -(first.Y + 0.5f));
            Assert.That(scene.Drag.BeginDrag(id, grab), Is.EqualTo(DragBeginResult.Started), id);
            var frame = scene.Board.CompartmentFrames().Single(f => f.Id == "main");
            scene.Drag.UpdateDrag(frame.Origin + new Vector3(x + first.X + .5f, 0f, -(y + first.Y + .5f)));
            Assert.That(scene.Drag.Drop().Move.IsAccepted, Is.True, id);
        }

        private static Bounds Bounds(IEnumerable<Renderer> renderers)
        {
            var list = renderers.ToList();
            var bounds = list[0].bounds;
            foreach (var renderer in list.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
