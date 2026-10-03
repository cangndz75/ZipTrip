using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ZipTrip.Domain;
using ZipTrip.Domain.Board;

namespace ZipTrip.Tests.EditMode
{
    public sealed class BoardSpecTests
    {
        private static IEnumerable<Cell> Rect(int width, int height) =>
            from y in Enumerable.Range(0, height)
            from x in Enumerable.Range(0, width)
            select new Cell(x, y);

        private static KeyValuePair<Cell, string> ColumnZone(int x, int y, string zone) =>
            new KeyValuePair<Cell, string>(new Cell(x, y), zone);

        // Launch suitcase main compartment (L = 2) with blocked corners and a pocket (L = 1).
        private static Compartment Main() => new Compartment("main", 5, 4, 2,
            Rect(5, 4).Where(c => !((c.X == 0 || c.X == 4) && (c.Y == 0 || c.Y == 3))),
            new[] { ColumnZone(1, 0, "top"), ColumnZone(2, 0, "top"), ColumnZone(1, 3, "bottom") });

        private static Compartment Pocket() => new Compartment("pocket", 2, 1, 1, Rect(2, 1));

        [Test]
        public void TwoLayerCompartment_AcceptsZ0AndZ1Only()
        {
            var main = Main();
            Assert.That(main.Layers, Is.EqualTo(2));
            Assert.That(main.Contains(new PhysicalCell("main", 2, 2, 0)), Is.True);
            Assert.That(main.Contains(new PhysicalCell("main", 2, 2, 1)), Is.True);
            Assert.That(main.Contains(new PhysicalCell("main", 2, 2, 2)), Is.False);
            Assert.That(main.Contains(new PhysicalCell("main", 2, 2, -1)), Is.False);
        }

        [Test]
        public void OneLayerCompartment_AcceptsZ0Only()
        {
            var pocket = Pocket();
            Assert.That(pocket.Layers, Is.EqualTo(1));
            Assert.That(pocket.Contains(new PhysicalCell("pocket", 1, 0, 0)), Is.True);
            Assert.That(pocket.Contains(new PhysicalCell("pocket", 1, 0, 1)), Is.False);
        }

        [Test]
        public void MaskedAndOutOfRangeColumns_ReturnFalseWithoutThrowing()
        {
            var board = new BoardSpec(new[] { Main(), Pocket() });
            var probes = new[]
            {
                new PhysicalCell("main", 0, 0, 0),   // blocked corner
                new PhysicalCell("main", 4, 3, 1),   // blocked corner, upper layer
                new PhysicalCell("main", -1, 1, 0),
                new PhysicalCell("main", 5, 1, 0),
                new PhysicalCell("main", 1, 4, 0),
                new PhysicalCell("pocket", 2, 0, 0),
                new PhysicalCell("missing", 0, 0, 0),
                default
            };
            foreach (var probe in probes)
            {
                Assert.That(() => board.Contains(probe), Throws.Nothing, probe.ToString());
                Assert.That(board.Contains(probe), Is.False, probe.ToString());
                Assert.That(board.GetZone(probe), Is.Null, probe.ToString());
            }
            Assert.That(board.Contains(new PhysicalCell("main", 1, 0, 1)), Is.True);
        }

        [Test]
        public void ZoneLookup_IsPerColumnAndSharedByEveryLayer()
        {
            var board = new BoardSpec(new[] { Main() });
            Assert.That(board.GetZone(new PhysicalCell("main", 1, 0, 0)), Is.EqualTo("top"));
            Assert.That(board.GetZone(new PhysicalCell("main", 1, 0, 1)), Is.EqualTo("top"));
            Assert.That(board.GetZone(new PhysicalCell("main", 1, 3, 1)), Is.EqualTo("bottom"));
            Assert.That(board.GetZone(new PhysicalCell("main", 3, 2, 0)), Is.Null);
            Assert.That(board.Compartments[0].GetColumnZone(new Cell(2, 0)), Is.EqualTo("top"));
            Assert.That(board.Compartments[0].ColumnZoneIds, Is.EqualTo(new[] { "bottom", "top" }), "one id per zone, not per layer");

            var deep = new Compartment("deep", 2, 1, 3, Rect(2, 1), new[] { ColumnZone(0, 0, "shoes") });
            for (var z = 0; z < deep.Layers; z++)
                Assert.That(deep.GetZone(new PhysicalCell("deep", 0, 0, z)), Is.EqualTo("shoes"), "z=" + z);
            Assert.That(deep.GetZone(new PhysicalCell("deep", 0, 0, 3)), Is.Null, "outside the layer range");
            Assert.That(deep.GetColumnZone(new Cell(1, 0)), Is.Null);
        }

        [Test]
        public void Fnv1a64_MatchesReferenceVectors()
        {
            Assert.That(Fnv1a64.Compute(new byte[0]), Is.EqualTo(14695981039346656037UL));
            Assert.That(Fnv1a64.Compute(new[] { (byte)'a' }), Is.EqualTo(0xaf63dc4c8601ec8cUL));
            Assert.That(Fnv1a64.Compute(new[] { (byte)'b' }), Is.Not.EqualTo(Fnv1a64.Compute(new[] { (byte)'a' })));
            Assert.That(Fnv1a64.Compute(System.Text.Encoding.ASCII.GetBytes("hello")), Is.EqualTo(0xA430D84680AABD0BUL));
            Assert.That(() => Fnv1a64.Compute(null), Throws.ArgumentNullException);
        }

        [Test]
        public void InvalidZoneDefinitions_AreRejected()
        {
            Assert.That(() => new Compartment("c", 2, 2, 1, Rect(2, 2), new[] { ColumnZone(5, 5, "z") }),
                Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new Compartment("c", 2, 2, 1, Rect(2, 2).Skip(1), new[] { ColumnZone(0, 0, "z") }),
                Throws.InstanceOf<ArgumentOutOfRangeException>(), "zone on a masked cell");
            Assert.That(() => new Compartment("c", 2, 2, 1, Rect(2, 2), new[] { ColumnZone(0, 0, "a"), ColumnZone(0, 0, "b") }),
                Throws.ArgumentException);
            Assert.That(() => new Compartment("c", 2, 2, 1, Rect(2, 2), new[] { ColumnZone(0, 0, " ") }),
                Throws.ArgumentException);
        }

        [Test]
        public void InvalidDimensions_AreRejected()
        {
            Assert.That(() => new Compartment("c", 0, 2, 1, Rect(0, 2)), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new Compartment("c", 2, 0, 1, Rect(2, 0)), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new Compartment("c", 2, 2, 0, Rect(2, 2)), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new Compartment(" ", 2, 2, 1, Rect(2, 2)), Throws.ArgumentException);
            Assert.That(() => new BoardSpec(new[] { Pocket(), Pocket() }), Throws.ArgumentException);
            Assert.That(() => new BoardSpec(Array.Empty<Compartment>()), Throws.ArgumentException);
        }

        [Test]
        public void ArbitraryDimensions_AreNotTiedToTheLegacy8By10Grid()
        {
            var wide = new Compartment("wide", 12, 3, 3, Rect(12, 3));
            Assert.That(wide.Contains(new PhysicalCell("wide", 11, 2, 2)), Is.True);
            Assert.That(wide.Contains(new PhysicalCell("wide", 12, 2, 2)), Is.False);
            Assert.That(wide.GetPhysicalCells().Count(), Is.EqualTo(12 * 3 * 3));
            var tall = new Compartment("tall", 2, 11, 1, Rect(2, 11));
            Assert.That(tall.Contains(new PhysicalCell("tall", 1, 10, 0)), Is.True);
        }

        [Test]
        public void SameCoordinatesInDifferentCompartments_AreDistinct()
        {
            var inMain = new PhysicalCell("main", 1, 0, 0);
            var inPocket = new PhysicalCell("pocket", 1, 0, 0);
            Assert.That(inMain, Is.Not.EqualTo(inPocket));
            Assert.That(inMain == inPocket, Is.False);
            Assert.That(new HashSet<PhysicalCell> { inMain, inPocket }.Count, Is.EqualTo(2));

            var board = new BoardSpec(new[] { Main(), Pocket() });
            Assert.That(board.Contains(inMain), Is.True);
            Assert.That(board.Contains(inPocket), Is.True);
            Assert.That(board.GetZone(inMain), Is.EqualTo("top"));
            Assert.That(board.GetZone(inPocket), Is.Null);
        }

        [Test]
        public void PhysicalCell_HasValueEqualityAndHash()
        {
            var a = new PhysicalCell("main", 2, 3, 1);
            var b = new PhysicalCell("main", new Cell(2, 3), 1);
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a == b, Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(a.Column, Is.EqualTo(new Cell(2, 3)));
            Assert.That(a, Is.Not.EqualTo(new PhysicalCell("main", 2, 3, 0)));
            Assert.That(a, Is.Not.EqualTo(new PhysicalCell("Main", 2, 3, 1)), "ordinal compartment ids");
        }

        [Test]
        public void Iteration_IsDeterministic_CompartmentIdThenLayerThenRowMajor()
        {
            var board = new BoardSpec(new[] { Pocket(), Main() });
            var cells = board.GetPhysicalCells().ToArray();

            Assert.That(board.Compartments.Select(c => c.Id), Is.EqualTo(new[] { "main", "pocket" }));
            Assert.That(cells.Length, Is.EqualTo(16 * 2 + 2));
            Assert.That(cells[0], Is.EqualTo(new PhysicalCell("main", 1, 0, 0)));
            Assert.That(cells[15], Is.EqualTo(new PhysicalCell("main", 3, 3, 0)));
            Assert.That(cells[16], Is.EqualTo(new PhysicalCell("main", 1, 0, 1)));
            Assert.That(cells[32], Is.EqualTo(new PhysicalCell("pocket", 0, 0, 0)));
            Assert.That(board.GetPhysicalCells().ToArray(), Is.EqualTo(cells));
        }

        [Test]
        public void AuthoringOrder_DoesNotChangeEqualityHashOrStableBytes()
        {
            var a = new BoardSpec(new[] { Main(), Pocket() });
            var b = new BoardSpec(new[] { Pocket(), Main() });
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(a.ToStableBytes(), Is.EqualTo(b.ToStableBytes()));

            var bytes = a.ToStableBytes();
            bytes[0] ^= 0xFF;
            Assert.That(a.ToStableBytes()[0], Is.Not.EqualTo(bytes[0]), "stable bytes are a defensive copy");
        }

        [Test]
        public void AnySemanticDifference_ChangesEqualityAndStableBytes()
        {
            var baseline = new BoardSpec(new[] { Main() });
            var variants = new[]
            {
                new Compartment("main", 5, 4, 1, Main().Mask.GetValidCells(), new[] { ColumnZone(1, 0, "top"), ColumnZone(2, 0, "top"), ColumnZone(1, 3, "bottom") }),
                new Compartment("main", 5, 4, 2, Main().Mask.GetValidCells(), new[] { ColumnZone(1, 0, "top"), ColumnZone(1, 3, "bottom") }),
                new Compartment("main", 5, 4, 2, Main().Mask.GetValidCells().Where(c => c != new Cell(3, 0)), new[] { ColumnZone(1, 0, "top"), ColumnZone(2, 0, "top"), ColumnZone(1, 3, "bottom") }),
                new Compartment("bag", 5, 4, 2, Main().Mask.GetValidCells(), new[] { ColumnZone(1, 0, "top"), ColumnZone(2, 0, "top"), ColumnZone(1, 3, "bottom") })
            };
            foreach (var variant in variants)
            {
                var other = new BoardSpec(new[] { variant });
                Assert.That(other, Is.Not.EqualTo(baseline));
                Assert.That(other.ToStableBytes(), Is.Not.EqualTo(baseline.ToStableBytes()));
            }
            Assert.That(new BoardSpec(new[] { Main() }), Is.EqualTo(baseline));
        }

        [Test]
        public void TryGetCompartment_IsSafeForUnknownAndNullIds()
        {
            var board = new BoardSpec(new[] { Main() });
            Assert.That(board.TryGetCompartment("main", out var main), Is.True);
            Assert.That(main.Id, Is.EqualTo("main"));
            Assert.That(board.TryGetCompartment("pocket", out _), Is.False);
            Assert.That(board.TryGetCompartment(null, out _), Is.False);
        }
    }
}
