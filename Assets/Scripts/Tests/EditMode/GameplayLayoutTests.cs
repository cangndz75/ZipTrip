using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ZipTrip.Domain;
using ZipTrip.Unity;

namespace ZipTrip.Tests.EditMode
{
    public sealed class GameplayLayoutTests
    {
        private static readonly Bounds CabinVisual = new Bounds(new Vector3(3f, 0.1f, -4f),
            new Vector3(6.9f, 0.9f, 8.9f));

        [TestCase(1080, 1920, 0, 0)]
        [TestCase(1080, 2340, 0, 0)]
        [TestCase(1080, 2400, 0, 0)]
        [TestCase(1080, 2340, 110, 66)]
        [TestCase(1080, 2400, 140, 80)]
        public void PortraitCompositionKeepsHudBoardAndTrayInsideSafeArea(int width, int height,
            int notch, int gestureBar)
        {
            var container = ContainerFixtures.CreateCabin(new Cell(0, 0));
            var safe = Rect.MinMaxRect(0f, gestureBar, width, height - notch);
            var frame = Compose(container, new Vector2(width, height), safe, L4Entries());

            Assert.That(frame.HudPixels.yMax, Is.LessThanOrEqualTo(safe.yMax + 0.01f));
            Assert.That(frame.BoardPixels.yMax, Is.LessThanOrEqualTo(frame.HudPixels.yMin + 0.01f));
            Assert.That(frame.TrayPixels.yMax, Is.LessThanOrEqualTo(frame.BoardPixels.yMin));
            Assert.That(frame.TrayPixels.yMin, Is.GreaterThanOrEqualTo(safe.yMin));
            Assert.That(frame.TrayPixels.height, Is.GreaterThan(0.15f * height));
            Assert.That(frame.OrthographicSize, Is.GreaterThanOrEqualTo(
                FixedGameplayCamera.OrthographicSize(6, width / (float)height) - 0.0001f));
            // Board keeps the ADR-0002 horizontal margin inside the view.
            var halfView = frame.OrthographicSize * frame.Aspect;
            Assert.That(CabinVisual.extents.x + FixedGameplayCamera.HorizontalMargin,
                Is.LessThanOrEqualTo(halfView + 0.0001f));
            Assert.That(frame.TrayScale, Is.GreaterThan(GameplayMetrics.MinTrayScale));
        }

        [Test]
        public void CompositionRejectsDegenerateScreen()
        {
            var container = ContainerFixtures.CreateCabin(new Cell(0, 0));
            Assert.That(() => Compose(container, Vector2.zero, new Rect(0f, 0f, 0f, 0f), L4Entries()),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void TraySlotsNeverOverlapAndStayInsideBand()
        {
            var frame = Frame(6.8f, 4.2f);
            var entries = L4Entries();
            var slots = GameplayLayout.LayoutTray(entries, frame, out var scale);

            Assert.That(slots.Length, Is.EqualTo(entries.Count));
            for (var i = 0; i < slots.Length; i++)
            {
                Assert.That(slots[i].Scale, Is.EqualTo(scale), "tray uses one uniform scale");
                var card = slots[i].Card;
                Assert.That(card.xMin, Is.GreaterThanOrEqualTo(frame.Band.XMin - 0.0001f));
                Assert.That(card.xMax, Is.LessThanOrEqualTo(frame.Band.XMax + 0.0001f));
                Assert.That(card.yMin, Is.GreaterThanOrEqualTo(frame.Band.ZBottom - 0.0001f));
                Assert.That(card.yMax, Is.LessThanOrEqualTo(frame.Band.ZTop + 0.0001f));
                for (var j = i + 1; j < slots.Length; j++)
                    Assert.That(card.Overlaps(slots[j].Card), Is.False, i + " overlaps " + j);
            }
        }

        [Test]
        public void ChipsSitInsideOwnCardBelowTheItem()
        {
            var frame = Frame(6.8f, 4.2f);
            var entries = L4Entries();
            var slots = GameplayLayout.LayoutTray(entries, frame, out var scale);
            for (var i = 0; i < slots.Length; i++)
            {
                var itemBottom = slots[i].Origin.z + scale * entries[i].MinZ -
                                 slots[i].HitPadding * scale;
                foreach (var chip in Chips(entries[i], slots[i]))
                {
                    Assert.That(slots[i].Card.Contains(new Vector2(chip.x, chip.z)), Is.True);
                    for (var j = 0; j < slots.Length; j++)
                        if (j != i)
                            Assert.That(slots[j].Card.Contains(new Vector2(chip.x, chip.z)), Is.False);
                    var chipTop = chip.z + (GameplayMetrics.ChipHeight * 0.5f +
                        GameplayMetrics.ChipHitPadding) * frame.WorldPerDp / Mathf.Sin(75f * Mathf.Deg2Rad);
                    Assert.That(chipTop, Is.LessThan(itemBottom), "chip hit area reaches the item drag area");
                }
            }
            Assert.That(slots[2].RotateChip, Is.EqualTo(Vector3.zero), "single-rotation camera has no chip");
            Assert.That(slots[2].FoldChip, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void VisualOverflowWidensTheSlotBeyondTheFootprint()
        {
            var frame = Frame(6.8f, 3f);
            var footprintOnly = new List<TrayEntry> { Entry(2, 2, false, false) };
            var overflowing = new List<TrayEntry> { Entry(2, 2, false, false) };
            var entry = overflowing[0];
            entry.MinX = -0.4f;
            entry.MaxX = 2.4f;
            overflowing[0] = entry;

            var a = GameplayLayout.LayoutTray(footprintOnly, frame, out var scaleA);
            var b = GameplayLayout.LayoutTray(overflowing, frame, out var scaleB);
            Assert.That(scaleA, Is.EqualTo(scaleB));
            Assert.That(b[0].Card.width, Is.GreaterThan(a[0].Card.width));
        }

        [Test]
        public void OverfullTrayClampsToMinimumScaleInsteadOfFailing()
        {
            var frame = Frame(1.5f, 0.6f);
            var slots = GameplayLayout.LayoutTray(L4Entries(), frame, out var scale);
            Assert.That(scale, Is.EqualTo(GameplayMetrics.MinTrayScale));
            Assert.That(slots.Length, Is.EqualTo(7));
        }

        private static IEnumerable<Vector3> Chips(TrayEntry entry, TraySlot slot)
        {
            if (entry.Rotate) yield return slot.RotateChip;
            if (entry.Fold) yield return slot.FoldChip;
        }

        private static GameplayFrame Compose(ContainerDefinition container, Vector2 screen, Rect safe,
            IReadOnlyList<TrayEntry> entries) =>
            GameplayLayout.Compose(screen, safe, CabinVisual,
                FixedGameplayCamera.ContainerCenter(container.Mask), 6, entries);

        private static TrayFrame Frame(float width, float depth) => new TrayFrame
        {
            Band = new TrayBand { XMin = -width * 0.5f, XMax = width * 0.5f, ZTop = -9f, ZBottom = -9f - depth },
            WorldPerDp = 0.0063f,
            MaxScale = GameplayMetrics.MaxTrayScale
        };

        // L4 tray: book, bottle, camera, laptop, scarf, sneaker, sweater.
        private static List<TrayEntry> L4Entries() => new List<TrayEntry>
        {
            Entry(2, 3, true, false), Entry(1, 3, true, false), Entry(2, 2, false, false),
            Entry(3, 4, true, false), Entry(1, 6, true, true), Entry(2, 3, true, false),
            Entry(3, 3, true, true)
        };

        private static TrayEntry Entry(int width, int depth, bool rotate, bool fold) => new TrayEntry
        {
            MinX = 0f, MaxX = width, MinZ = -depth, MaxZ = 0f, Height = 0.4f, Rotate = rotate, Fold = fold
        };
    }
}
