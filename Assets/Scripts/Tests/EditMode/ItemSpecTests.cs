using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ZipTrip.Domain;
using ZipTrip.Domain.Items;

namespace ZipTrip.Tests.EditMode
{
    public sealed class ItemSpecTests
    {
        private static readonly Rotation[] AllRotations =
            { Rotation.Degrees0, Rotation.Degrees90, Rotation.Degrees180, Rotation.Degrees270 };

        private static ItemShape Rect(int width, int height) =>
            new ItemShape(from y in Enumerable.Range(0, height) from x in Enumerable.Range(0, width) select new Cell(x, y));

        private static ItemStateSpec State(string id, ItemShape footprint, int thickness = 1) =>
            new ItemStateSpec(id, footprint, thickness, AllRotations);

        private static StateTransition Fold(string from, string to) => new StateTransition(ItemModifier.Fold, from, to);
        private static StateTransition Compress(string from, string to) => new StateTransition(ItemModifier.Compress, from, to);

        // Golden sweater: open 3x3 (9) <-> folded 2x4 (8).
        private static ItemSpec Sweater() => new ItemSpec("sweater", "open",
            new[] { State("open", Rect(3, 3)), State("folded", Rect(2, 4)) },
            new[] { "clothes", "soft" },
            new[] { Fold("open", "folded"), Fold("folded", "open") });

        // Jacket: thickness 2 -> compressed thickness 1, same footprint.
        private static ItemSpec Jacket() => new ItemSpec("jacket", "normal",
            new[] { State("normal", Rect(2, 3), 2), State("compressed", Rect(2, 3), 1) },
            new[] { "clothes" },
            new[] { Compress("normal", "compressed") });

        private static ItemSpec Single(string id, params string[] tags) =>
            new ItemSpec(id, "default", new[] { State("default", Rect(1, 1)) }, tags);

        private static void AssertRejected(TestDelegate construct, string reason)
        {
            Assert.That(construct, Throws.InstanceOf<ArgumentException>().With.Message.StartsWith(reason));
        }

        // ---- Item states ----

        [Test]
        public void Thickness_OneAndTwoAccepted_ZeroNegativeAndThreeRejected()
        {
            Assert.That(State("a", Rect(1, 1), 1).Thickness, Is.EqualTo(1));
            Assert.That(State("a", Rect(1, 1), 2).Thickness, Is.EqualTo(2));
            AssertRejected(() => State("a", Rect(1, 1), 0), "InvalidThickness");
            AssertRejected(() => State("a", Rect(1, 1), -1), "InvalidThickness");
            AssertRejected(() => State("a", Rect(1, 1), 3), "InvalidThickness");
        }

        [Test]
        public void State_RejectsMissingIdFootprintAndRotations()
        {
            AssertRejected(() => State(" ", Rect(1, 1)), "MissingStateId");
            AssertRejected(() => State("a", null), "MissingFootprint");
            AssertRejected(() => new ItemStateSpec("a", Rect(1, 1), 1, new Rotation[0]), "MissingRotations");
            AssertRejected(() => new ItemStateSpec("a", Rect(1, 1), 1, new[] { (Rotation)45 }), "UnsupportedRotation");
        }

        [Test]
        public void DefaultStateLookup_ReturnsAuthoredDefault()
        {
            var sweater = Sweater();
            Assert.That(sweater.DefaultState.Id, Is.EqualTo("open"));
            Assert.That(sweater.TryGetState("folded", out var folded), Is.True);
            Assert.That(folded.Footprint.CellCount, Is.EqualTo(8));
            Assert.That(sweater.TryGetState("missing", out _), Is.False);
            Assert.That(sweater.TryGetState(null, out _), Is.False);
        }

        [Test]
        public void Definition_RejectsDuplicateStateMissingDefaultAndBadIds()
        {
            AssertRejected(() => new ItemSpec("x", "a", new[] { State("a", Rect(1, 1)), State("a", Rect(2, 1)) }),
                "DuplicateStateId");
            AssertRejected(() => new ItemSpec("x", "b", new[] { State("a", Rect(1, 1)) }), "MissingDefaultState");
            AssertRejected(() => new ItemSpec("x", null, new[] { State("a", Rect(1, 1)) }), "MissingDefaultState");
            AssertRejected(() => new ItemSpec("x", "a", new ItemStateSpec[0]), "MissingStates");
            AssertRejected(() => new ItemSpec("", "a", new[] { State("a", Rect(1, 1)) }), "MissingItemId");
            AssertRejected(() => new ItemSpec("x", "a", new[] { State("a", Rect(1, 1)) }, new[] { "ok", " " }), "InvalidTag");
        }

        [Test]
        public void ItemShape_IsReusedAndInvalidShapesStayRejected()
        {
            Assert.That(() => new ItemShape(new Cell[0]), Throws.ArgumentException);
            Assert.That(() => new ItemShape(new[] { new Cell(0, 0), new Cell(0, 0) }), Throws.ArgumentException);
        }

        // ---- Rotation (ADR-0003) ----

        [Test]
        public void Rotation_UsesSharedItemShapeMathAndFourQuarterTurnsAreIdentity()
        {
            var sneaker = new ItemShape(new[] { new Cell(0, 0), new Cell(0, 1), new Cell(0, 2), new Cell(1, 2) });
            var state = State("default", sneaker);

            Assert.That(state.TryGetFootprint(Rotation.Degrees90, out var turned), Is.True);
            Assert.That(turned.OccupiedCells, Is.EqualTo(sneaker.Rotate(Rotation.Degrees90).OccupiedCells));

            var shape = state.Footprint;
            for (var i = 0; i < 4; i++)
                shape = shape.Rotate(Rotation.Degrees90);
            Assert.That(shape.OccupiedCells, Is.EqualTo(sneaker.OccupiedCells));
        }

        [Test]
        public void Rotation_NotAllowedIsReportedWithoutThrowing()
        {
            var state = new ItemStateSpec("default", Rect(1, 3), 1, new[] { Rotation.Degrees0, Rotation.Degrees180 });
            Assert.That(state.AllowedRotations, Is.EqualTo(new[] { Rotation.Degrees0, Rotation.Degrees180 }));
            Assert.That(state.TryGetFootprint(Rotation.Degrees90, out var footprint), Is.False);
            Assert.That(footprint, Is.Null);
            Assert.That(state.TryGetFootprint(Rotation.Degrees180, out _), Is.True);
        }

        // ---- Fold ----

        [Test]
        public void Fold_OneCellLossAndReverseAccepted_GeometryActuallyDiffers()
        {
            var sweater = Sweater();
            Assert.That(sweater.TryGetTransition("open", ItemModifier.Fold, out var folded), Is.True);
            Assert.That(folded.Id, Is.EqualTo("folded"));
            Assert.That(sweater.TryGetTransition("folded", ItemModifier.Fold, out var open), Is.True);
            Assert.That(open.Id, Is.EqualTo("open"));

            var openCells = sweater.DefaultState.Footprint.OccupiedCells;
            Assert.That(AllRotations.All(r => !folded.Footprint.OccupiedCells.SequenceEqual(
                sweater.DefaultState.Footprint.Rotate(r).OccupiedCells)), "folded is not a rotation of open");
            Assert.That(openCells.Count - folded.Footprint.CellCount, Is.EqualTo(1));
        }

        [Test]
        public void Fold_EqualAreaAccepted()
        {
            // Scarf 1x6 <-> 2x3 (Blueprint §8 example).
            var scarf = new ItemSpec("scarf", "long", new[] { State("long", Rect(1, 6)), State("short", Rect(2, 3)) },
                transitions: new[] { Fold("long", "short") });
            Assert.That(scarf.TryGetTransition("long", ItemModifier.Fold, out var target), Is.True);
            Assert.That(target.Footprint.CellCount, Is.EqualTo(6));
        }

        [Test]
        public void Fold_AreaLossAboveOneCell_IsRejectedDirectlyAndThroughAChain()
        {
            AssertRejected(() => new ItemSpec("x", "a", new[] { State("a", Rect(3, 3)), State("b", Rect(7, 1)) },
                transitions: new[] { Fold("a", "b") }), "FoldAreaLossTooLarge");
            // a(9) -> b(8) -> c(7): each edge loses one cell, but the chain would be a free 2-cell reduction.
            AssertRejected(() => new ItemSpec("x", "a",
                new[] { State("a", Rect(3, 3)), State("b", Rect(2, 4)), State("c", Rect(7, 1)) },
                transitions: new[] { Fold("a", "b"), Fold("b", "c") }), "FoldAreaLossTooLarge");
        }

        [Test]
        public void Fold_MissingSelfDuplicateAndRotationOnlyTransitions_AreRejected()
        {
            var states = new[] { State("open", Rect(3, 3)), State("folded", Rect(2, 4)), State("turned", Rect(4, 2)) };
            AssertRejected(() => new ItemSpec("x", "open", states, transitions: new[] { Fold("open", "gone") }),
                "MissingTransitionState");
            AssertRejected(() => new ItemSpec("x", "open", states, transitions: new[] { Fold("gone", "open") }),
                "MissingTransitionState");
            AssertRejected(() => new ItemSpec("x", "open", states, transitions: new[] { Fold("open", "open") }),
                "SelfTransition");
            AssertRejected(() => new ItemSpec("x", "open", states,
                transitions: new[] { Fold("open", "folded"), Fold("open", "turned") }), "DuplicateTransition");
            AssertRejected(() => new ItemSpec("x", "open", states,
                transitions: new[] { Fold("open", "folded"), Fold("open", "folded") }), "DuplicateTransition");
            AssertRejected(() => new ItemSpec("x", "open", states, transitions: new[] { Fold("folded", "turned") }),
                "FoldGeometryUnchanged");
        }

        [Test]
        public void TransitionQueries_DoNotThrowForUnknownInput()
        {
            var sweater = Sweater();
            Assert.That(sweater.TryGetTransition("open", ItemModifier.Compress, out var none), Is.False);
            Assert.That(none, Is.Null);
            Assert.That(sweater.TryGetTransition("missing", ItemModifier.Fold, out _), Is.False);
            Assert.That(sweater.TryGetTransition(null, ItemModifier.Fold, out _), Is.False);
        }

        // ---- Compress ----

        [Test]
        public void Compress_ThicknessTwoToOneAccepted()
        {
            var jacket = Jacket();
            Assert.That(jacket.DefaultState.Thickness, Is.EqualTo(2));
            Assert.That(jacket.TryGetTransition("normal", ItemModifier.Compress, out var compressed), Is.True);
            Assert.That(compressed.Thickness, Is.EqualTo(1));
            Assert.That(compressed.Footprint.OccupiedCells, Is.EqualTo(jacket.DefaultState.Footprint.OccupiedCells));
            Assert.That(jacket.TryGetTransition("normal", ItemModifier.Fold, out _), Is.False, "Compress is not Fold");
        }

        [Test]
        public void Compress_InvalidTargetsAreRejected()
        {
            var states = new[] { State("normal", Rect(2, 3), 2), State("compressed", Rect(2, 3), 1), State("flat", Rect(2, 3), 2) };
            AssertRejected(() => new ItemSpec("x", "normal", states, transitions: new[] { Compress("normal", "gone") }),
                "MissingTransitionState");
            AssertRejected(() => new ItemSpec("x", "normal", states, transitions: new[] { Compress("normal", "flat") }),
                "CompressThicknessUnchanged");
            AssertRejected(() => new ItemSpec("x", "normal", states, transitions: new[] { Compress("normal", "normal") }),
                "SelfTransition");
            AssertRejected(() => new ItemSpec("x", "normal", states,
                transitions: new[] { new StateTransition((ItemModifier)7, "normal", "compressed") }), "UnsupportedModifier");
        }

        [Test]
        public void ItemModel_HasNoVacuumDependency()
        {
            Assert.That(Enum.GetNames(typeof(ItemModifier)), Is.EqualTo(new[] { "Fold", "Compress" }));
            foreach (var type in new[] { typeof(ItemSpec), typeof(ItemStateSpec), typeof(NestSpec), typeof(StateTransition) })
                Assert.That(type.GetProperties().Select(p => p.Name).Where(n => n.Contains("Vacuum")), Is.Empty, type.Name);
        }

        // ---- Nest ----

        [Test]
        public void Nest_CapacityZeroIsNone_PositiveIsFinite_NegativeRejected()
        {
            Assert.That(Sweater().Nest, Is.SameAs(NestSpec.None));
            Assert.That(NestSpec.None.Capacity, Is.EqualTo(0));
            Assert.That(NestSpec.None.Accepts(Single("socks")), Is.False);
            Assert.That(new NestSpec(2, new[] { "socks" }, null).Capacity, Is.EqualTo(2));
            Assert.That(() => new NestSpec(-1, new[] { "socks" }, null),
                Throws.InstanceOf<ArgumentOutOfRangeException>().With.Message.StartsWith("InvalidNestCapacity"));
        }

        [Test]
        public void Nest_MalformedCompatibilityIsRejected()
        {
            AssertRejected(() => new NestSpec(1, null, null), "MalformedNestCompatibility");
            AssertRejected(() => new NestSpec(0, new[] { "socks" }, null), "MalformedNestCompatibility");
            AssertRejected(() => new NestSpec(1, new[] { " " }, null), "MalformedNestCompatibility");
            AssertRejected(() => new NestSpec(1, null, new[] { "small", null }), "MalformedNestCompatibility");
        }

        [Test]
        public void Nest_AcceptsAuthoredIdsAndTagsOnly()
        {
            var nest = new NestSpec(1, new[] { "socks" }, new[] { "small" });
            Assert.That(nest.Accepts(Single("socks")), Is.True, "accepted item id");
            Assert.That(nest.Accepts(Single("charger", "tech", "small")), Is.True, "accepted tag");
            Assert.That(nest.Accepts(Single("laptop", "tech")), Is.False, "incompatible child");
            Assert.That(nest.Accepts(Single("tiny-but-unlisted")), Is.False, "size never implies compatibility");
            Assert.That(nest.Accepts(null), Is.False);
        }

        [Test]
        public void Nest_LookupIsDeterministicAndIndependentOfAuthoringOrder()
        {
            var a = new NestSpec(2, new[] { "socks", "belt" }, new[] { "small", "cable" });
            var b = new NestSpec(2, new[] { "belt", "socks", "belt" }, new[] { "cable", "small" });
            Assert.That(a.AcceptedItemIds, Is.EqualTo(new[] { "belt", "socks" }));
            Assert.That(b.AcceptedTags, Is.EqualTo(a.AcceptedTags));
            foreach (var child in new[] { Single("socks"), Single("belt"), Single("x", "cable"), Single("laptop", "tech") })
                Assert.That(b.Accepts(child), Is.EqualTo(a.Accepts(child)), child.Id);
        }

        // ---- Determinism / equality ----

        [Test]
        public void AuthoringOrder_DoesNotChangeEqualityHashOrStableBytes()
        {
            var nest = new NestSpec(1, new[] { "socks", "belt" }, new[] { "small" });
            var a = new ItemSpec("shoe", "open",
                new[] { State("open", Rect(3, 3)), State("folded", Rect(2, 4)) },
                new[] { "clothes", "soft" }, new[] { Fold("open", "folded"), Fold("folded", "open") }, nest);
            var b = new ItemSpec("shoe", "open",
                new[] { State("folded", Rect(2, 4)), State("open", Rect(3, 3)) },
                new[] { "soft", "clothes", "soft" }, new[] { Fold("folded", "open"), Fold("open", "folded") },
                new NestSpec(1, new[] { "belt", "socks" }, new[] { "small" }));

            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(a.ToStableBytes(), Is.EqualTo(b.ToStableBytes()));
            Assert.That(a.States.Select(s => s.Id), Is.EqualTo(new[] { "folded", "open" }));
            Assert.That(a.Transitions.Select(t => t.FromStateId), Is.EqualTo(new[] { "folded", "open" }));
        }

        [Test]
        public void AnySemanticDifference_ChangesEqualityAndStableBytes()
        {
            var baseline = Sweater();
            var variants = new[]
            {
                new ItemSpec("cardigan", "open", new[] { State("open", Rect(3, 3)), State("folded", Rect(2, 4)) },
                    new[] { "clothes", "soft" }, new[] { Fold("open", "folded"), Fold("folded", "open") }),
                new ItemSpec("sweater", "folded", new[] { State("open", Rect(3, 3)), State("folded", Rect(2, 4)) },
                    new[] { "clothes", "soft" }, new[] { Fold("open", "folded"), Fold("folded", "open") }),
                new ItemSpec("sweater", "open", new[] { State("open", Rect(3, 3), 2), State("folded", Rect(2, 4)) },
                    new[] { "clothes", "soft" }, new[] { Fold("open", "folded"), Fold("folded", "open") }),
                new ItemSpec("sweater", "open", new[] { State("open", Rect(3, 3)), State("folded", Rect(4, 2)) },
                    new[] { "clothes", "soft" }, new[] { Fold("open", "folded"), Fold("folded", "open") }),
                new ItemSpec("sweater", "open", new[] { new ItemStateSpec("open", Rect(3, 3), 1, new[] { Rotation.Degrees0 }), State("folded", Rect(2, 4)) },
                    new[] { "clothes", "soft" }, new[] { Fold("open", "folded"), Fold("folded", "open") }),
                new ItemSpec("sweater", "open", new[] { State("open", Rect(3, 3)), State("folded", Rect(2, 4)) },
                    new[] { "clothes" }, new[] { Fold("open", "folded"), Fold("folded", "open") }),
                new ItemSpec("sweater", "open", new[] { State("open", Rect(3, 3)), State("folded", Rect(2, 4)) },
                    new[] { "clothes", "soft" }, new[] { Fold("open", "folded") }),
                new ItemSpec("sweater", "open", new[] { State("open", Rect(3, 3)), State("folded", Rect(2, 4)) },
                    new[] { "clothes", "soft" }, new[] { Fold("open", "folded"), Fold("folded", "open") },
                    new NestSpec(1, new[] { "socks" }, null))
            };

            Assert.That(Sweater(), Is.EqualTo(baseline));
            foreach (var variant in variants)
            {
                Assert.That(variant, Is.Not.EqualTo(baseline));
                Assert.That(variant.ToStableBytes(), Is.Not.EqualTo(baseline.ToStableBytes()));
            }
        }

        [Test]
        public void Collections_AreReadOnly()
        {
            var sweater = Sweater();
            Assert.That(() => ((IList<string>)sweater.Tags).Add("x"), Throws.InstanceOf<NotSupportedException>());
            Assert.That(() => ((IList<ItemStateSpec>)sweater.States).Clear(), Throws.InstanceOf<NotSupportedException>());
            var bytes = sweater.ToStableBytes();
            bytes[0] ^= 0xFF;
            Assert.That(sweater.ToStableBytes(), Is.Not.EqualTo(bytes));
        }
    }
}
