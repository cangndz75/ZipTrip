using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZipTrip.Application;
using ZipTrip.Domain;
using ZipTrip.Domain.Board;
using ZipTrip.Domain.Items;
using ZipTrip.Domain.Puzzle;
using ZipTrip.Unity;

namespace ZipTrip.Tests.PlayMode
{
    public sealed class PuzzleBoardPresenterTests
    {
        private static readonly Rotation[] All = { Rotation.Degrees0, Rotation.Degrees90, Rotation.Degrees180, Rotation.Degrees270 };
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.Destroy(_root);
        }

        // ---- Typed harness states ----

        private static IEnumerable<Cell> Rect(int w, int h) =>
            from y in Enumerable.Range(0, h) from x in Enumerable.Range(0, w) select new Cell(x, y);

        private static ItemSpec Block(string id, int w, int h, int thickness = 1, NestSpec nest = null) =>
            new ItemSpec(id, "default", new[] { new ItemStateSpec("default", new ItemShape(Rect(w, h)), thickness, All) }, null, null, nest);

        private static ItemSpec Jacket() => new ItemSpec("jacket", "normal",
            new[] { new ItemStateSpec("normal", new ItemShape(Rect(2, 2)), 2, All), new ItemStateSpec("compressed", new ItemShape(Rect(2, 2)), 1, All) },
            null, new[] { new StateTransition(ItemModifier.Compress, "normal", "compressed") });

        // main 4x3 L2 with (3,0) masked; pocket 2x1 L1.
        private static BoardSpec Board() => new BoardSpec(new[]
        {
            new Compartment("main", 4, 3, 2, Rect(4, 3).Where(c => c != new Cell(3, 0))),
            new Compartment("pocket", 2, 1, 1, Rect(2, 1))
        });

        private static PuzzleSpec Spec(BoardSpec board = null) =>
            new PuzzleSpec(board ?? Board(), 2, new[] { new ExtractionDestinationSpec("tray", 1, acceptedRoles: new[] { ObjectiveRole.ExtractionTarget }) });

        private static PuzzleItem At(string id, ItemSpec spec, int x, int y, int layer = 0, Rotation rotation = Rotation.Degrees0,
            string compartment = "main", string stateId = null) =>
            new PuzzleItem(id, spec, stateId ?? spec.DefaultStateId, ObjectiveRole.Required,
                ItemLocation.InSuitcase(new Placement(compartment, new Cell(x, y), layer, rotation)));

        private static PuzzleItem Off(string id, ItemSpec spec, ItemLocation location) =>
            new PuzzleItem(id, spec, spec.DefaultStateId, ObjectiveRole.Required, location);

        private PuzzleBoardPresenter Presenter(BoardSpec board, Material template = null, System.Func<PuzzleItem, GameObject> resolver = null)
        {
            _root = new GameObject("PuzzleBoardPresenter test");
            var presenter = _root.AddComponent<PuzzleBoardPresenter>();
            presenter.Present(board, template, resolver);
            return presenter;
        }

        // Domain columns covered by a fallback-block view, read back from world transforms.
        private static HashSet<Cell> RenderedColumns(PuzzleBoardPresenter presenter, PuzzleItemView view)
        {
            var origin = presenter.Compartments[view.Placement.Compartment].transform.position;
            var cells = new HashSet<Cell>();
            foreach (Transform block in view.VisualRoot)
            {
                var local = block.position - origin;
                cells.Add(new Cell(Mathf.FloorToInt(local.x), Mathf.FloorToInt(-local.z)));
            }
            return cells;
        }

        private static Bounds RendererBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        // ---- Tests ----

        [UnityTest]
        public IEnumerator Compartments_RenderMaskAndStayIndependent()
        {
            var board = Board();
            var presenter = Presenter(board);
            presenter.Sync(new PuzzleState(Spec(board), new[]
            {
                At("a", Block("a", 1, 1), 0, 0), At("b", Block("b", 1, 1), 0, 0, compartment: "pocket")
            }));
            yield return null;

            Assert.That(presenter.Compartments.Keys, Is.EquivalentTo(new[] { "main", "pocket" }));
            Assert.That(presenter.Compartments["main"].ValidCellCount, Is.EqualTo(11), "masked (3,0) has no tile");
            Assert.That(presenter.Compartments["main"].transform.Find("Cell 3,0"), Is.Null);
            Assert.That(presenter.Compartments["pocket"].ValidCellCount, Is.EqualTo(2));
            Assert.That(presenter.ItemViews["a"].transform.position, Is.Not.EqualTo(presenter.ItemViews["b"].transform.position),
                "same local (0,0) in different compartments");
            Assert.That(presenter.ItemViews["b"].transform.parent.parent, Is.SameAs(presenter.Compartments["pocket"].transform));
        }

        [UnityTest]
        public IEnumerator OneViewPerSuitcaseInstance_NestedAndExternalItemsHaveNone()
        {
            var board = Board();
            var shoe = Block("shoe", 2, 1, nest: new NestSpec(1, new[] { "socks" }, null));
            var twin = Block("twin", 1, 1);
            var presenter = Presenter(board);
            presenter.Sync(new PuzzleState(Spec(board), new[]
            {
                At("shoe-1", shoe, 0, 0), Off("socks-1", Block("socks", 1, 1), ItemLocation.NestedIn("shoe-1")),
                At("twin-a", twin, 0, 1), At("twin-b", twin, 1, 1),
                Off("tray-1", Block("x", 1, 1), ItemLocation.SourceTray), Off("staged-1", Block("y", 1, 1), ItemLocation.Staging)
            }));
            yield return null;

            Assert.That(presenter.ItemViews.Keys, Is.EquivalentTo(new[] { "shoe-1", "twin-a", "twin-b" }));
            Assert.That(presenter.ItemViews["twin-a"], Is.Not.SameAs(presenter.ItemViews["twin-b"]));
            Assert.That(presenter.ItemViews["twin-a"].DefinitionId, Is.EqualTo(presenter.ItemViews["twin-b"].DefinitionId));
        }

        [UnityTest]
        public IEnumerator RotatedFootprint_CoversExactlyTheDomainCells()
        {
            var board = Board();
            var presenter = Presenter(board);
            var sneaker = new ItemSpec("sneaker", "default", new[]
            {
                new ItemStateSpec("default", new ItemShape(new[] { new Cell(0, 0), new Cell(0, 1), new Cell(0, 2), new Cell(1, 2) }), 1, All)
            });
            foreach (var rotation in All)
            {
                var state = new PuzzleState(Spec(board), new[] { At("s", sneaker, 0, 0, rotation: rotation) });
                presenter.Sync(state);
                state.TryGetItem("s", out var item);
                var expected = new HashSet<Cell>(PuzzleState.GetPhysicalCells(item).Select(c => c.Column));
                Assert.That(RenderedColumns(presenter, presenter.ItemViews["s"]), Is.EquivalentTo(expected), rotation.ToString());
                Assert.That(presenter.ItemViews["s"].Placement.Rotation, Is.EqualTo(rotation));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Layers_And_Thickness_ReadDistinctly()
        {
            var board = Board();
            var presenter = Presenter(board);
            presenter.Sync(new PuzzleState(Spec(board), new[]
            {
                At("base", Block("base", 2, 2), 0, 1), At("top", Block("top", 1, 1), 1, 2, layer: 1), At("j", Jacket(), 2, 1)
            }));
            yield return null;

            var baseView = presenter.ItemViews["base"];
            var top = presenter.ItemViews["top"];
            Assert.That(top.transform.localPosition.y, Is.EqualTo(PuzzleBoardLayout.LayerHeight).Within(1e-5f));
            Assert.That(RendererBounds(top.VisualRoot).min.y, Is.GreaterThan(RendererBounds(baseView.VisualRoot).max.y), "upper reads above lower");
            Assert.That(RenderedColumns(presenter, top), Is.EquivalentTo(new[] { new Cell(1, 2) }), "stacking keeps logical x / y");

            var jacket = presenter.ItemViews["j"];
            Assert.That(jacket.Thickness, Is.EqualTo(2));
            Assert.That(jacket.VisualRoot.childCount, Is.EqualTo(4), "one view, one block per footprint cell, not per layer");
            Assert.That(RendererBounds(jacket.VisualRoot).size.y, Is.EqualTo(PuzzleBoardLayout.ItemHeight(2)).Within(1e-4f));
            Assert.That(RendererBounds(jacket.VisualRoot).max.y, Is.GreaterThan(RendererBounds(top.VisualRoot).min.y), "thick item spans into layer 1");
        }

        [UnityTest]
        public IEnumerator Sync_UpdatesExistingViews_AndRemovesItemsThatLeaveTheSuitcase()
        {
            var board = Board();
            var shoe = Block("shoe", 2, 1, nest: new NestSpec(1, new[] { "socks" }, null));
            var level = new PuzzleLevel("presenter", new PuzzleState(Spec(board), new[]
            {
                At("rod", Block("rod", 2, 1), 0, 0), At("shoe-1", shoe, 0, 2), Off("socks-1", Block("socks", 1, 1), ItemLocation.NestedIn("shoe-1")),
                Off("j", Jacket(), ItemLocation.Staging)
            }), new RuleSet(board, null), PuzzleObjective.Pack(new[] { "rod", "shoe-1" }));
            var session = new PuzzleSession(level);
            var presenter = Presenter(board);
            presenter.Sync(session.CurrentState);
            var rodView = presenter.ItemViews["rod"];
            var rodVisual = rodView.VisualRoot;

            Assert.That(session.Apply(PuzzleMove.PlaceInSuitcase("rod", "main", new Cell(1, 0), Rotation.Degrees0)).StateChanged, Is.True);
            presenter.Sync(session.CurrentState);
            Assert.That(presenter.ItemViews["rod"], Is.SameAs(rodView), "anchor change keeps the view");
            Assert.That(rodView.VisualRoot, Is.SameAs(rodVisual), "no visual rebuild for a pure move");
            Assert.That(rodView.Placement.Anchor, Is.EqualTo(new Cell(1, 0)));

            session.Apply(PuzzleMove.PlaceInSuitcase("rod", "main", new Cell(0, 0), Rotation.Degrees90));
            presenter.Sync(session.CurrentState);
            Assert.That(presenter.ItemViews["rod"], Is.SameAs(rodView));
            Assert.That(RenderedColumns(presenter, rodView), Is.EquivalentTo(new[] { new Cell(0, 0), new Cell(0, 1) }), "rotation rebuilt the footprint");

            session.Apply(PuzzleMove.PlaceInSuitcase("rod", "pocket", new Cell(0, 0), Rotation.Degrees0));
            presenter.Sync(session.CurrentState);
            Assert.That(rodView.transform.parent.parent, Is.SameAs(presenter.Compartments["pocket"].transform), "compartment change");

            session.Apply(PuzzleMove.MoveToStaging("shoe-1"));
            presenter.Sync(session.CurrentState);
            yield return null;
            Assert.That(presenter.ItemViews.ContainsKey("shoe-1"), Is.False, "left the suitcase");
            Assert.That(presenter.ItemViews.ContainsKey("socks-1"), Is.False);

            session.Apply(PuzzleMove.PlaceInSuitcase("shoe-1", "main", new Cell(2, 2), Rotation.Degrees0));
            session.Apply(PuzzleMove.PlaceInSuitcase("j", "main", new Cell(0, 1), Rotation.Degrees0, "compressed"));
            presenter.Sync(session.CurrentState);
            Assert.That(presenter.ItemViews["shoe-1"].Placement.Anchor, Is.EqualTo(new Cell(2, 2)), "returned with its nested child");
            Assert.That(presenter.ItemViews.ContainsKey("socks-1"), Is.False);
            Assert.That(presenter.ItemViews["j"].StateId, Is.EqualTo("compressed"));
            Assert.That(presenter.ItemViews["j"].Thickness, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator XRay_GhostsOnlyUpperItems_RestoresAndNeverTouchesState()
        {
            yield return SceneManager.LoadSceneAsync("PuzzleGameplay");
            yield return null;
            var template = Object.FindFirstObjectByType<PuzzleGameplayScene>().MaterialTemplate;
            Assert.That(template, Is.Not.Null);

            var board = Board();
            var level = new PuzzleLevel("xray", new PuzzleState(Spec(board), new[]
            {
                At("lower", Block("lower", 2, 2), 0, 0), At("upper", Block("upper", 2, 1), 0, 0, layer: 1)
            }), new RuleSet(board, null), PuzzleObjective.Pack(new[] { "lower", "upper" }));
            var session = new PuzzleSession(level);
            var presenter = Presenter(board, template);
            presenter.Sync(session.CurrentState);
            var upperRenderer = presenter.ItemViews["upper"].VisualRoot.GetComponentInChildren<Renderer>();
            var lowerRenderer = presenter.ItemViews["lower"].VisualRoot.GetComponentInChildren<Renderer>();
            var upperMaterial = upperRenderer.sharedMaterial;
            var hash = session.CurrentState.Hash;
            var state = session.CurrentState;

            presenter.SetXRayEnabled(true);
            yield return null;
            Assert.That(presenter.ItemViews["upper"].IsGhosted, Is.True);
            Assert.That(presenter.ItemViews["lower"].IsGhosted, Is.False);
            Assert.That(upperRenderer.sharedMaterial, Is.Not.SameAs(upperMaterial));
            Assert.That(upperRenderer.sharedMaterial.renderQueue, Is.GreaterThanOrEqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));
            Assert.That(lowerRenderer.sharedMaterial, Is.SameAs(template), "lower item keeps its normal look");
            Assert.That(template.renderQueue, Is.LessThan((int)UnityEngine.Rendering.RenderQueue.Transparent), "shared asset untouched");

            presenter.SetXRayEnabled(false);
            Assert.That(presenter.ItemViews["upper"].IsGhosted, Is.False);
            Assert.That(upperRenderer.sharedMaterial, Is.SameAs(upperMaterial), "normal rendering restored");

            Assert.That(session.CurrentState, Is.SameAs(state));
            Assert.That(session.CurrentState.Hash, Is.EqualTo(hash));
            Assert.That(AccessQueries.GetBlockers(session.CurrentState, "lower"), Is.EqualTo(new[] { "upper" }), "access unchanged");
        }

        [UnityTest]
        public IEnumerator GoldenPrefabs_AlignWithTheV2FootprintInAnOffsetCompartment()
        {
            yield return SceneManager.LoadSceneAsync("PuzzleGameplay");
            yield return null;
            var catalog = Object.FindFirstObjectByType<GoldenItemPrefabCatalog>();
            var upright = new[] { Rotation.Degrees0, Rotation.Degrees90 };
            // Approved golden footprints (golden-item-footprints.md), including the folded sweater that the
            // first-playable catalog does not author yet.
            var golden = new[]
            {
                new ItemSpec("laptop", "open", new[] { new ItemStateSpec("open", new ItemShape(Rect(3, 4)), 1, upright) }),
                new ItemSpec("sneaker", "open", new[]
                {
                    new ItemStateSpec("open", new ItemShape(new[] { new Cell(0, 0), new Cell(0, 1), new Cell(0, 2), new Cell(1, 2) }), 1, All)
                }),
                new ItemSpec("sweater", "open", new[]
                {
                    new ItemStateSpec("open", new ItemShape(Rect(3, 3)), 1, upright),
                    new ItemStateSpec("folded", new ItemShape(Rect(2, 4)), 1, upright)
                })
            };
            var board = new BoardSpec(new[]
            {
                new Compartment("a-pocket", 2, 1, 1, Rect(2, 1)),
                new Compartment("main", 7, 7, 2, Rect(7, 7))
            });

            foreach (var spec in golden)
            {
                var id = spec.Id;
                var presenter = Presenter(board, null, item => PuzzleItemCatalog.ResolveGolden(catalog, item.Definition.Id, item.StateId));
                foreach (var state in spec.States)
                    foreach (var rotation in state.AllowedRotations)
                    {
                        var puzzleState = new PuzzleState(Spec(board), new[] { At(id, spec, 1, 1, 1, rotation, stateId: state.Id) });
                        presenter.Sync(puzzleState);
                        var view = presenter.ItemViews[id];
                        Assert.That(view.UsesPrefab, Is.True);
                        state.TryGetFootprint(rotation, out var footprint);
                        var width = footprint.OccupiedCells.Max(c => c.X) + 1;
                        var depth = footprint.OccupiedCells.Max(c => c.Y) + 1;
                        var origin = presenter.Compartments["main"].transform.position;
                        var bounds = RendererBounds(view.VisualRoot);
                        var label = $"{id}/{state.Id}/{rotation}";
                        Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(origin.x + 1f - 0.001f), label);
                        Assert.That(bounds.max.x, Is.LessThanOrEqualTo(origin.x + 1f + width + 0.001f), label);
                        Assert.That(bounds.max.z, Is.LessThanOrEqualTo(origin.z - 1f + 0.001f), label);
                        Assert.That(bounds.min.z, Is.GreaterThanOrEqualTo(origin.z - 1f - depth - 0.001f), label);
                        Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(PuzzleBoardLayout.LayerHeight - 0.001f), label + " sits on layer 1");
                    }
                Object.Destroy(_root);
                _root = null;
                yield return null;
            }
        }
    }
}
