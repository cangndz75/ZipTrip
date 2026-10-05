using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;
using UnityEngine.UI;
using ZipTrip.Domain;
using ZipTrip.Unity;

namespace ZipTrip.Tests.PlayMode
{
    // ART-CC02 composition convergence on the shipped Lv1, measured in the 9:16 reference frame fitted (never stretched)
    // into the screen: suitcase body width / bottom, floating mission card, dock, the lid kept out of packing and
    // brought back for ZIP-02, semantic region upholstery, gold/blue highlight semantics and the dev target overlay.
    public sealed class CompositionConvergenceTests
    {
        private const string ScenePath = "Assets/Scenes/PuzzleGameplay.unity";
        private static readonly (int Width, int Height)[] Screens = { (1080, 2340), (1080, 1920) };
        private RenderTexture _target;

        private IEnumerator Open(int width = 1080, int height = 2340)
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var scene = Scene;
            Assert.That(scene.Level.Id, Is.EqualTo("lv1-fit"));
            scene.Completion.AutoAdvance = false;
            _target = new RenderTexture(width, height, 24);
            scene.Camera.targetTexture = _target;
            scene.Hud.RenderThrough(scene.Camera);
            scene.FrameCamera();
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            var scene = Scene;
            if (scene != null)
            {
                scene.Hud.RenderThrough(null);
                scene.Camera.targetTexture = null;
            }
            if (TargetCompositionOverlay.Instance != null)
            {
                TargetCompositionOverlay.Instance.Visible = false;
                TargetCompositionOverlay.Instance.RenderThrough(null);
            }
            if (_target != null)
                Object.Destroy(_target);
        }

        private static PuzzleGameplayScene Scene => Object.FindFirstObjectByType<PuzzleGameplayScene>();

        // Reference-frame fraction from the frame's top (y) / left (x) for a screen point.
        private static Vector2 Reference(Camera camera, Vector3 screen)
        {
            var frame = PuzzleCameraFraming.ReferenceFrame(camera.pixelWidth, camera.pixelHeight);
            return new Vector2((screen.x - frame.xMin) / frame.width, (frame.yMax - screen.y) / frame.height);
        }

        private static Rect ScreenBounds(Camera camera, Bounds bounds)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (var i = 0; i < 8; i++)
            {
                var p = camera.WorldToScreenPoint(new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y, (i & 4) == 0 ? bounds.min.z : bounds.max.z));
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
            }
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private static Bounds Encapsulate(IEnumerable<Renderer> renderers)
        {
            var list = renderers.ToList();
            var bounds = list[0].bounds;
            foreach (var renderer in list.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        [UnityTest]
        public IEnumerator Suitcase_FillsTheReferenceWidth_BottomMeetsTheDock_CardFloatsAt21Percent()
        {
            foreach (var (width, height) in Screens)
            {
                yield return Open(width, height);
                var scene = Scene;
                var camera = scene.Camera;
                var body = ScreenBounds(camera, Encapsulate(scene.Board.Container.Base.GetComponentsInChildren<Renderer>()));
                var frame = PuzzleCameraFraming.ReferenceFrame(camera.pixelWidth, camera.pixelHeight);
                var bodyWidth = body.width / frame.width;
                var bottom = Reference(camera, new Vector3(0f, body.yMin)).y;
                var top = Reference(camera, new Vector3(0f, body.yMax)).y;
                var backRow = Reference(camera, camera.WorldToScreenPoint(scene.Board.CompartmentFrames().Single().Origin)).y;

                var corners = new Vector3[4];
                scene.Rules.Note.GetWorldCorners(corners);
                var card = corners.Select(c => Reference(camera, UiProjection.Screen(scene.Rules.Note, c))).ToArray();
                var cardCenter = (card.Max(c => c.y) + card.Min(c => c.y)) * 0.5f;
                var cardWidth = scene.Rules.Note.rect.width / PuzzleHud.ReferenceWidth;
                var cardHeight = scene.Rules.Note.rect.height / PuzzleHud.ReferenceHeight;
                var tilt = Mathf.DeltaAngle(0f, scene.Rules.Note.localEulerAngles.z);
                var shell = scene.Table.TrayShellRect;
                var dock = Reference(camera, camera.WorldToScreenPoint(new Vector3(shell.center.x, scene.Tray.transform.position.y, shell.yMax))).y;
                var origin = scene.Board.CompartmentFrames().Single().Origin;
                var cell = camera.WorldToScreenPoint(origin + Vector3.right) - camera.WorldToScreenPoint(origin);
                var cellDepth = camera.WorldToScreenPoint(origin) - camera.WorldToScreenPoint(origin + Vector3.back);
                Debug.Log($"[ART-CC02] {width}x{height} bodyWidth={bodyWidth:P1} bodyTop={top:P1} backRow={backRow:P1} "
                    + $"bodyBottom={bottom:P1} card={cardWidth:P1}x{cardHeight:P1} cardCenter={cardCenter:P1} cardTilt={tilt:F1} "
                    + $"dockTop={dock:P1} cell={cell.x:F0}x{cellDepth.y:F0}px compact={scene.Rules.Compact}");

                Assert.That(bodyWidth, Is.InRange(0.94f, 0.98f), "suitcase width (9:16 reference)");
                Assert.That(body.xMin >= 0f && body.xMax <= camera.pixelWidth, Is.True, "no horizontal clipping");
                Assert.That(cardWidth, Is.InRange(0.56f, 0.60f), "card width");
                Assert.That(tilt, Is.InRange(-4f, -2f), "card tilt");
                Assert.That(cellDepth.y, Is.GreaterThanOrEqualTo(100f), "far-row cell stays readable");
                Assert.That(Mathf.Abs(dock - bottom), Is.LessThan(0.02f), "dock upper boundary meets the suitcase bottom");
                var utilities = new Vector3[4];
                scene.Hud.Dock.GetWorldCorners(utilities);
                Assert.That(utilities.Min(c => UiProjection.Screen(scene.Hud.Dock, c).y), Is.GreaterThanOrEqualTo(0f), "controls on screen");
                if (height == 2340)
                {
                    // P30 Pro acceptance frame.
                    Assert.That(bottom, Is.InRange(0.77f, 0.79f), "suitcase bottom (9:16 reference)");
                    Assert.That(scene.Rules.Compact, Is.False, "full mission card");
                    Assert.That(cardCenter, Is.InRange(0.20f, 0.22f), "card centre");
                    Assert.That(card.Max(c => c.y), Is.LessThan(backRow), "card floats over the back rim, not the packing interior");
                }
                else
                {
                    // 16:9 has no height beyond the 9:16 frame: the dock reserve lifts the suitcase, and the mission
                    // collapses to its chip rather than covering the interior.
                    Assert.That(bottom, Is.InRange(0.69f, 0.79f), "suitcase bottom lifted for the dock");
                    if (!scene.Rules.Compact)
                        Assert.That(card.Max(c => c.y), Is.LessThan(backRow), "card clear of the packing interior");
                }
                TearDown();
            }
        }

        [UnityTest]
        public IEnumerator Lid_StaysInTheHierarchyButOutOfPacking_AndReturnsForZip02()
        {
            yield return Open();
            var scene = Scene;
            var rig = scene.Board.Container;
            var lid = rig.Lid.GetComponentsInChildren<Renderer>(true);
            Assert.That(rig.Lid.IsChildOf(rig.Root), Is.True, "lid is part of the suitcase hierarchy");
            Assert.That(lid, Is.Not.Empty);
            Assert.That(lid.Any(r => r.enabled), Is.False, "packing: lid outside the composition");
            Assert.That(scene.Completion.LidVisible, Is.False);
            Assert.That(Quaternion.Angle(rig.Lid.localRotation, rig.LidOpenLocalRotation), Is.LessThan(0.01f), "lid keeps its open pose");

            Place(scene, "travel-pouch-1", 1, 3);
            Place(scene, "sunglasses-1", 3, 6);
            Assert.That(lid.Any(r => r.enabled), Is.False, "non-completing steps keep the lid hidden");
            Place(scene, "shampoo-1", 3, 2);
            var hash = scene.Session.CurrentState.Hash;
            Assert.That(scene.Completion.PlayCount, Is.EqualTo(1));
            Assert.That(lid.All(r => r.enabled), Is.True, "Zip It: lid visible");
            scene.Completion.Advance(PuzzleCompletionPresenter.SettleDuration + PuzzleCompletionPresenter.AnticipationDuration
                + PuzzleCompletionPresenter.RuleCascadeDuration + PuzzleCompletionPresenter.StrapsDuration
                + PuzzleCompletionPresenter.LidDuration * 0.5f);
            Assert.That(scene.Completion.CurrentPhase, Is.EqualTo(PuzzleCompletionPresenter.Phase.Lid));
            var mid = scene.Camera.WorldToViewportPoint(Encapsulate(lid).center);
            Assert.That(mid.x >= 0f && mid.x <= 1f && mid.y >= 0f && mid.y <= 1f, Is.True, "lid on screen at mid-close");
            scene.Completion.Advance(PuzzleCompletionPresenter.FullDuration);
            Assert.That(scene.Completion.CurrentPhase, Is.EqualTo(PuzzleCompletionPresenter.Phase.Confirmed));
            Assert.That(Quaternion.Angle(rig.Lid.localRotation, Quaternion.identity), Is.LessThan(0.01f), "closed");
            var closed = ScreenBounds(scene.Camera, Encapsulate(lid));
            Assert.That(closed.xMin >= -1f && closed.xMax <= scene.Camera.pixelWidth + 1f
                && closed.yMin >= -1f && closed.yMax <= scene.Camera.pixelHeight + 1f, Is.True, "closed lid on screen");
            Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(hash), "the ritual never writes gameplay state");

            scene.Restart();
            yield return null;
            Assert.That(lid.Any(r => r.enabled), Is.False, "restart returns to packing without the lid");
        }

        // Lv1 zones: "upper" = rows 0-1, "right" = columns 3-4 of rows 2-6, the rest = columns 0-2 of rows 2-6. Seams sit
        // only on those two boundaries (a T), never on the other cell lines.
        [UnityTest]
        public IEnumerator Regions_FollowTheRuleZones_NotTheHiddenGrid()
        {
            yield return Open();
            var scene = Scene;
            var origin = scene.Board.Compartments["main"].transform.position;
            var decor = scene.Board.GetComponentsInChildren<MeshFilter>(true);
            var seams = decor.Where(f => f.name.StartsWith(PuzzleBoardPresenter.RegionSeamName)).ToList();
            Assert.That(seams.Count, Is.EqualTo(3), "groove, piping and stitch: one combined mesh each");
            foreach (var seam in seams)
            {
                Assert.That(seam.GetComponents<Collider>(), Is.Empty, "no picking surface");
                foreach (var v in seam.sharedMesh.vertices.Select(v => seam.transform.TransformPoint(v) - origin))
                {
                    var horizontal = Mathf.Abs(v.z + 2f) < 0.1f && v.x > -0.05f && v.x < 5.05f;
                    var vertical = Mathf.Abs(v.x - 3f) < 0.1f && v.z < -1.9f && v.z > -7.05f;
                    Assert.That(horizontal || vertical, Is.True, seam.name + " vertex off the zone boundary: " + v);
                }
            }
            var seamBounds = Encapsulate(seams.Select(s => s.GetComponent<Renderer>()));
            Assert.That(seamBounds.size.x, Is.GreaterThan(4.9f), "upper boundary spans the board");
            Assert.That(seamBounds.min.z - origin.z, Is.LessThan(-6.9f), "right boundary reaches the front wall");
            Assert.That(seamBounds.min.y - origin.y, Is.GreaterThan(scene.Board.FloorLift), "seams sit on top of the padded floor");
            Assert.That(scene.Board.Compartments["main"].GetComponentsInChildren<Renderer>().Any(r => r.enabled && r.gameObject.activeInHierarchy
                && r.name.StartsWith("Cell ")), Is.False, "logical cell guides stay hidden");

            scene.NextLevel(); // lv2-rotate authors no zones: one region, nothing to separate
            yield return null;
            Assert.That(scene.Board.GetComponentsInChildren<MeshFilter>(true)
                .Any(f => f.name.StartsWith(PuzzleBoardPresenter.RegionSeamName)),
                Is.False);
        }

        [UnityTest]
        public IEnumerator Highlights_GoldMarksTheActiveRuleRegion_BlueOnlyMarksAValidDrop()
        {
            yield return Open();
            var scene = Scene;
            Assert.That(scene.Rules.VisibleCues, Is.EquivalentTo(new[] { "zone:shampoo-right:main" }),
                "idle: the active requirement (Şampuan -> sağ) in gold; the met passport rule has none");
            var zone = scene.Rules.GetComponentsInChildren<Renderer>().Single(r => r.name == "Rule Cue zone:shampoo-right:main");
            Assert.That(zone.sharedMaterial.GetColor(PresentationKit.BaseColorId), Is.EqualTo(PuzzleRulesPresenter.ZoneTint).Using(ColorEqualityComparer.Instance));
            Assert.That(PuzzleRulesPresenter.ZoneTint.r, Is.GreaterThan(PuzzleRulesPresenter.ZoneTint.b), "gold, not blue");
            Assert.That(scene.Drag.FootprintPreview == null || !scene.Drag.FootprintPreview.gameObject.activeInHierarchy, Is.True,
                "no blue glow when nothing is dragged");

            var view = scene.Tray.ItemViews["shampoo-1"];
            var first = view.Footprint.OccupiedCells[0];
            scene.Drag.BeginDrag("shampoo-1", view.transform.position + view.transform.lossyScale.x * new Vector3(first.X + .5f, 0f, -(first.Y + .5f)));
            var frame = scene.Board.CompartmentFrames().Single();
            scene.Drag.UpdateDrag(frame.Origin + new Vector3(3.5f + first.X, 0f, -(2.5f + first.Y)));
            yield return null;
            Assert.That(scene.Drag.PreviewValid, Is.True);
            var blue = scene.Drag.FootprintPreview.sharedMaterial.GetColor(PresentationKit.BaseColorId);
            Assert.That(blue.b, Is.GreaterThan(blue.r).And.GreaterThan(blue.g), "valid drop is blue");
            Assert.That(scene.Rules.VisibleCues, Does.Contain("zone:shampoo-right:main"), "blue reads inside the gold region");

            scene.Drag.UpdateDrag(frame.Origin + new Vector3(3.5f + first.X, 0f, -(0.5f + first.Y))); // over the passport
            yield return null;
            Assert.That(scene.Drag.PreviewValid, Is.False);
            var invalid = scene.Drag.FootprintPreview.sharedMaterial.GetColor(PresentationKit.BaseColorId);
            Assert.That(invalid.r, Is.GreaterThan(invalid.b), "an illegal drop is never blue");
            scene.Drag.Cancel();
            Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(scene.Level.InitialState.Hash), "highlights never change state");
        }

        [UnityTest]
        public IEnumerator TargetOverlay_FitsTheWidth_NeverStretches_AndIgnoresInput()
        {
            yield return Open();
            var overlay = TargetCompositionOverlay.Instance;
            Assert.That(overlay, Is.Not.Null, "editor/development builds create the overlay");
            Assert.That(overlay.Visible, Is.False, "off by default");
            Assert.That(overlay.Opacity, Is.EqualTo(0.5f));
            Assert.That(overlay.Reference, Is.Not.Null);
            Assert.That(overlay.Reference.width * 16, Is.EqualTo(overlay.Reference.height * 9), "9:16 reference");
            Assert.That(overlay.GetComponentInChildren<RawImage>(true).enabled, Is.False);

            overlay.Visible = true;
            overlay.RenderThrough(Scene.Camera);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var image = overlay.GetComponentInChildren<RawImage>();
            Assert.That(image.enabled, Is.True);
            Assert.That(image.color.a, Is.EqualTo(0.5f));
            Assert.That(image.raycastTarget, Is.False);
            Assert.That(overlay.GetComponentsInChildren<GraphicRaycaster>(true), Is.Empty, "never in the input path");
            var corners = new Vector3[4];
            overlay.Frame.GetWorldCorners(corners);
            var screen = corners.Select(c => UiProjection.Screen(overlay.Frame, c)).ToArray();
            Assert.That(screen.Min(p => p.x), Is.EqualTo(0f).Within(1f));
            Assert.That(screen.Max(p => p.x), Is.EqualTo(1080f).Within(1f));
            Assert.That(screen.Min(p => p.y), Is.EqualTo(210f).Within(1f), "bottom offset");
            Assert.That(screen.Max(p => p.y), Is.EqualTo(2130f).Within(1f), "top offset 2340 - 2130 = 210");
        }

        [UnityTest, Explicit("Writes ART-CC02 review captures")]
        public IEnumerator CaptureEvidence()
        {
            var quality = QualitySettings.GetQualityLevel();
            QualitySettings.SetQualityLevel(System.Array.IndexOf(QualitySettings.names, "Mobile"), true);
            var folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Builds/art-cc02/after"));
            System.IO.Directory.CreateDirectory(folder);
            try
            {
                yield return Open();
                var scene = Scene;
                yield return Shot(folder, "A-packing");
                var overlay = TargetCompositionOverlay.Instance;
                overlay.Visible = true;
                overlay.RenderThrough(scene.Camera);
                yield return Shot(folder, "B-target-overlay-50");
                overlay.Visible = false;
                foreach (var view in scene.Board.ItemViews.Values.Concat(scene.Tray.ItemViews.Values))
                    view.gameObject.SetActive(false);
                yield return Shot(folder, "C-regions-items-hidden");
                foreach (var view in scene.Board.ItemViews.Values.Concat(scene.Tray.ItemViews.Values))
                    view.gameObject.SetActive(true);
                var shampoo = scene.Tray.ItemViews["shampoo-1"];
                var first = shampoo.Footprint.OccupiedCells[0];
                scene.Drag.BeginDrag("shampoo-1", shampoo.transform.position
                    + shampoo.transform.lossyScale.x * new Vector3(first.X + .5f, 0f, -(first.Y + .5f)));
                scene.Drag.UpdateDrag(scene.Board.CompartmentFrames().Single().Origin + new Vector3(3.5f + first.X, 0f, -(2.5f + first.Y)));
                yield return Shot(folder, "D-drag-valid-blue-in-gold");
                scene.Drag.Cancel();
                Place(scene, "travel-pouch-1", 1, 3);
                Place(scene, "sunglasses-1", 3, 6);
                Place(scene, "shampoo-1", 3, 2);
                yield return Shot(folder, "E-zip-it-begin");
                scene.Completion.Advance(PuzzleCompletionPresenter.SettleDuration + PuzzleCompletionPresenter.AnticipationDuration
                    + PuzzleCompletionPresenter.RuleCascadeDuration + PuzzleCompletionPresenter.StrapsDuration
                    + PuzzleCompletionPresenter.LidDuration * 0.5f);
                yield return Shot(folder, "F-zip02-mid-close");
                scene.Completion.Advance(PuzzleCompletionPresenter.FullDuration);
                yield return Shot(folder, "G-zip02-final");
                scene.Restart();
                yield return null;
                TearDown();
                yield return Open(1080, 1920);
                yield return Shot(folder, "H-1080x1920-packing");
                Debug.Log("[ART-CC02] captures " + folder);
            }
            finally
            {
                QualitySettings.SetQualityLevel(quality, true);
            }
        }

        private IEnumerator Shot(string folder, string name)
        {
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            var camera = Scene.Camera;
            camera.Render();
            RenderTexture.active = _target;
            var image = new Texture2D(_target.width, _target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, _target.width, _target.height), 0, 0);
            image.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            RenderTexture.active = null;
            Object.Destroy(image);
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
    }
}
