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
    public sealed class PremiumPresentationTests
    {
        [UnityTest]
        public IEnumerator ItemPresence_StaysInsideFootprints_AndRejectDoesNotMutateState()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/PuzzleGameplay.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
            var initial = scene.Session.CurrentState.Hash;
            foreach (var view in scene.Board.ItemViews.Values.Concat(scene.Tray.ItemViews.Values))
            {
                if (view.DefinitionId == "sunglasses") continue;
                view.Feedback.CompleteAll();
                var bounds = view.VisualRoot.GetComponentInChildren<Renderer>().bounds;
                var low = view.transform.InverseTransformPoint(bounds.min);
                var high = view.transform.InverseTransformPoint(bounds.max);
                var width = view.Footprint.OccupiedCells.Max(c => c.X) + 1;
                var depth = view.Footprint.OccupiedCells.Max(c => c.Y) + 1;
                Assert.That((high.x - low.x) / width, Is.InRange(.85f, .98f), view.InstanceId + " width occupancy");
                Assert.That((high.z - low.z) / depth, Is.InRange(.85f, .98f), view.InstanceId + " depth occupancy");
                Assert.That(low.x, Is.GreaterThanOrEqualTo(.045f), view.InstanceId);
                Assert.That(high.x, Is.LessThanOrEqualTo(width - .045f), view.InstanceId);
                Assert.That(low.z, Is.GreaterThanOrEqualTo(-depth + .045f), view.InstanceId);
                Assert.That(high.z, Is.LessThanOrEqualTo(-.045f), view.InstanceId);
                Assert.That(view.VisualRoot.GetComponentsInChildren<Collider>(), Is.Empty);
                Debug.Log($"[PREMIUM OCCUPANCY] {view.InstanceId}: {(high.x - low.x) / width:P1} x {(high.z - low.z) / depth:P1}");
            }
            var shampoo = scene.Tray.ItemViews["shampoo-1"];
            var grab = shampoo.transform.position + shampoo.transform.lossyScale.x * new Vector3(.5f, 0f, -.5f);
            Assert.That(scene.Drag.BeginDrag("shampoo-1", grab), Is.EqualTo(DragBeginResult.Started));
            var origin = scene.Board.CompartmentFrames().Single().Origin;
            scene.Drag.UpdateDrag(origin + new Vector3(3.5f, 0f, -.5f));
            Assert.That(scene.Drag.PreviewValid, Is.False, "occupied passport cells stay illegal");
            scene.Drag.Drop();
            Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(initial));
            shampoo = scene.Tray.ItemViews["shampoo-1"];
            grab = shampoo.transform.position + shampoo.transform.lossyScale.x * new Vector3(.5f, 0f, -.5f);
            Assert.That(scene.Drag.BeginDrag("shampoo-1", grab), Is.EqualTo(DragBeginResult.Started));
            scene.Drag.UpdateDrag(origin + new Vector3(3.5f, 0f, -2.5f));
            Assert.That(scene.Drag.PreviewValid, Is.True);
            Assert.That(scene.Drag.Drop().Move.IsAccepted, Is.True);
            Assert.That(scene.Board.ItemViews["shampoo-1"].Placement.Anchor, Is.EqualTo(new Cell(3, 2)));
        }
    }
}
