using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZipTrip.Domain;
using ZipTrip.Unity;

namespace ZipTrip.Tests.PlayMode
{
    public class GameplaySandboxTests
    {
        [UnityTest]
        public IEnumerator Framing_9By16_ContainsBoardAndFourTrayViews() =>
            AssertPresentationFits(9f / 16f);

        [UnityTest]
        public IEnumerator Framing_9By19Point5_ContainsBoardAndFourTrayViews() =>
            AssertPresentationFits(9f / 19.5f);

        [UnityTest]
        public IEnumerator Framing_9By20_ContainsBoardAndFourTrayViews() =>
            AssertPresentationFits(9f / 20f);

        private static IEnumerator AssertPresentationFits(float aspect)
        {
            yield return SceneManager.LoadSceneAsync("GameplaySandbox");
            yield return null;

            var board = Object.FindFirstObjectByType<BoardPresenter>();
            var camera = Camera.main;
            camera.aspect = aspect;
            camera.GetComponent<FixedGameplayCamera>().Configure(board.PresentedState.Container,
                aspect, board.PresentationBounds);
            Assert.AreEqual(4, board.ItemViews.Count);
            Assert.AreEqual(31, board.ValidCellCount);
            Assert.AreEqual(4, board.BlockedCellCount);

            var outer = FixedGameplayCamera.OuterBounds(board.PresentedState.Container.Mask);
            foreach (var x in new[] { outer.xMin, outer.xMax })
            foreach (var y in new[] { outer.yMin, outer.yMax })
                AssertInside(camera, new Vector3(x, 0f, -y), false);

            var expected = new[] { "book", "camera", "bottle", "sneaker" };
            for (var i = 0; i < expected.Length; i++)
            {
                var view = board.ItemViews[i];
                Assert.AreEqual(expected[i], view.ItemId);
                foreach (var renderer in view.GetComponentsInChildren<Renderer>())
                {
                    var bounds = renderer.bounds;
                    var min = bounds.min;
                    var max = bounds.max;
                    for (var x = 0; x < 2; x++)
                    for (var y = 0; y < 2; y++)
                    for (var z = 0; z < 2; z++)
                        AssertInside(camera, new Vector3(x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y, z == 0 ? min.z : max.z), true);
                }
            }
        }

        private static void AssertInside(Camera camera, Vector3 world, bool tray)
        {
            var point = camera.WorldToViewportPoint(world);
            Assert.That(point.z, Is.GreaterThan(camera.nearClipPlane));
            Assert.That(point.x, Is.InRange(0f, 1f));
            Assert.That(point.y, Is.InRange(tray ? 0.02f : 0f, 1f));
        }

        [UnityTest]
        public IEnumerator L1_PresentsAuthoritativeInitialState()
        {
            yield return SceneManager.LoadSceneAsync("GameplaySandbox");
            yield return null;

            var camera = Camera.main;
            Assert.IsNotNull(camera);
            Assert.IsTrue(camera.orthographic);
            Assert.That(camera.transform.eulerAngles.x, Is.EqualTo(75f).Within(0.001f));
            Assert.IsNotNull(camera.GetComponent<FixedGameplayCamera>());

            var root = Object.FindFirstObjectByType<PhaseAL1Presentation>();
            var board = root.GetComponent<BoardPresenter>();
            Assert.IsNotNull(root.Level);
            Assert.AreEqual("L1", root.Level.Id);
            Assert.AreEqual("backpack_std", root.Level.ContainerId);
            Assert.AreSame(root.Level.InitialState, board.PresentedState);
            Assert.AreEqual(31, board.ValidCellCount);
            Assert.AreEqual(4, board.BlockedCellCount);
            Assert.AreEqual(0, board.PresentedState.Placements.Count);
            Assert.AreEqual(4, board.PresentedState.Tray.Count);
            Assert.AreEqual(4, board.ItemViews.Count);

            var expected = new HashSet<string> { "book", "camera", "bottle", "sneaker" };
            for (var i = 0; i < board.ItemViews.Count; i++)
            {
                var view = board.ItemViews[i];
                var tray = board.PresentedState.Tray[i];
                Assert.IsTrue(view.IsInTray);
                Assert.AreSame(root.Level.Items[tray.ItemId], view.Item);
                Assert.AreEqual(tray.ItemId, view.ItemId);
                Assert.AreEqual(tray.ShapeState, view.ShapeState);
                Assert.AreEqual(tray.Rotation, view.Rotation);
                Assert.IsTrue(expected.Remove(view.ItemId));
                Assert.AreEqual(root.Level.Items[view.ItemId]
                    .GetRotatedShape(view.ShapeState, view.Rotation).Shape.CellCount,
                    view.Footprint.CellCount);
                Assert.AreEqual(view.Footprint.CellCount, view.VisualRoot.childCount);
            }
            Assert.IsEmpty(expected);
            Assert.AreEqual(4, board.ItemViews[3].Footprint.CellCount);
            Assert.AreEqual(root.InitialStateHash, StateHash.Compute(root.Level.InitialState));
            Assert.AreEqual(0, root.Level.InitialState.Occupancy.Count);
        }

        [UnityTest]
        public IEnumerator L1_DragPreviewIsWiredWithoutStateMutation()
        {
            yield return SceneManager.LoadSceneAsync("GameplaySandbox");
            yield return null;

            var root = Object.FindFirstObjectByType<PhaseAL1Presentation>();
            var board = root.GetComponent<BoardPresenter>();
            var pointer = root.GetComponent<PointerInteractor>();
            var preview = root.GetComponent<DragPreviewPresenter>();
            Assert.IsNotNull(pointer);
            Assert.IsNotNull(preview);
            var initial = StateHash.Compute(board.PresentedState);
            var sneaker = board.ItemViews[3];
            var point = sneaker.transform.TransformPoint(new Vector3(0.5f, 0f, -0.5f));
            var screen = Camera.main.WorldToScreenPoint(new Vector3(point.x, 0f, point.z));
            preview.HandlePointer(new PointerSignal(PointerPhase.Down, screen));
            Assert.AreSame(sneaker, preview.ActiveItem);
            preview.HandlePointer(new PointerSignal(PointerPhase.Up, screen));
            Assert.IsNull(preview.ActiveItem);
            Assert.AreEqual(initial, StateHash.Compute(board.PresentedState));
        }
    }
}
