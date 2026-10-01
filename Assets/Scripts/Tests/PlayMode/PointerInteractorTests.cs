using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using ZipTrip.Unity;

namespace ZipTrip.Tests.PlayMode
{
    public class PointerInteractorTests : InputTestFixture
    {
        private GameObject _object;
        private PointerInteractor _interactor;
        private List<PointerSignal> _signals;

        public override void Setup()
        {
            base.Setup();
            _object = new GameObject("Pointer interactor test");
            _interactor = _object.AddComponent<PointerInteractor>();
            _signals = new List<PointerSignal>();
            _interactor.PointerEvent += _signals.Add;
        }

        public override void TearDown()
        {
            Object.DestroyImmediate(_object);
            base.TearDown();
        }

        [Test]
        public void MouseAndTouch_ProduceTheSameDownMoveUpFlow()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            Set(mouse.position, new Vector2(10f, 20f));
            Press(mouse.leftButton);
            _interactor.Poll();
            Set(mouse.position, new Vector2(30f, 40f));
            _interactor.Poll();
            Release(mouse.leftButton);
            _interactor.Poll();
            AssertFlow(new[] { PointerPhase.Down, PointerPhase.Move, PointerPhase.Up },
                new[] { new Vector2(10f, 20f), new Vector2(30f, 40f), new Vector2(30f, 40f) });

            _signals.Clear();
            InputSystem.RemoveDevice(mouse);
            var screen = InputSystem.AddDevice<Touchscreen>();
            BeginTouch(11, new Vector2(10f, 20f), screen: screen);
            _interactor.Poll();
            MoveTouch(11, new Vector2(30f, 40f), screen: screen);
            _interactor.Poll();
            EndTouch(11, new Vector2(30f, 40f), screen: screen);
            _interactor.Poll();
            AssertFlow(new[] { PointerPhase.Down, PointerPhase.Move, PointerPhase.Up },
                new[] { new Vector2(10f, 20f), new Vector2(30f, 40f), new Vector2(30f, 40f) });
            Assert.IsFalse(_interactor.HasActivePointer);
        }

        [Test]
        public void TouchCancel_ReleasesOwnershipAndAllowsNextTouch()
        {
            var screen = InputSystem.AddDevice<Touchscreen>();
            BeginTouch(1, new Vector2(10f, 10f), screen: screen);
            _interactor.Poll();
            CancelTouch(1, new Vector2(11f, 12f), screen: screen);
            _interactor.Poll();
            Assert.IsFalse(_interactor.HasActivePointer);
            BeginTouch(2, new Vector2(20f, 20f), screen: screen);
            _interactor.Poll();
            AssertFlow(new[] { PointerPhase.Down, PointerPhase.Cancel, PointerPhase.Down },
                new[] { new Vector2(10f, 10f), new Vector2(11f, 12f), new Vector2(20f, 20f) });
        }

        [Test]
        public void SecondTouch_CannotTakeOrRetakeActiveInteraction()
        {
            var screen = InputSystem.AddDevice<Touchscreen>();
            BeginTouch(1, new Vector2(10f, 10f), screen: screen);
            _interactor.Poll();
            BeginTouch(2, new Vector2(50f, 50f), screen: screen);
            _interactor.Poll();
            MoveTouch(2, new Vector2(60f, 60f), screen: screen);
            _interactor.Poll();
            MoveTouch(1, new Vector2(12f, 14f), screen: screen);
            _interactor.Poll();
            EndTouch(1, new Vector2(12f, 14f), screen: screen);
            _interactor.Poll();
            _interactor.Poll();
            AssertFlow(new[] { PointerPhase.Down, PointerPhase.Move, PointerPhase.Up },
                new[] { new Vector2(10f, 10f), new Vector2(12f, 14f), new Vector2(12f, 14f) });
            Assert.IsFalse(_interactor.HasActivePointer);

            EndTouch(2, new Vector2(60f, 60f), screen: screen);
            _interactor.Poll();
            BeginTouch(3, new Vector2(30f, 30f), screen: screen);
            _interactor.Poll();
            Assert.AreEqual(4, _signals.Count);
            Assert.AreEqual(PointerPhase.Down, _signals[3].Phase);
            Assert.AreEqual(new Vector2(30f, 30f), _signals[3].ScreenPosition);
        }

        [Test]
        public void DisableWithActivePointer_EmitsCancel()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            Set(mouse.position, new Vector2(4f, 5f));
            Press(mouse.leftButton);
            _interactor.Poll();
            _interactor.enabled = false;
            AssertFlow(new[] { PointerPhase.Down, PointerPhase.Cancel },
                new[] { new Vector2(4f, 5f), new Vector2(4f, 5f) });
            Assert.IsFalse(_interactor.HasActivePointer);
        }

        private void AssertFlow(PointerPhase[] phases, Vector2[] positions)
        {
            Assert.AreEqual(phases.Length, _signals.Count);
            for (var i = 0; i < phases.Length; i++)
            {
                Assert.AreEqual(phases[i], _signals[i].Phase, "phase at " + i);
                Assert.AreEqual(positions[i], _signals[i].ScreenPosition, "position at " + i);
            }
        }
    }
}
