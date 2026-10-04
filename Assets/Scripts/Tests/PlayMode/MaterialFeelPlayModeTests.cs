using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZipTrip.Unity;

namespace ZipTrip.Tests.PlayMode
{
    public sealed class MaterialFeelPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/PuzzleGameplay.unity";

        private static IEnumerator Boot()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
        }

        private static PuzzleGameplayScene Scene => Object.FindFirstObjectByType<PuzzleGameplayScene>();

        [UnityTest]
        public IEnumerator FabricPlasticPaperLeather_SettleAtDistinctRates_WithoutChangingState()
        {
            yield return Boot();
            var scene = Scene;
            var hash = scene.Session.CurrentState.Hash;
            foreach (var id in new[] { "towel-1", "shampoo-1", "passport-1", "travel-pouch-1" })
            {
                var view = scene.Board.ItemViews.TryGetValue(id, out var packed) ? packed : scene.Tray.ItemViews[id];
                var feedback = view.Feedback;
                feedback.PlaySettle();
                Assert.That(feedback.Root.localPosition.y, Is.GreaterThan(0f), id + " starts above rest");
                yield return new WaitForSecondsRealtime(0.14f);
                Assert.That(feedback.IsAnimating, Is.True, id + " still settling");
                Assert.That(feedback.Root.localScale.y, Is.Not.EqualTo(1f).Within(0.0001f), id + " has its own settle response");
                yield return new WaitForSecondsRealtime(feedback.Profile.SettleDuration);
                Assert.That(feedback.IsAnimating, Is.False, id + " completes");
                Assert.That(feedback.Root.localScale, Is.EqualTo(Vector3.one), id + " canonical visual pose");
                Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(hash), id + " presentation only");
            }
            Assert.That(PuzzleItemCatalog.ResolveMotion("towel").SettleDuration,
                Is.GreaterThan(PuzzleItemCatalog.ResolveMotion("shampoo").SettleDuration));
            Assert.That(PuzzleItemCatalog.ResolveMotion("travel-pouch").SettleDuration,
                Is.GreaterThan(PuzzleItemCatalog.ResolveMotion("passport").SettleDuration));
        }

        [UnityTest]
        public IEnumerator DragTilt_CancelAndRegrab_ResetPoseWithoutStateChange()
        {
            yield return Boot();
            var scene = Scene;
            var view = scene.Tray.ItemViews["shampoo-1"];
            var hash = scene.Session.CurrentState.Hash;
            var grab = GoldenLv1LayoutReview.TrayGrab(view);
            Assert.That(scene.Drag.BeginDrag("shampoo-1", grab), Is.EqualTo(DragBeginResult.Started));
            scene.Drag.UpdateDrag(grab + Vector3.right);
            Assert.That(Quaternion.Angle(view.Feedback.Root.localRotation, Quaternion.identity),
                Is.InRange(0.1f, MotionTokens.DragTiltMaxDegrees + 0.01f));
            scene.Drag.Cancel();
            Assert.That(view.Feedback.Root.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(hash));
            Assert.That(scene.Drag.BeginDrag("shampoo-1", grab), Is.EqualTo(DragBeginResult.Started));
            Assert.That(view.Feedback.Root.localRotation, Is.EqualTo(Quaternion.identity));
            scene.Drag.Cancel();
        }

        [UnityTest]
        public IEnumerator UndoDuringSettle_RestoresStateAndCompletionRemainsUnchanged()
        {
            yield return Boot();
            var scene = Scene;
            var initial = scene.Session.CurrentState.Hash;
            var view = scene.Tray.ItemViews["travel-pouch-1"];
            Assert.That(scene.Drag.BeginDrag("travel-pouch-1", GoldenLv1LayoutReview.TrayGrab(view)),
                Is.EqualTo(DragBeginResult.Started));
            var target = scene.Board.Compartments["main"].transform.position + new Vector3(1.5f, 0f, -3.5f);
            scene.Drag.UpdateDrag(target);
            Assert.That(scene.Drag.PreviewValid, Is.True);
            var step = scene.Drag.Drop();
            Assert.That(step.Move.IsAccepted, Is.True);
            Assert.That(scene.Board.ItemViews["travel-pouch-1"].Feedback.IsAnimating, Is.True);
            Assert.That(scene.Undo(), Is.True);
            Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(initial));
            Assert.That(scene.Session.CurrentCompletion.IsComplete, Is.False);
            Assert.That(scene.Tray.ItemViews["travel-pouch-1"].Feedback.Root.localRotation,
                Is.EqualTo(Quaternion.identity));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RapidRegrab_InterruptsSettleAndKeepsInputImmediate()
        {
            yield return Boot();
            var scene = Scene;
            var view = scene.Board.ItemViews["towel-1"];
            var hash = scene.Session.CurrentState.Hash;
            view.Feedback.PlaySettle();
            Assert.That(view.Feedback.IsAnimating, Is.True);
            var grab = view.transform.position + new Vector3(0.5f, 0f, -0.5f);
            Assert.That(scene.Drag.BeginDrag("towel-1", grab), Is.EqualTo(DragBeginResult.Started));
            Assert.That(view.Feedback.IsHeld, Is.True);
            Assert.That(scene.Drag.IsDragging, Is.True);
            Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(hash));
            scene.Drag.Cancel();
            Assert.That(view.Feedback.Root.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(hash));
        }
    }
}
