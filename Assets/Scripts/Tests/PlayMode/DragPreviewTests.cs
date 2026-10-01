using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using ZipTrip.Domain;
using ZipTrip.Unity;

namespace ZipTrip.Tests.PlayMode
{
    public sealed class DragPreviewTests : InputTestFixture
    {
        private GameObject _root;
        private GameObject _cameraObject;
        private BoardPresenter _board;
        private DragPreviewPresenter _preview;
        private PointerInteractor _pointer;
        private Camera _camera;
        private ItemView _sneaker;
        private ulong _initialHash;
        private Vector3 _grabWorld;
        private Vector3 _restPosition;
        private Vector3 _restScale;

        public override void Setup()
        {
            base.Setup();
            var level = PhaseALevels.Load("L1");
            _initialHash = StateHash.Compute(level.InitialState);
            _root = new GameObject("ZT-012 preview test");
            _board = _root.AddComponent<BoardPresenter>();
            _board.Present(level, level.InitialState);
            _pointer = _root.AddComponent<PointerInteractor>();
            _pointer.enabled = false;
            _cameraObject = new GameObject("ZT-012 camera");
            _camera = _cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 8f;
            _camera.pixelRect = new Rect(0f, 0f, 800f, 800f);
            _camera.transform.position = new Vector3(2.5f, 12f, -3.5f);
            _camera.transform.rotation = Quaternion.Euler(75f, 0f, 0f);
            _preview = _root.AddComponent<DragPreviewPresenter>();
            _preview.Initialize(_board, _camera, _pointer);
            _sneaker = _board.ItemViews[3];
            _restPosition = _sneaker.transform.position;
            _restScale = _sneaker.transform.localScale;
        }

        public override void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
            UnityEngine.Object.DestroyImmediate(_cameraObject);
            base.TearDown();
        }

        [Test]
        public void OccupiedSneakerCellSelectsAndLiftsWithoutInitialHorizontalJump()
        {
            DownOnSneaker();
            Assert.AreSame(_sneaker, _preview.ActiveItem);
            Assert.AreEqual(_restPosition.x, _sneaker.transform.position.x, 0.0001f);
            Assert.AreEqual(_restPosition.z, _sneaker.transform.position.z, 0.0001f);
            Assert.AreEqual(_restPosition.y + 0.15f, _sneaker.transform.position.y, 0.0001f);
            Assert.IsTrue(_preview.GhostVisible);
            AssertUnchanged();
        }

        [Test]
        public void EmptyCellInsideSneakerBoundingRectangleDoesNotSelect()
        {
            var empty = _sneaker.transform.TransformPoint(new Vector3(1.5f, 0f, -0.5f));
            _preview.HandlePointer(new PointerSignal(PointerPhase.Down, Screen(empty)));
            Assert.IsNull(_preview.ActiveItem);
            Assert.IsFalse(_preview.GhostVisible);
            AssertUnchanged();
        }

        [Test]
        public void ValidAnchorShowsFullCleanFootprint()
        {
            DownOnSneaker();
            MoveTo(new Cell(1, 1));
            Assert.AreEqual(new Cell(1, 1), _preview.CandidateAnchor);
            Assert.IsTrue(_preview.Validation.IsValid);
            Assert.AreEqual(4, _preview.GhostCellCount);
            Assert.AreEqual(0, _preview.GhostMarkerCount);
            Assert.AreEqual(4, ActiveOutlineCount());
            AssertUnchanged();
        }

        [Test]
        public void NonCentralGrabAlignsFullSizeSneakerWithOnlyOccupiedGhostCells()
        {
            _grabWorld = _sneaker.transform.TransformPoint(new Vector3(0.75f, 0f, -2.75f));
            _preview.HandlePointer(new PointerSignal(PointerPhase.Down, Screen(_grabWorld)));
            var anchor = new Cell(1, 1);
            var rawPointer = new Vector3(anchor.X, 0f, -anchor.Y) - (_restPosition - _grabWorld);
            Assert.AreNotEqual(anchor, GridProjector.WorldToAnchor(rawPointer));

            MoveTo(anchor);

            Assert.IsTrue(_preview.Validation.IsValid);
            Assert.AreEqual(anchor, _preview.CandidateAnchor);
            Assert.Less(Vector3.Distance(new Vector3(1f, 0.15f, -1f),
                _sneaker.transform.position), 0.0001f);
            Assert.AreEqual(Vector3.one, _sneaker.transform.localScale);

            var ghostCells = new List<Transform>();
            var outlines = new List<Transform>();
            foreach (var child in _preview.GetComponentsInChildren<Transform>(true))
            {
                if (!child.gameObject.activeInHierarchy)
                    continue;
                if (child.name == "Ghost cell") ghostCells.Add(child);
                if (child.name == "Valid outline") outlines.Add(child);
            }
            Assert.AreEqual(4, ghostCells.Count);
            Assert.AreEqual(4, outlines.Count);

            var visualCells = _sneaker.VisualRoot.GetComponentsInChildren<Renderer>();
            Assert.AreEqual(4, visualCells.Length);
            foreach (var visual in visualCells)
                Assert.IsTrue(ghostCells.Exists(ghost => SameXZ(ghost.position, visual.bounds.center)),
                    "Every dragged visual cell must cover a canonical ghost cell.");
            foreach (var ghost in ghostCells)
                Assert.IsTrue(outlines.Exists(outline =>
                    Vector2.Distance(_camera.WorldToScreenPoint(ghost.position),
                        _camera.WorldToScreenPoint(outline.position)) < 1f),
                    "Every occupied ghost cell must have a screen-aligned outline.");
            Assert.IsFalse(ghostCells.Exists(ghost => SameXZ(ghost.position,
                new Vector3(2.5f, 0f, -1.5f))), "The empty L corner must have no ghost fill.");
            AssertUnchanged();
        }

        [TestCase(7, 8, PlacementFailureReason.OutOfBounds, 8, 10)]
        [TestCase(0, 0, PlacementFailureReason.OutsideMask, 0, 0)]
        public void InvalidAnchorMarksDomainOffendingCells(int x, int y,
            PlacementFailureReason reason, int markedX, int markedY)
        {
            DownOnSneaker();
            MoveTo(new Cell(x, y));
            Assert.AreEqual(reason, _preview.Validation.Reason);
            Assert.AreEqual(4, _preview.GhostCellCount);
            Assert.Greater(_preview.GhostMarkerCount, 0);
            Assert.AreEqual(0, ActiveOutlineCount());
            Assert.IsTrue(_preview.IsMarked(new Cell(markedX, markedY)));
            AssertUnchanged();
        }

        [Test]
        public void OverlapMarksOnlyDomainOffendingCell()
        {
            var item = _sneaker.Item;
            var level = PhaseALevels.Load("L1");
            UnityEngine.Object.DestroyImmediate(_root);
            _root = new GameObject("ZT-012 overlap test");
            _board = _root.AddComponent<BoardPresenter>();
            var placedBook = new PlacedItem(level.Items["book"], new Cell(1, 1),
                Rotation.Degrees0, "open");
            var state = new GameState(level.InitialState.Container, new[] { placedBook },
                new[] { new TrayItem(item.Id, Rotation.Degrees0, "open") }, Array.Empty<string>());
            _initialHash = StateHash.Compute(state);
            _board.Present(level, state);
            _pointer = _root.AddComponent<PointerInteractor>();
            _pointer.enabled = false;
            _preview = _root.AddComponent<DragPreviewPresenter>();
            _preview.Initialize(_board, _camera, _pointer);
            _sneaker = _board.ItemViews[1];
            _restPosition = _sneaker.transform.position;

            DownOnSneaker();
            MoveTo(new Cell(1, 1));
            Assert.AreEqual(PlacementFailureReason.Overlap, _preview.Validation.Reason);
            Assert.IsTrue(_preview.IsMarked(new Cell(1, 1)));
            Assert.IsTrue(_preview.IsMarked(new Cell(1, 2)));
            AssertUnchanged();
        }

        [TestCase(PointerPhase.Up)]
        [TestCase(PointerPhase.Cancel)]
        public void EndingPreviewRestoresTrayViewAndState(PointerPhase endPhase)
        {
            DownOnSneaker();
            MoveTo(new Cell(1, 1));
            _preview.HandlePointer(new PointerSignal(endPhase, Screen(_grabWorld)));
            Assert.IsNull(_preview.ActiveItem);
            Assert.IsFalse(_preview.GhostVisible);
            Assert.AreEqual(_restPosition, _sneaker.transform.position);
            Assert.AreEqual(_restScale, _sneaker.transform.localScale);
            AssertUnchanged();
        }

        [Test]
        public void SecondTouchCannotChangeActivePreview()
        {
            _grabWorld = _sneaker.transform.TransformPoint(new Vector3(0.5f, 0f, -0.5f));
            var screen = InputSystem.AddDevice<Touchscreen>();
            BeginTouch(1, Screen(_grabWorld), screen: screen);
            _pointer.Poll();
            Assert.AreSame(_sneaker, _preview.ActiveItem);
            var anchor = _preview.CandidateAnchor;
            var position = _sneaker.transform.position;
            BeginTouch(2, Screen(new Vector3(2f, 0f, -2f)), screen: screen);
            _pointer.Poll();
            MoveTouch(2, Screen(new Vector3(4f, 0f, -4f)), screen: screen);
            _pointer.Poll();
            Assert.AreEqual(anchor, _preview.CandidateAnchor);
            Assert.AreEqual(position, _sneaker.transform.position);
            EndTouch(1, Screen(_grabWorld), screen: screen);
            _pointer.Poll();
            Assert.IsNull(_preview.ActiveItem);
            AssertUnchanged();
        }

        [Test]
        public void RepeatedActivePreviewMoves_AllocateZeroManagedBytesAfterWarmup()
        {
            DownOnSneaker();
            for (var i = 0; i < 200; i++)
                MoveTo(new Cell(i % 2 + 1, 1));

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 200; i++)
                MoveTo(new Cell(i % 2 + 1, 1));
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated);
        }

        private void DownOnSneaker()
        {
            _grabWorld = _sneaker.transform.TransformPoint(new Vector3(0.5f, 0f, -0.5f));
            _preview.HandlePointer(new PointerSignal(PointerPhase.Down, Screen(_grabWorld)));
        }

        private int ActiveOutlineCount()
        {
            var count = 0;
            foreach (var child in _preview.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != "Valid outline" || !child.gameObject.activeSelf)
                    continue;
                Assert.AreEqual(4, child.childCount);
                for (var i = 0; i < child.childCount; i++)
                    Assert.AreEqual(0.06f, Mathf.Min(child.GetChild(i).localScale.x,
                        child.GetChild(i).localScale.z), 0.0001f);
                count++;
            }
            return count;
        }

        private void MoveTo(Cell anchor)
        {
            var grabOffset = _restPosition - _grabWorld;
            var pointerWorld = new Vector3(anchor.X, 0f, -anchor.Y) - grabOffset;
            _preview.HandlePointer(new PointerSignal(PointerPhase.Move, Screen(pointerWorld)));
        }

        private static bool SameXZ(Vector3 a, Vector3 b) =>
            Mathf.Abs(a.x - b.x) < 0.0001f && Mathf.Abs(a.z - b.z) < 0.0001f;

        private Vector2 Screen(Vector3 world) => _camera.WorldToScreenPoint(
            new Vector3(world.x, 0f, world.z));

        private void AssertUnchanged() =>
            Assert.AreEqual(_initialHash, StateHash.Compute(_board.PresentedState));
    }
}
